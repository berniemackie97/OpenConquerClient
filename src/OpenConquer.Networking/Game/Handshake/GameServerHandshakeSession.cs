using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using OpenConquer.Networking.Authentication;
using OpenConquer.Networking.Game.Cryptography;
using OpenConquer.Networking.Game.Framing;
using OpenConquer.Networking.Game.Login;

namespace OpenConquer.Networking.Game.Handshake;

/// <summary>
/// Owns one live IPv4 loopback GameServer TCP socket and its negotiated directional
/// CAST5 streams after exactly one server-initiated DH exchange. This is NOT
/// authentication, proof submission or admission to the world.
/// </summary>
/// <remarks>
/// A <c>NEW_ROLE</c> login control opens the create-role continuation on the same socket: 1001 requests, each
/// answered on the create-role channel, until one is accepted or the request is cancelled; then exactly one
/// repeated 1052 proof, after which the existing-character control, prelude and 1006 reads apply again.
/// </remarks>
internal sealed class GameServerHandshakeSession : IAsyncDisposable
{
    private readonly TcpClient _client;
    private readonly GameSessionCipher _cipher;
    private readonly Lock _lifetimeGate = new();
    private LoginAccountExchangeResult? _account;
    private readonly uint _sessionToken;
    private byte[]? _repeatedProof;
    private readonly GameSecuredFrameEncoder _encoder;
    private readonly GameSecuredFrameDecoder _decoder;
    private readonly CancellationTokenSource _lifetimeCancellation = new();

    private TaskCompletionSource? _operationsDrained;
    private Task? _disposeTask;
    private int _activeOperations;
    private int _phase; // 0: handshake complete, 1: proof sending, 2: ready, 3: reading, 4: terminal
    private int _loginControlState; // 0: unclaimed, 1: reading, 2: resolved, 3: raw-read bypassed
    private int _existingCharacterPreludeState; // 0: unclaimed, 1: reading, 2: resolved, 3: bypassed
    private int _characterInfoState; // 0: unclaimed, 1: reading, 2: resolved, 3: bypassed
    private CreationState _creationState;
    private bool _existingCharacterRoute;
    private bool _disposed;

    private GameServerHandshakeSession(TcpClient client, GameSessionCipher cipher,
        LoginAccountExchangeResult account)
    {
        _client = client;
        _cipher = cipher;
        _account = account;
        _sessionToken = account.Response.SessionToken;
        _encoder = new GameSecuredFrameEncoder(cipher);
        _decoder = new GameSecuredFrameDecoder(cipher);
    }

    // Future secured framing must stay owned by this session, not expose the
    // raw TCP stream or a reusable, untrusted remote connection to UI code.
    internal GameSessionCipher SessionCipher
    {
        get
        {
            lock (_lifetimeGate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return _cipher;
            }
        }
    }

    /// <summary>
    /// Claims the post-1055 redirect once after both account reports are complete.
    /// No DNS, proxy, remote IP or arbitrary host supplied by the AccountServer
    /// may be connected. Disposing the account result cancels an in-flight exchange.
    /// </summary>
    internal static async Task<GameServerHandshakeSession> OpenAsync(
        LoginAccountExchangeResult account, GameServerHandshakeTimeouts timeouts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);
        timeouts.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        (int port, CancellationToken authorityLifetime) = account.ClaimLoopbackGameHandoff();
        using CancellationTokenSource operation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, authorityLifetime);

        TcpClient client = new(AddressFamily.InterNetwork)
        {
            NoDelay = true
        };
        GameSessionCipher? sessionCipher = null;
        try
        {
            using (CancellationTokenSource connect = CancellationTokenSource.CreateLinkedTokenSource(operation.Token))
            {
                connect.CancelAfter(timeouts.Connect);
                try
                {
                    await client.ConnectAsync(IPAddress.Loopback, port, connect.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!operation.IsCancellationRequested)
                {
                    throw new LoginConnectFailureException(LoginConnectTarget.Game, true,
                        new TimeoutException("GameServer loopback connection timed out."));
                }
                catch (SocketException exception)
                {
                    throw new LoginConnectFailureException(LoginConnectTarget.Game, false, exception);
                }
            }

            using CancellationTokenSource exchangeDeadline =
                CancellationTokenSource.CreateLinkedTokenSource(operation.Token);
            exchangeDeadline.CancelAfter(timeouts.Exchange);

            try
            {
                NetworkStream stream = client.GetStream();
                byte[] challengeBytes = new byte[GameServerChallengeDecoder.MaximumFrameLength];
                byte[]? encryptedResponse = null;
                try
                {
                    // The server speaks first. Read no more than the 15-byte
                    // challenge prefix before learning the bounded total size.
                    await stream.ReadExactlyAsync(challengeBytes.AsMemory(
                        0, GameServerChallengeDecoder.HeaderLength), exchangeDeadline.Token)
                        .ConfigureAwait(false);

                    using (GameHandshakeBootstrapCipher decryptor =
                           GameHandshakeBootstrapCipher.CreateDecryptor())
                    {
                        decryptor.Transform(
                            challengeBytes.AsSpan(0, GameServerChallengeDecoder.HeaderLength),
                            challengeBytes.AsSpan(0, GameServerChallengeDecoder.HeaderLength));

                        if (!GameServerChallengeDecoder.TryGetFrameLength(
                                challengeBytes.AsSpan(0, GameServerChallengeDecoder.HeaderLength),
                                out int frameLength))
                        {
                            throw new InvalidDataException("GameServer challenge header was incomplete.");
                        }

                        int remaining = frameLength - GameServerChallengeDecoder.HeaderLength;
                        await stream.ReadExactlyAsync(challengeBytes.AsMemory(
                            GameServerChallengeDecoder.HeaderLength, remaining),
                            exchangeDeadline.Token).ConfigureAwait(false);
                        decryptor.Transform(
                            challengeBytes.AsSpan(GameServerChallengeDecoder.HeaderLength, remaining),
                            challengeBytes.AsSpan(GameServerChallengeDecoder.HeaderLength, remaining));

                        if (!GameServerChallengeDecoder.TryRead(
                                challengeBytes.AsSpan(0, frameLength), out GameServerChallenge? challenge) ||
                            challenge is null)
                        {
                            throw new InvalidDataException("GameServer challenge is incomplete.");
                        }

                        using GameClientDhExchange clientExchange = GameClientDhExchange.Create(challenge);
                        encryptedResponse = new byte[clientExchange.EncryptedResponseLength];
                        clientExchange.WriteEncryptedResponse(encryptedResponse);

                        // Any partial or uncertain send is terminal; never retry.
                        exchangeDeadline.Token.ThrowIfCancellationRequested();
                        await stream.WriteAsync(encryptedResponse, exchangeDeadline.Token)
                            .ConfigureAwait(false);

                        // Only after an accepted local socket write may session
                        // cipher ownership transfer. This does not acknowledge
                        // remote reception or redeem a login ticket.
                        sessionCipher = GameSessionCipher.CreateFromExchange(clientExchange);
                        exchangeDeadline.Token.ThrowIfCancellationRequested();
                    }
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(challengeBytes);
                    if (encryptedResponse is not null)
                    {
                        CryptographicOperations.ZeroMemory(encryptedResponse);
                    }
                }
            }
            catch (OperationCanceledException) when (!operation.IsCancellationRequested)
            {
                throw new TimeoutException("GameServer DH handshake timed out.");
            }

            operation.Token.ThrowIfCancellationRequested();
            return new GameServerHandshakeSession(client, sessionCipher ??
                throw new InvalidOperationException("GameServer session cipher was not negotiated."), account);
        }
        catch
        {
            sessionCipher?.Dispose();
            client.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Sends the originating AccountServer result's one-use 1052 ticket proof.
    /// Returning from the write does not mean that the GameServer admitted the
    /// player. Any attempted write failure makes the connection terminal.
    /// </summary>
    internal async Task SendLoginProofAsync(ushort localeTag, ReadOnlyMemory<byte> hardwareAddress,
        TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ValidateTimeout(timeout);
        cancellationToken.ThrowIfCancellationRequested();
        EnterOperation(expectedPhase: 0, activePhase: 1);

        bool completed = false;
        byte[] packet = new byte[GameLoginProof1052.PacketLength];
        try
        {
            (_account ?? throw new InvalidOperationException("Account proof state was already released."))
                .WriteGameLoginProof(packet, localeTag, hardwareAddress.Span);
            await WriteSecuredPacketAsync(packet, timeout, "GameServer login proof write timed out.",
                cancellationToken).ConfigureAwait(false);

            lock (_lifetimeGate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                _repeatedProof = (byte[])packet.Clone();
                _account = null;
            }

            completed = true;
        }
        catch
        {
            _client.Dispose();
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(packet);
            ExitOperation(completed);
        }
    }

    /// <summary>
    /// Reads exactly one complete server-to-client secured frame, after proof
    /// transmission. A clean EOF at a frame boundary returns null and closes
    /// the session; a partial frame, invalid trailer, timeout or cancellation
    /// is terminal. The caller must dispose the returned frame.
    /// </summary>
    internal Task<GameSecuredInboundFrame?> ReadFrameAsync(
        TimeSpan timeout, CancellationToken cancellationToken = default) =>
        ReadFrameCoreAsync(timeout, SecuredRead.Raw, cancellationToken);

    /// <summary>
    /// Resolves the first post-proof login-channel 1004 once. Accepted routes
    /// leave the session alive for separate character bootstrap; a refusal or
    /// any invalid/incomplete response terminates it.
    /// </summary>
    internal async Task<GameLoginControlOutcome> ReadLoginControlAsync(
        TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ValidateTimeout(timeout);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lifetimeGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_phase != 2 || _loginControlState != 0)
            {
                throw new InvalidOperationException("The GameServer login control can only be read once per proof, before any raw secured reads.");
            }

            _loginControlState = 1;
        }

        try
        {
            using GameSecuredInboundFrame? frame = await ReadFrameCoreAsync(
                timeout, SecuredRead.LoginControl, cancellationToken).ConfigureAwait(false);

            if (frame is null)
            {
                throw new EndOfStreamException("GameServer closed without a login control outcome.");
            }

            GameLoginControlOutcome outcome = GameLoginControl1004.Read(frame.Packet.Span);

            lock (_lifetimeGate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (_phase != 2)
                {
                    throw new InvalidOperationException("GameServer session left its secured read phase before resolving login control.");
                }

                if (_creationState == CreationState.ProofRepeated &&
                    outcome.Kind != GameLoginControlKind.ExistingCharacter)
                {
                    throw new InvalidDataException("GameServer did not continue the created character after the repeated proof.");
                }

                _loginControlState = 2;
                _existingCharacterRoute = outcome.Kind == GameLoginControlKind.ExistingCharacter;
                if (outcome.Kind == GameLoginControlKind.CharacterCreation)
                {
                    _creationState = CreationState.AwaitingRequest;
                }
                else
                {
                    ClearRepeatedProofUnderLock();
                }

                if (outcome.Kind == GameLoginControlKind.Refused)
                {
                    // Publish terminal state before releasing the gate: no raw
                    // reader may race a refusal into another secured read.
                    _phase = 4;
                }
            }

            if (outcome.Kind == GameLoginControlKind.Refused)
            {
                _client.Dispose();
            }

            return outcome;
        }
        catch
        {
            TerminateLoginControlSession();
            throw;
        }
    }

    /// <summary>
    /// Reads the current companion server's existing-character account-history
    /// and server-state prelude as one exclusive secured-read operation.
    /// This method is not a world-admission or character-ready transition.
    /// </summary>
    internal async Task<GameExistingCharacterPrelude> ReadExistingCharacterPreludeAsync(
        TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ValidateTimeout(timeout);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lifetimeGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_phase != 2 || !_existingCharacterRoute || _loginControlState != 2 ||
                _existingCharacterPreludeState != 0)
            {
                throw new InvalidOperationException(
                    "Existing-character prelude requires an accepted control and uninterrupted first bootstrap reads.");
            }

            _existingCharacterPreludeState = 1;
        }

        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _lifetimeCancellation.Token);
        deadline.CancelAfter(timeout);

        try
        {
            using GameSecuredInboundFrame? historyFrame = await ReadFrameCoreAsync(
                timeout, SecuredRead.Prelude, deadline.Token).ConfigureAwait(false);
            if (historyFrame is null)
            {
                throw new EndOfStreamException("GameServer closed before account login history.");
            }

            GameLoginHistory2078 history = GameLoginHistory2078.Read(historyFrame.Packet.Span);

            using GameSecuredInboundFrame? stateFrame = await ReadFrameCoreAsync(
                timeout, SecuredRead.Prelude, deadline.Token).ConfigureAwait(false);
            if (stateFrame is null)
            {
                throw new EndOfStreamException("GameServer closed before the server state packet.");
            }

            GameServerState2079 state = GameServerState2079.Read(stateFrame.Packet.Span);
            deadline.Token.ThrowIfCancellationRequested();

            lock (_lifetimeGate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (_phase != 2 || _existingCharacterPreludeState != 1)
                {
                    throw new InvalidOperationException(
                        "The existing-character prelude lost exclusive secured-read ownership.");
                }

                _existingCharacterPreludeState = 2;
            }

            return new GameExistingCharacterPrelude(history, state);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested &&
            !_lifetimeCancellation.IsCancellationRequested)
        {
            TerminateLoginControlSession();
            throw new TimeoutException("GameServer existing-character prelude timed out.");
        }
        catch
        {
            TerminateLoginControlSession();
            throw;
        }
    }

    /// <summary>
    /// Consumes the companion server's next secured 1006 as a bounded, owned
    /// character-information snapshot, after the accepted control and its
    /// 2078/2079 prelude in this bootstrap sequence.
    /// This does not hydrate a hero, send EnterMap or establish world admission.
    /// </summary>
    internal async Task<GameCharacterInfo1006> ReadCharacterInfoAsync(
        TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ValidateTimeout(timeout);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lifetimeGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_phase != 2 || !_existingCharacterRoute || _loginControlState != 2 ||
                _existingCharacterPreludeState != 2 || _characterInfoState != 0)
            {
                throw new InvalidOperationException(
                    "The next character-information frame can only be claimed once after the completed existing-character prelude.");
            }

            _characterInfoState = 1;
        }

        try
        {
            using GameSecuredInboundFrame? frame = await ReadFrameCoreAsync(
                timeout, SecuredRead.CharacterInfo, cancellationToken).ConfigureAwait(false);
            if (frame is null)
            {
                throw new EndOfStreamException("GameServer closed before character information.");
            }

            GameCharacterInfo1006 character = GameCharacterInfo1006.Read(frame.Packet.Span);
            cancellationToken.ThrowIfCancellationRequested();

            lock (_lifetimeGate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (_phase != 2 || _characterInfoState != 1)
                {
                    throw new InvalidOperationException(
                        "GameServer character information lost exclusive secured-read ownership.");
                }

                _characterInfoState = 2;
            }

            return character;
        }
        catch
        {
            TerminateLoginControlSession();
            throw;
        }
    }

    /// <summary>
    /// Sends one 1001 create request after a <c>NEW_ROLE</c> control, or after a refused request. Its result must
    /// be read with <see cref="ReadCharacterCreationResultAsync"/> before anything else is sent or read.
    /// </summary>
    internal async Task SendCharacterCreationAsync(ReadOnlyMemory<byte> name, ushort body, ushort profession,
        TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ValidateTimeout(timeout);
        GameCharacterCreation1001.ValidateName(name.Span);
        cancellationToken.ThrowIfCancellationRequested();
        EnterCreationWrite(CreationState.AwaitingRequest);

        bool completed = false;
        byte[] packet = new byte[GameCharacterCreation1001.PacketLength];
        try
        {
            GameCharacterCreation1001.WriteCreate(packet, name.Span, body, profession, _sessionToken);
            await WriteSecuredPacketAsync(packet, timeout, "GameServer 1001 request write timed out.", cancellationToken)
                .ConfigureAwait(false);
            lock (_lifetimeGate)
            {
                _creationState = CreationState.AwaitingResult;
            }

            completed = true;
        }
        catch
        {
            TerminateLoginControlSession();
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(packet);
            ExitOperation(completed);
        }
    }

    /// <summary>
    /// Reads the create-role channel answer to the last 1001. A refusal reopens the request step on the same
    /// socket; <c>ANSWER_OK</c> allows exactly one <see cref="SendRepeatedLoginProofAsync"/>. Any other frame,
    /// a close or a timeout is terminal.
    /// </summary>
    internal async Task<GameCharacterCreationOutcome> ReadCharacterCreationResultAsync(
        TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ValidateTimeout(timeout);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lifetimeGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_phase != 2 || _creationState != CreationState.AwaitingResult)
            {
                throw new InvalidOperationException("No 1001 request is waiting for its result.");
            }
        }

        try
        {
            using GameSecuredInboundFrame? frame = await ReadFrameCoreAsync(
                timeout, SecuredRead.CreationResult, cancellationToken).ConfigureAwait(false);
            if (frame is null)
            {
                throw new EndOfStreamException("GameServer closed without a character creation result.");
            }

            GameCharacterCreationOutcome outcome = GameCharacterCreationResult1004.Read(frame.Packet.Span);
            lock (_lifetimeGate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (_phase != 2 || _creationState != CreationState.AwaitingResult)
                {
                    throw new InvalidOperationException("The character creation result lost its secured-read ownership.");
                }

                _creationState = outcome.Kind == GameCharacterCreationKind.Created
                    ? CreationState.Accepted
                    : CreationState.AwaitingRequest;
            }

            return outcome;
        }
        catch
        {
            TerminateLoginControlSession();
            throw;
        }
    }

    /// <summary>
    /// Sends the create-role cancellation command and closes the socket.
    /// </summary>
    internal async Task SendCharacterCreationCancelAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ValidateTimeout(timeout);
        cancellationToken.ThrowIfCancellationRequested();
        EnterCreationWrite(CreationState.AwaitingRequest);

        byte[] packet = new byte[GameCharacterCreation1001.PacketLength];
        try
        {
            GameCharacterCreation1001.WriteCancel(packet, _sessionToken);
            await WriteSecuredPacketAsync(packet, timeout, "GameServer 1001 cancel write timed out.", cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(packet);
            lock (_lifetimeGate)
            {
                _creationState = CreationState.Cancelled;
            }

            TerminateLoginControlSession();
            ExitOperation(succeeded: false);
        }
    }

    /// <summary>
    /// Re-sends the 1052 login proof once after <c>ANSWER_OK</c>, on the same socket.
    /// The next login control must accept the
    /// created character, after which the existing-character prelude and 1006 reads apply.
    /// </summary>
    internal async Task SendRepeatedLoginProofAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ValidateTimeout(timeout);
        cancellationToken.ThrowIfCancellationRequested();
        EnterCreationWrite(CreationState.Accepted);

        bool completed = false;
        try
        {
            byte[] proof;
            lock (_lifetimeGate)
            {
                proof = _repeatedProof ?? throw new InvalidOperationException("The initial GameServer proof is unavailable.");
            }

            await WriteSecuredPacketAsync(proof, timeout, "GameServer repeated login proof write timed out.",
                cancellationToken).ConfigureAwait(false);
            lock (_lifetimeGate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                _creationState = CreationState.ProofRepeated;
                _loginControlState = 0;
                ClearRepeatedProofUnderLock();
            }

            completed = true;
        }
        catch
        {
            TerminateLoginControlSession();
            throw;
        }
        finally
        {
            ExitOperation(completed);
        }
    }

    /// <summary>Sends one fixed world-entry 10010 action after typed character information is accepted.</summary>
    internal async Task SendWorldActionAsync(ushort action, uint entityId, uint parameterPair,
        TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ValidateTimeout(timeout);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lifetimeGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_phase != 2 || !_existingCharacterRoute || _characterInfoState != 2)
            {
                throw new InvalidOperationException("World-entry actions require a completed character-info read.");
            }

            _phase = 1;
            _activeOperations++;
        }

        byte[] packet = new byte[GameWorldAction10010.PacketLength];
        bool completed = false;
        try
        {
            GameWorldAction10010.Write(packet, action, entityId, parameterPair);
            await WriteSecuredPacketAsync(packet, timeout, "GameServer world-entry action write timed out.",
                cancellationToken).ConfigureAwait(false);
            completed = true;
        }
        catch
        {
            TerminateLoginControlSession();
            throw;
        }
        finally
        {
            ExitOperation(completed);
        }
    }

    private void EnterCreationWrite(CreationState required)
    {
        lock (_lifetimeGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_phase != 2 || _creationState != required)
            {
                throw new InvalidOperationException("The GameServer create-role exchange is not at this step.");
            }

            _phase = 1;
            _activeOperations++;
        }
    }

    /// <summary>Encodes and writes one secured frame. Any attempted write failure leaves the socket unusable.</summary>
    private async Task WriteSecuredPacketAsync(byte[] packet, TimeSpan timeout, string timeoutMessage,
        CancellationToken cancellationToken)
    {
        byte[] wire = ArrayPool<byte>.Shared.Rent(packet.Length + GameSecuredFrameProtocol.SignatureLength);
        try
        {
            using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _lifetimeCancellation.Token);
            deadline.CancelAfter(timeout);
            deadline.Token.ThrowIfCancellationRequested();

            int written = _encoder.Write(packet, wire);
            try
            {
                await _client.GetStream().WriteAsync(wire.AsMemory(0, written), deadline.Token).ConfigureAwait(false);
                deadline.Token.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested &&
                !_lifetimeCancellation.IsCancellationRequested)
            {
                throw new TimeoutException(timeoutMessage);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(wire);
            ArrayPool<byte>.Shared.Return(wire);
        }
    }

    private void TerminateLoginControlSession()
    {
        lock (_lifetimeGate)
        {
            _phase = 4;
            ClearRepeatedProofUnderLock();
        }

        _client.Dispose();
    }

    private void ClearRepeatedProofUnderLock()
    {
        if (_repeatedProof is { } proof)
        {
            _repeatedProof = null;
            CryptographicOperations.ZeroMemory(proof);
        }
    }

    private async Task<GameSecuredInboundFrame?> ReadFrameCoreAsync(
        TimeSpan timeout, SecuredRead read, CancellationToken cancellationToken)
    {
        ValidateTimeout(timeout);
        cancellationToken.ThrowIfCancellationRequested();
        EnterReadOperation(read);

        bool completed = false;
        byte[] encrypted = ArrayPool<byte>.Shared.Rent(GameSecuredFrameProtocol.MaximumWireLength);
        try
        {
            using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _lifetimeCancellation.Token);
            deadline.CancelAfter(timeout);
            NetworkStream stream = _client.GetStream();
            bool frameStarted = false;

            while (true)
            {
                int needed = _decoder.BytesNeeded;
                int received;
                try
                {
                    received = await stream.ReadAsync(encrypted.AsMemory(0, needed), deadline.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested &&
                    !_lifetimeCancellation.IsCancellationRequested)
                {
                    throw new TimeoutException("GameServer secured frame read timed out.");
                }

                if (received == 0)
                {
                    if (frameStarted)
                    {
                        throw new EndOfStreamException("GameServer closed during a secured frame.");
                    }

                    return null;
                }

                frameStarted = true;
                GameSecuredFrameDecodeResult result = _decoder.Decode(encrypted.AsSpan(0, received));
                if (result.BytesConsumed != received)
                {
                    result.Frame?.Dispose();
                    throw new InvalidDataException("GameServer secured decoder consumed unexpected bytes.");
                }

                switch (result.Status)
                {
                    case GameSecuredFrameDecodeStatus.NeedMoreData:
                        continue;

                    case GameSecuredFrameDecodeStatus.Success:
                        GameSecuredInboundFrame frame = result.Frame ??
                            throw new InvalidDataException("GameServer decoder did not return its completed frame.");
                        try
                        {
                            deadline.Token.ThrowIfCancellationRequested();
                        }
                        catch
                        {
                            frame.Dispose();
                            throw;
                        }

                        completed = true;
                        return frame;

                    case GameSecuredFrameDecodeStatus.InvalidPacketLength:
                    case GameSecuredFrameDecodeStatus.InvalidSignature:
                        throw new InvalidDataException("GameServer secured frame failed validation.");

                    default:
                        throw new InvalidDataException("GameServer decoder returned an unknown status.");
                }
            }
        }
        catch
        {
            _client.Dispose();
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encrypted);
            ArrayPool<byte>.Shared.Return(encrypted);
            if (!completed)
            {
                _client.Dispose();
            }

            ExitOperation(completed);
        }
    }

    private static void ValidateTimeout(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromSeconds(30))
        {
            throw new ArgumentOutOfRangeException(nameof(timeout),
                "GameServer secured I/O timeout must be positive and at most 30 seconds.");
        }
    }

    private void EnterReadOperation(SecuredRead read)
    {
        bool preludeRead = read == SecuredRead.Prelude;
        bool characterInfoRead = read == SecuredRead.CharacterInfo;
        lock (_lifetimeGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_phase != 2)
            {
                throw new InvalidOperationException("GameServer secured operation is not valid in this phase.");
            }

            // A creation result needs a sent request; while the create-role exchange is open no other read may
            // consume its frames.
            bool creationOpen = _creationState is CreationState.AwaitingRequest or CreationState.AwaitingResult or
                CreationState.Accepted;
            if (read == SecuredRead.CreationResult ? _creationState != CreationState.AwaitingResult : creationOpen)
            {
                throw new InvalidOperationException("The GameServer create-role exchange owns the secured reads.");
            }

            if (read == SecuredRead.LoginControl)
            {
                if (_loginControlState != 1)
                {
                    throw new InvalidOperationException("The GameServer login control was not claimed.");
                }
            }
            else if (read != SecuredRead.CreationResult)
            {
                if (_loginControlState == 1)
                {
                    throw new InvalidOperationException("A GameServer login control read is in progress.");
                }

                if (_loginControlState == 0)
                {
                    _loginControlState = 3;
                }

                if (preludeRead)
                {
                    if (!_existingCharacterRoute || _existingCharacterPreludeState != 1)
                    {
                        throw new InvalidOperationException("Existing-character prelude is not claimed.");
                    }
                }
                else if (characterInfoRead)
                {
                    if (!_existingCharacterRoute || _existingCharacterPreludeState != 2 ||
                        _characterInfoState != 1)
                    {
                        throw new InvalidOperationException("Character-information read is not claimed.");
                    }
                }
                else
                {
                    if (_existingCharacterPreludeState == 1 || _characterInfoState == 1)
                    {
                        throw new InvalidOperationException(
                            "A typed GameServer bootstrap read is in progress.");
                    }

                    if (_existingCharacterRoute && _existingCharacterPreludeState == 0)
                    {
                        _existingCharacterPreludeState = 3;
                    }

                    if (_existingCharacterRoute && _existingCharacterPreludeState == 2 &&
                        _characterInfoState == 0)
                    {
                        // A raw consumer may inspect 1006, but it cannot later
                        // relabel that frame as the one-shot typed result.
                        _characterInfoState = 3;
                    }
                }
            }

            _phase = 3;
            _activeOperations++;
        }
    }

    private void EnterOperation(int expectedPhase, int activePhase)
    {
        lock (_lifetimeGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_phase != expectedPhase)
            {
                throw new InvalidOperationException("GameServer secured operation is not valid in this phase.");
            }

            _phase = activePhase;
            _activeOperations++;
        }
    }

    private void ExitOperation(bool succeeded)
    {
        TaskCompletionSource? drained = null;
        lock (_lifetimeGate)
        {
            _phase = succeeded && !_disposed ? 2 : 4;
            _activeOperations--;
            if (_activeOperations == 0)
            {
                drained = _operationsDrained;
            }
        }

        drained?.TrySetResult();
    }

    public ValueTask DisposeAsync()
    {
        lock (_lifetimeGate)
        {
            if (_disposeTask is not null)
            {
                return new ValueTask(_disposeTask);
            }

            _disposed = true;
            _phase = 4;
            Task drained = _activeOperations == 0 ? Task.CompletedTask :
                (_operationsDrained ??= new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously)).Task;

            _disposeTask = DisposeCoreAsync(drained);
            return new ValueTask(_disposeTask);
        }
    }

    private async Task DisposeCoreAsync(Task operationsDrained)
    {
        _client.Dispose();
        await _lifetimeCancellation.CancelAsync().ConfigureAwait(false);
        await operationsDrained.ConfigureAwait(false);
        lock (_lifetimeGate)
        {
            ClearRepeatedProofUnderLock();
            _account = null;
        }

        _decoder.Dispose();
        _encoder.Dispose();
        _cipher.Dispose();
        _lifetimeCancellation.Dispose();
    }

    private enum SecuredRead
    {
        Raw,
        LoginControl,
        Prelude,
        CharacterInfo,
        CreationResult,
    }

    private enum CreationState
    {
        None,
        AwaitingRequest,
        AwaitingResult,
        Accepted,
        ProofRepeated,
        Cancelled,
    }
}
