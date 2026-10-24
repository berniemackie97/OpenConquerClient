using System.Buffers;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace OpenConquer.Networking.Authentication;

/// <summary>
/// One connected AccountServer socket and the cipher position for each direction.
/// Opening reads one verified 1059 seed frame. No credentials are sent.
/// </summary>
internal sealed class LoginAccountSession : IAsyncDisposable
{
    private readonly TcpClient _client;
    private readonly LoginLegacyStreamCipher _cipher = new();
    private int _disposed;
    private int _authenticationStarted;
    private int _postAuthenticationStarted;
    private uint _successfulSessionToken;

    private LoginAccountSession(TcpClient client)
    {
        _client = client;
    }

    internal uint Seed
    {
        get; private set;
    }

    /// <summary>
    /// Low-level transport operation; the caller MUST obtain the host/port from the current
    /// authenticated realm directory. UI consumers use LoginSignedAccountSession.
    /// </summary>
    internal static async Task<LoginAccountSession> OpenAsync(
        string host, int port, LoginAccountTimeouts timeouts, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        if (host.Length > 253 || host != host.Trim() ||
            host.Any(static character => character > 127 || char.IsWhiteSpace(character)) ||
            Uri.CheckHostName(host) is not (UriHostNameType.Dns or UriHostNameType.IPv4))
        {
            throw new ArgumentException("Invalid AccountServer host.", nameof(host));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(port, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);
        timeouts.Validate();
        cancellationToken.ThrowIfCancellationRequested();

        // Native AccountServer ingress is IPv4. There is no DH negotiation or trailer
        // in this legacy mode and the initial packet is server-to-client.
        TcpClient client = new(System.Net.Sockets.AddressFamily.InterNetwork)
        {
            NoDelay = true
        };
        try
        {
            using (CancellationTokenSource connectDeadline =
                   CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                connectDeadline.CancelAfter(timeouts.Connect);
                try
                {
                    await client.ConnectAsync(host, port, connectDeadline.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new LoginConnectFailureException(LoginConnectTarget.Account, true,
                        new TimeoutException("AccountServer connection timed out."));
                }
                catch (SocketException exception)
                {
                    throw new LoginConnectFailureException(LoginConnectTarget.Account, false, exception);
                }
            }

            LoginAccountSession session = new(client);
            using (CancellationTokenSource seedDeadline =
                   CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                seedDeadline.CancelAfter(timeouts.Seed);
                try
                {
                    session.Seed = await session.ReadSeedAsync(seedDeadline.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new TimeoutException("AccountServer seed exchange timed out.");
                }
            }

            return session;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }


    /// <summary>
    /// Completes at most one credential exchange after the 1059 seed. Successful responses keep
    /// this session open for the native post-authentication continuation; failures close it.
    /// Live credential submission is restricted to IPv4 loopback until the remote-peer trust
    /// boundary is implemented independently of the native unauthenticated stream cipher.
    /// </summary>
    internal async Task<LoginAccountResponse1055> AuthenticateAsync(
        ReadOnlyMemory<byte> accountName, ReadOnlyMemory<char> password,
        ReadOnlyMemory<byte> protocolServerName, Func<bool> isAuthorized,
        TimeSpan responseTimeout, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(isAuthorized);
        if (responseTimeout <= TimeSpan.Zero || responseTimeout > TimeSpan.FromSeconds(30))
        {
            throw new ArgumentOutOfRangeException(nameof(responseTimeout));
        }

        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (Interlocked.CompareExchange(ref _authenticationStarted, 1, 0) != 0)
        {
            throw new InvalidOperationException("The AccountServer session already has an authentication attempt.");
        }

        byte[] request = new byte[StandardAccountRequest1060.PacketLength];
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            StandardAccountCredentialComposer.Write(request, accountName.Span, password.Span,
                protocolServerName.Span, Seed);

            // No credential may leave this client for a remote peer over legacy stream obfuscation.
            if (_client.Client.RemoteEndPoint is not IPEndPoint peer ||
                !IPAddress.IsLoopback(peer.Address))
            {
                throw new InvalidOperationException("Remote AccountServer credentials require authenticated transport.");
            }

            using CancellationTokenSource deadline =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(responseTimeout);

            // The authority callback is evaluated AFTER composing and immediately before the
            // first potential write. The UI is not wired to this operation in AUTH-001D.
            if (!isAuthorized())
            {
                throw new InvalidOperationException("The authenticated realm selection changed before credential submission.");
            }

            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
            _cipher.EncryptOutbound(request);
            await _client.GetStream().WriteAsync(request, deadline.Token).ConfigureAwait(false);

            byte[] frame = new byte[LoginAccountResponse1055.FrameLength];
            NetworkStream stream = _client.GetStream();
            await ReadAndDecryptExactlyAsync(stream,
                frame.AsMemory(0, LoginDecodedFrameReader.HeaderLength), deadline.Token).ConfigureAwait(false);

            if (BinaryPrimitives.ReadUInt16LittleEndian(frame) != LoginAccountResponse1055.FrameLength ||
                BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(2)) != LoginAccountResponse1055.PacketType)
            {
                throw new InvalidDataException("AccountServer returned an invalid authentication frame header.");
            }

            await ReadAndDecryptExactlyAsync(stream,
                frame.AsMemory(LoginDecodedFrameReader.HeaderLength), deadline.Token).ConfigureAwait(false);

            if (!LoginAccountResponse1055.TryRead(frame, out LoginAccountResponse1055 response))
            {
                throw new InvalidDataException("AccountServer returned an invalid authentication response.");
            }

            if (!response.IsSuccess)
            {
                await DisposeAsync().ConfigureAwait(false);
            }
            else
            {
                _successfulSessionToken = response.SessionToken;
            }

            return response;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await DisposeAsync().ConfigureAwait(false);
            throw new TimeoutException("AccountServer authentication exchange timed out.");
        }
        catch
        {
            await DisposeAsync().ConfigureAwait(false);
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(request);
        }
    }


    /// <summary>
    /// Single-use native continuation after a successful 1055. Sends 1100 then the AccountServer
    /// form of 1052 on the SAME outbound cipher stream and always closes the account socket.
    /// A successful socket write does not prove the remote peer accepted these telemetry reports.
    /// </summary>
    internal async Task SendPostAuthenticationReportsAsync(
        uint sessionToken, ReadOnlyMemory<byte> macAddress, int resourceVersion,
        TimeSpan phaseTimeout, CancellationToken cancellationToken = default)
    {
        if (phaseTimeout <= TimeSpan.Zero || phaseTimeout > TimeSpan.FromSeconds(30))
        {
            throw new ArgumentOutOfRangeException(nameof(phaseTimeout));
        }

        if (sessionToken == 0 || sessionToken != _successfulSessionToken)
        {
            throw new InvalidOperationException("The session has no matching successful authentication.");
        }

        if (Interlocked.CompareExchange(ref _postAuthenticationStarted, 1, 0) != 0)
        {
            throw new InvalidOperationException("Post-authentication reports have already been attempted.");
        }

        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        byte[] macFrame = new byte[LoginAccountMacAddressReport1100.FrameLength];
        byte[] resourceFrame = new byte[LoginAccountResourceVersionReport1052.FrameLength];
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Validate and construct BOTH reports before any bytes are sent.
            LoginAccountMacAddressReport1100.Write(macFrame, sessionToken, macAddress.Span);
            LoginAccountResourceVersionReport1052.Write(resourceFrame, sessionToken, resourceVersion);

            using CancellationTokenSource deadline =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(phaseTimeout);

            NetworkStream stream = _client.GetStream();
            _cipher.EncryptOutbound(macFrame);
            await stream.WriteAsync(macFrame, deadline.Token).ConfigureAwait(false);

            _cipher.EncryptOutbound(resourceFrame);
            await stream.WriteAsync(resourceFrame, deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("AccountServer post-authentication reports timed out.");
        }
        finally
        {
            // The native account socket is not reused for world login and is never retried.
            await DisposeAsync().ConfigureAwait(false);
            CryptographicOperations.ZeroMemory(macFrame);
            CryptographicOperations.ZeroMemory(resourceFrame);
        }
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _client.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    private async Task<uint> ReadSeedAsync(CancellationToken token)
    {
        // Exactly-sized reads avoid consuming any subsequent frame. 1059 is at most
        // 64 bytes under the independently verified client packet contract.
        byte[] frame = new byte[LoginSeed1059.MaximumFrameLength];
        NetworkStream stream = _client.GetStream();
        await ReadAndDecryptExactlyAsync(stream, frame.AsMemory(0, LoginDecodedFrameReader.HeaderLength), token)
            .ConfigureAwait(false);

        ReadOnlySequence<byte> header = new(frame.AsMemory(0, LoginDecodedFrameReader.HeaderLength));
        _ = LoginDecodedFrameReader.TryRead(ref header, out _);

        int length = BinaryPrimitives.ReadUInt16LittleEndian(frame);
        if (length is < LoginSeed1059.MinimumFrameLength or > LoginSeed1059.MaximumFrameLength)
        {
            throw new InvalidDataException("AccountServer seed frame has an invalid length.");
        }

        await ReadAndDecryptExactlyAsync(
            stream, frame.AsMemory(LoginDecodedFrameReader.HeaderLength,
                length - LoginDecodedFrameReader.HeaderLength), token).ConfigureAwait(false);

        ReadOnlySequence<byte> input = new(frame.AsMemory(0, length));
        if (!LoginDecodedFrameReader.TryRead(ref input, out ReadOnlySequence<byte> decoded) ||
            !input.IsEmpty || !LoginSeed1059.TryReadSeed(decoded.FirstSpan, out uint seed))
        {
            throw new InvalidDataException("AccountServer did not send a valid 1059 seed frame.");
        }

        return seed;
    }

    private async Task ReadAndDecryptExactlyAsync(NetworkStream stream, Memory<byte> remaining, CancellationToken token)
    {
        while (!remaining.IsEmpty)
        {
            int read = await stream.ReadAsync(remaining, token).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException("AccountServer closed before the expected frame completed.");
            }

            _cipher.DecryptInbound(remaining.Span[..read]);
            remaining = remaining[read..];
        }
    }
}
