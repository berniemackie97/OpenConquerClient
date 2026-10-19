using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using OpenConquer.Networking.Authentication;
using OpenConquer.Networking.Game.Cryptography;

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
    private bool _disposed;

    private GameServerHandshakeSession(TcpClient client, GameSessionCipher cipher)
    {
        _client = client;
        _cipher = cipher;
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
                throw new InvalidOperationException("GameServer session cipher was not negotiated."));
        }
        catch
        {
            sessionCipher?.Dispose();
            client.Dispose();
            throw;
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_lifetimeGate)
        {
            if (_disposed)
            {
                return ValueTask.CompletedTask;
            }

            _disposed = true;
            // Closing TCP first interrupts any future I/O owner.
            _client.Dispose();
            _cipher.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
