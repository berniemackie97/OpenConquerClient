using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using OpenConquer.Networking.Authentication;
using OpenConquer.Networking.Game.Cryptography;
using OpenConquer.Networking.Game.Framing;
using OpenConquer.Networking.Game.Login;
using OpenConquer.Networking.Game.World;

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
    private int[]? _missionIds;
    private bool _missionCatalogSent;
    private readonly GameSecuredFrameEncoder _encoder;
    private readonly GameSecuredFrameDecoder _decoder;
    private readonly CancellationTokenSource _lifetimeCancellation = new();

    private TaskCompletionSource? _operationsDrained;
    private Task? _disposeTask;
    private int _activeOperations;
    private uint _localAddress;
    private bool _worldWriteActive;
    private int _phase; // 0: handshake complete, 1: sending, 2: bootstrap ready, 3: bootstrap read, 4: terminal, 5: world ready, 6: world read
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
        using CancellationTokenSource proofDeadline = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _lifetimeCancellation.Token);
        proofDeadline.CancelAfter(timeout);
        EnterOperation(expectedPhase: 0, activePhase: 1);

        bool completed = false;
        byte[] packet = new byte[GameLoginProof1052.PacketLength];
        try
        {
            proofDeadline.Token.ThrowIfCancellationRequested();
            (_account ?? throw new InvalidOperationException("Account proof state was already released."))
                .WriteGameLoginProof(packet, localeTag, hardwareAddress.Span);
            proofDeadline.Token.ThrowIfCancellationRequested();
            await WriteSecuredPacketAsync(packet, timeout, "GameServer login proof write timed out.",
                proofDeadline.Token).ConfigureAwait(false);

            lock (_lifetimeGate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                _repeatedProof = (byte[])packet.Clone();
                _account = null;
            }

            completed = true;
        }
        catch (OperationCanceledException) when (
            proofDeadline.IsCancellationRequested &&
            !cancellationToken.IsCancellationRequested &&
            !_lifetimeCancellation.IsCancellationRequested)
        {
            throw new TimeoutException("GameServer login proof write timed out.");
        }
        finally
        {
            if (!completed)
            {
                _client.Dispose();
            }

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

    internal void ActivateWorld()
    {
        lock (_lifetimeGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_phase != 2 || !_existingCharacterRoute || _characterInfoState != 2)
            {
                throw new InvalidOperationException("The secured world reader requires completed character admission.");
            }

            _phase = 5;
        }
    }

    internal Task<GameSecuredInboundFrame?> ReadWorldFrameAsync(CancellationToken cancellationToken = default) =>
        ReadFrameCoreAsync(null, SecuredRead.World, cancellationToken);

    internal Task SendWorldWalkAsync(uint entityId, byte directionIndex,
        uint movementTick, uint mapState, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(entityId);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(directionIndex, (byte)7);
        byte[] packet = new byte[GameWalk10005.PacketLength];
        GameWalk10005.WriteOrdinary(packet, entityId, directionIndex, movementTick, mapState);
        return SendWorldPacketAsync(packet, "GameServer world walk write timed out.", cancellationToken);
    }

    internal Task SendWorldActionResponseAsync(GameWorldActionResponse response, uint now,
        CancellationToken cancellationToken = default) =>
        SendWorldPacketAsync(CreateWorldActionPacket(response.Action, response.EntityId,
            response.ParameterPair, response.TimestampOverride ?? now),
            "GameServer admitted world action write timed out.", cancellationToken);

    private static byte[] CreateWorldActionPacket(ushort action, uint entityId, uint parameterPair, uint timestamp)
    {
        byte[] packet = new byte[GameWorldAction10010.PacketLength];
        GameWorldAction10010.Write(packet, action, entityId, parameterPair, timestamp);
        return packet;
    }

    internal Task SendWorldCorrectionReplyAsync(uint heroId, ushort x, ushort y, byte direction,
        uint mapId, uint timestamp, CancellationToken cancellationToken = default)
    {
        byte[] packet = new byte[GameWorldAction10010.PacketLength];
        GameWorldAction10010.WriteCorrectionReply(packet, heroId, x, y, direction, mapId, timestamp);
        return SendWorldPacketAsync(packet, "GameServer correction reply write timed out.", cancellationToken);
    }

    /// <summary>Sends the 1009 subtype-27 heartbeat (<see cref="GameLatencyTick1009"/>).</summary>
    internal Task SendWorldLatencyTickAsync(uint heroId, uint milliseconds, CancellationToken cancellationToken = default)
    {
        byte[] packet = new byte[GameItem1009.FixedLength];
        GameLatencyTick1009.Write(packet, heroId, milliseconds);
        return SendWorldPacketAsync(packet, "GameServer world heartbeat write timed out.", cancellationToken);
    }

    /// <summary>Sends the 1037 latency report (<see cref="GameLatencyReport1037"/>).</summary>
    internal Task SendWorldLatencyReportAsync(uint localAddress, uint latency, uint milliseconds,
        CancellationToken cancellationToken = default)
    {
        byte[] packet = new byte[GameLatencyReport1037.PacketLength];
        GameLatencyReport1037.Write(packet, localAddress, latency, milliseconds);
        return SendWorldPacketAsync(packet, "GameServer world latency report write timed out.", cancellationToken);
    }

    /// <summary>
    /// The local IPv4 address (raw in_addr dword) the native client resolves when it connects to the account server
    /// (<c>sub_66df97</c>, called at 0x4AF266) and reports in 1037. Zero when it could not be resolved.
    /// </summary>
    internal uint LocalAddress
    {
        get => Volatile.Read(ref _localAddress);
        set => Volatile.Write(ref _localAddress, value);
    }

    /// <summary>The silent-info values the 251 world-entry report carries.</summary>
    internal GameSilentInfoReport SilentInfo
    {
        get;
        set;
    } = GameSilentInfoReport.WithoutFiles;

    private async Task SendWorldPacketAsync(byte[] packet, string timeoutMessage, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lifetimeGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_phase is not (5 or 6) || _worldWriteActive)
            {
                throw new InvalidOperationException("World writes require an admitted session and an idle outbound lane.");
            }

            _worldWriteActive = true;
            _activeOperations++;
        }

        bool completed = false;
        try
        {
            await WriteSecuredPacketAsync(packet, TimeSpan.FromSeconds(10), timeoutMessage, cancellationToken)
                .ConfigureAwait(false);
            completed = true;
        }
        catch
        {
            TerminateLoginControlSession();
            throw;
        }
        finally
        {
            TaskCompletionSource? drained = null;
            lock (_lifetimeGate)
            {
                _worldWriteActive = false;
                if (!completed)
                {
                    _phase = 4;
                }

                _activeOperations--;
                if (_activeOperations == 0)
                {
                    drained = _operationsDrained;
                }
            }

            drained?.TrySetResult();
        }
    }

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

    internal void SetMissionCatalog(ReadOnlyMemory<int> ids)
    {
        if (ids.IsEmpty || ids.Length > GameMissionCatalog1134.MaximumMissionsPerFrame * 3)
        {
            throw new InvalidDataException("The mission catalog contains an unsupported number of IDs.");
        }

        int previous = 0;
        foreach (int id in ids.Span)
        {
            if (id <= previous)
            {
                throw new InvalidDataException("Mission catalog IDs must be positive and ascending.");
            }

            previous = id;
        }

        lock (_lifetimeGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_phase != 2 || _loginControlState != 2 || _missionIds is not null)
            {
                throw new InvalidOperationException("The mission catalog can only be assigned once after login control.");
            }

            _missionIds = ids.ToArray();
        }
    }

    internal async Task SendMissionCatalogAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        ValidateTimeout(timeout);
        cancellationToken.ThrowIfCancellationRequested();

        int[] ids;
        lock (_lifetimeGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_phase != 2 || _loginControlState != 2 || !_existingCharacterRoute ||
                _missionCatalogSent || _missionIds is null)
            {
                throw new InvalidOperationException("Mission catalog emission requires accepted character login control.");
            }

            ids = _missionIds;
            _missionCatalogSent = true;
            _phase = 1;
            _activeOperations++;
        }

        bool completed = false;
        try
        {
            for (int offset = 0; offset < ids.Length; offset += GameMissionCatalog1134.MaximumMissionsPerFrame)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int count = Math.Min(GameMissionCatalog1134.MaximumMissionsPerFrame, ids.Length - offset);
                byte[] frame = new byte[GameMissionCatalog1134.GetPacketLength(count)];
                GameMissionCatalog1134.Write(frame, ids.AsSpan(offset, count));
                await WriteSecuredPacketAsync(frame, timeout, "GameServer mission catalog write timed out.",
                    cancellationToken).ConfigureAwait(false);
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
    internal Task SendWorldActionAsync(ushort action, uint entityId, uint parameterPair,
        TimeSpan timeout, CancellationToken cancellationToken = default) =>
        SendWorldActionAsync(action, entityId, parameterPair, 0, timeout, cancellationToken);

    /// <summary>Sends one world-entry 10010 action carrying a client timestamp at +16.</summary>
    internal Task SendWorldActionAsync(ushort action, uint entityId, uint parameterPair, uint timestamp,
        TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        return SendWorldEntryPacketAsync(CreateWorldActionPacket(action, entityId, parameterPair, timestamp),
            timeout, cancellationToken);
    }

    /// <summary>Sends the 1151 request native emits right after EnterMap for body digit 2.</summary>
    internal Task SendWorldEntryFlowerRankAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        byte[] packet = new byte[GameFlowerRankRequest1151.PacketLength];
        GameFlowerRankRequest1151.Write(packet, GameFlowerRankRequest1151.EnterMapSubtype);
        return SendWorldEntryPacketAsync(packet, timeout, cancellationToken);
    }

    private async Task SendWorldEntryPacketAsync(byte[] packet, TimeSpan timeout, CancellationToken cancellationToken)
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

        bool completed = false;
        try
        {
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
        TimeSpan? timeout, SecuredRead read, CancellationToken cancellationToken)
    {
        if (timeout is TimeSpan duration)
        {
            ValidateTimeout(duration);
        }
        cancellationToken.ThrowIfCancellationRequested();
        EnterReadOperation(read);

        bool completed = false;
        byte[] encrypted = ArrayPool<byte>.Shared.Rent(GameSecuredFrameProtocol.MaximumWireLength);
        try
        {
            using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _lifetimeCancellation.Token);
            if (timeout is TimeSpan readTimeout)
            {
                deadline.CancelAfter(readTimeout);
            }

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

            ExitReadOperation(read, completed);
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
        if (read == SecuredRead.World)
        {
            lock (_lifetimeGate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (_phase != 5)
                {
                    throw new InvalidOperationException("Only one secured world read may be active after admission.");
                }

                _phase = 6;
                _activeOperations++;
                return;
            }
        }

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

    private void ExitReadOperation(SecuredRead read, bool completed)
    {
        if (read != SecuredRead.World)
        {
            ExitOperation(completed);
            return;
        }

        TaskCompletionSource? drained = null;
        lock (_lifetimeGate)
        {
            _phase = completed && !_disposed && _phase != 4 ? 5 : 4;
            _activeOperations--;
            if (_activeOperations == 0)
            {
                drained = _operationsDrained;
            }
        }

        drained?.TrySetResult();
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
        List<Exception> failures = [];
        DisposePart(_client.Dispose);
        try
        {
            await _lifetimeCancellation.CancelAsync().ConfigureAwait(false);
        }
        catch (Exception failure)
        {
            failures.Add(failure);
        }

        await operationsDrained.ConfigureAwait(false);
        lock (_lifetimeGate)
        {
            ClearRepeatedProofUnderLock();
            _account = null;
            _missionIds = null;
        }

        DisposePart(_decoder.Dispose);
        DisposePart(_encoder.Dispose);
        DisposePart(_cipher.Dispose);
        DisposePart(_lifetimeCancellation.Dispose);

        if (failures.Count == 1)
        {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }

        if (failures.Count > 1)
        {
            throw new AggregateException("GameServer session cleanup failed.", failures).Flatten();
        }

        void DisposePart(Action dispose)
        {
            try
            {
                dispose();
            }
            catch (Exception failure)
            {
                failures.Add(failure);
            }
        }
    }

    private enum SecuredRead
    {
        Raw,
        LoginControl,
        Prelude,
        CharacterInfo,
        CreationResult,
        World,
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
