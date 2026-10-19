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
internal sealed class GameServerHandshakeSession : IAsyncDisposable
{
    private readonly TcpClient _client;
    private readonly GameSessionCipher _cipher;
    private readonly Lock _lifetimeGate = new();
    private readonly LoginAccountExchangeResult _account;
    private readonly GameSecuredFrameEncoder _encoder;
    private readonly GameSecuredFrameDecoder _decoder;
    private readonly CancellationTokenSource _lifetimeCancellation = new();

    private TaskCompletionSource? _operationsDrained;
    private Task? _disposeTask;
    private int _activeOperations;
    private int _phase; // 0: handshake complete, 1: proof sending, 2: ready, 3: reading, 4: terminal
    private int _loginControlState; // 0: unclaimed, 1: reading, 2: resolved, 3: raw-read bypassed
    private bool _disposed;

    private GameServerHandshakeSession(TcpClient client, GameSessionCipher cipher,
        LoginAccountExchangeResult account)
    {
        _client = client;
        _cipher = cipher;
        _account = account;
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
                    throw new TimeoutException("GameServer loopback connection timed out.");
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
        byte[] wire = new byte[GameLoginProof1052.PacketLength +
            GameSecuredFrameProtocol.SignatureLength];
        try
        {
            using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _lifetimeCancellation.Token);
            deadline.CancelAfter(timeout);
            deadline.Token.ThrowIfCancellationRequested();

            _account.WriteGameLoginProof(packet, localeTag, hardwareAddress.Span);
            int written = _encoder.Write(packet, wire);
            try
            {
                await _client.GetStream().WriteAsync(wire.AsMemory(0, written), deadline.Token)
                    .ConfigureAwait(false);
                deadline.Token.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested &&
                !_lifetimeCancellation.IsCancellationRequested)
            {
                throw new TimeoutException("GameServer login proof write timed out.");
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
            CryptographicOperations.ZeroMemory(wire);
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
        ReadFrameCoreAsync(timeout, loginControlRead: false, cancellationToken);

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
                throw new InvalidOperationException("The first GameServer login control can only be read once, before any raw secured reads.");
            }

            _loginControlState = 1;
        }

        try
        {
            using GameSecuredInboundFrame? frame = await ReadFrameCoreAsync(
                timeout, loginControlRead: true, cancellationToken).ConfigureAwait(false);

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

                _loginControlState = 2;
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

    private void TerminateLoginControlSession()
    {
        lock (_lifetimeGate)
        {
            _phase = 4;
        }

        _client.Dispose();
    }

    private async Task<GameSecuredInboundFrame?> ReadFrameCoreAsync(
        TimeSpan timeout, bool loginControlRead, CancellationToken cancellationToken)
    {
        ValidateTimeout(timeout);
        cancellationToken.ThrowIfCancellationRequested();
        EnterReadOperation(loginControlRead);

        bool completed = false;
        byte[] encrypted = new byte[GameSecuredFrameProtocol.MaximumWireLength];
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

    private void EnterReadOperation(bool loginControlRead)
    {
        lock (_lifetimeGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_phase != 2)
            {
                throw new InvalidOperationException("GameServer secured operation is not valid in this phase.");
            }

            if (loginControlRead)
            {
                if (_loginControlState != 1)
                {
                    throw new InvalidOperationException("The first GameServer login control was not claimed.");
                }
            }
            else
            {
                if (_loginControlState == 1)
                {
                    throw new InvalidOperationException("A GameServer login control read is in progress.");
                }

                if (_loginControlState == 0)
                {
                    _loginControlState = 3;
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
        _decoder.Dispose();
        _encoder.Dispose();
        _cipher.Dispose();
        _lifetimeCancellation.Dispose();
    }
}
