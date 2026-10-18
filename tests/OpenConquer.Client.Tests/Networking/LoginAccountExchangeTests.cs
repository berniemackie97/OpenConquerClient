using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using OpenConquer.Networking.Authentication;

namespace OpenConquer.Client.Tests.Networking;

public sealed class LoginAccountExchangeTests
{
    private const string SeedWire = "C54869128E317C0F";
    private const string SuccessWire =
        "BFF0C21A058F75E70B44B1BAB2056F3EB900E32A626FF42466CAF8503A345F0E9D5053BA";
    private const string FailureWire =
        "BFF0C21A41BC57F6B4980B2239646F3E6D00E32A714C87C66528FBB229345F0E9D5053BA";
    private static readonly LoginAccountTimeouts s_timeouts =
        new(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

    [Fact]
    public async Task SeedRequestResponsePreserveIndependentCipherPositionsAndContinuation()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener listener = Listen();
        Task<int> peer = PeerAsync(listener, Convert.FromHexString(SuccessWire), token, validateRequest: true);

        await using LoginAccountSession session = await OpenAsync(listener, token);
        LoginAccountResponse1055 response = await session.AuthenticateAsync(
            "testacc"u8.ToArray(), "password1".AsMemory(), "LocalRealm"u8.ToArray(),
            static () => true, TimeSpan.FromSeconds(5), token);

        Assert.True(response.IsSuccess);
        Assert.Equal(0x11223344u, response.SessionToken);
        Assert.Equal(0x89ABCDEFu, response.AuthenticationKeyOrFailureCode);
        Assert.Equal(5816u, response.GamePort);
        Assert.Equal(77u, response.AdditionalSessionField);
        Assert.Equal("127.0.0.1", response.GameHost);

        // The session remains live on the 1055 success branch until its owner disposes it.
        await session.DisposeAsync();
        Assert.Equal(0, await peer.WaitAsync(TimeSpan.FromSeconds(5), token));
    }

    [Fact]
    public async Task FailurePreservesOnlyCodeAndClosesSocket()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener listener = Listen();
        Task<int> peer = PeerAsync(listener, Convert.FromHexString(FailureWire), token);

        await using LoginAccountSession session = await OpenAsync(listener, token);
        LoginAccountResponse1055 response = await session.AuthenticateAsync(
            "testacc"u8.ToArray(), "password1".AsMemory(), "LocalRealm"u8.ToArray(),
            static () => true, TimeSpan.FromSeconds(5), token);

        Assert.False(response.IsSuccess);
        Assert.Equal(20u, response.AuthenticationKeyOrFailureCode);
        Assert.Null(response.GameHost);
        Assert.Equal(0u, response.GamePort);
        Assert.Equal(0, await peer.WaitAsync(TimeSpan.FromSeconds(5), token));
    }

    [Fact]
    public async Task UnknownFailureCodeIsPreservedWithoutAnyEndpoint()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener listener = Listen();
        Task<int> peer = PeerAsync(listener, Convert.FromHexString(
            "BFF0C21A41BC57F60B73D1CF39646F3E6D00E32A714C87C66528FBB229345F0E9D5053BA"), token);

        await using LoginAccountSession session = await OpenAsync(listener, token);
        LoginAccountResponse1055 response = await session.AuthenticateAsync(
            "testacc"u8.ToArray(), "password1".AsMemory(), "LocalRealm"u8.ToArray(),
            static () => true, TimeSpan.FromSeconds(5), token);

        Assert.False(response.IsSuccess);
        Assert.Equal(0xDEADBEEFu, response.AuthenticationKeyOrFailureCode);
        Assert.Null(response.GameHost);
        Assert.Equal(0u, response.GamePort);
        Assert.Equal(0, await peer.WaitAsync(TimeSpan.FromSeconds(5), token));
    }

    [Theory]
    [InlineData("BFF0211A51BC57F60B44B1BAB2056F3EB900E32A626FF42466CAF8503A345F0E9D5053BA")]
    [InlineData("BFF0C21A51BC57F6D5980B2239646F3EB900E32A626FF42466CAF8503A345F0E9D5053BA")]
    public async Task UnexpectedTypeOrMalformedSuccessEndpointAbortsSession(string wire)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener listener = Listen();
        byte[] response = Convert.FromHexString(wire);
        // An unexpected packet type is rejected from its four-byte header. Do not
        // race a terminal socket close by sending a body the parser must never read.
        if (response[2] == 0x21)
        {
            response = response[..4];
        }

        Task<int> peer = PeerAsync(listener, response, token);

        await using LoginAccountSession session = await OpenAsync(listener, token);
        await Assert.ThrowsAsync<InvalidDataException>(() => session.AuthenticateAsync(
            "testacc"u8.ToArray(), "password1".AsMemory(), "LocalRealm"u8.ToArray(),
            static () => true, TimeSpan.FromSeconds(5), token));

        Assert.Equal(0, await peer.WaitAsync(TimeSpan.FromSeconds(5), token));
    }

    [Fact]
    public async Task MalformedHeaderFailsClosedBeforeReadingBody()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener listener = Listen();
        byte[] wire = Convert.FromHexString(SuccessWire);
        wire[0] = 0; // Changes decoded length, not the expected response packet header.
        Task<int> peer = PeerAsync(listener, wire[..4], token);

        await using LoginAccountSession session = await OpenAsync(listener, token);
        await Assert.ThrowsAsync<InvalidDataException>(() => session.AuthenticateAsync(
            "testacc"u8.ToArray(), "password1".AsMemory(), "LocalRealm"u8.ToArray(),
            static () => true, TimeSpan.FromSeconds(5), token));
        Assert.Equal(0, await peer.WaitAsync(TimeSpan.FromSeconds(5), token));
    }

    [Fact]
    public async Task TruncatedResponseFailsWithEof()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener listener = Listen();
        Task peer = PeerDisconnectsAsync(listener, Convert.FromHexString(SuccessWire)[..10], token);

        await using LoginAccountSession session = await OpenAsync(listener, token);
        await Assert.ThrowsAsync<EndOfStreamException>(() => session.AuthenticateAsync(
            "testacc"u8.ToArray(), "password1".AsMemory(), "LocalRealm"u8.ToArray(),
            static () => true, TimeSpan.FromSeconds(5), token));
        await peer.WaitAsync(TimeSpan.FromSeconds(5), token);
    }

    [Fact]
    public async Task AuthorityRejectionNeverWritesCredentialsAndClosesConnection()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener listener = Listen();
        Task<int> peer = PeerObservesNoCredentialsAsync(listener, token);

        await using LoginAccountSession session = await OpenAsync(listener, token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.AuthenticateAsync(
            "testacc"u8.ToArray(), "password1".AsMemory(), "LocalRealm"u8.ToArray(),
            static () => false, TimeSpan.FromSeconds(5), token));

        Assert.Equal(0, await peer.WaitAsync(TimeSpan.FromSeconds(5), token));
    }

    [Fact]
    public async Task PendingAuthenticationRejectsSecondAttemptAndHonorsCancellation()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener listener = Listen();
        TaskCompletionSource<bool> received = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task peer = PeerWaitsForClientAbortAsync(listener, received, token);

        await using LoginAccountSession session = await OpenAsync(listener, token);
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        Task<LoginAccountResponse1055> first = session.AuthenticateAsync(
            "testacc"u8.ToArray(), "password1".AsMemory(), "LocalRealm"u8.ToArray(),
            static () => true, TimeSpan.FromSeconds(5), cancellation.Token);
        await received.Task.WaitAsync(TimeSpan.FromSeconds(5), token);

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.AuthenticateAsync(
            "testacc"u8.ToArray(), "password1".AsMemory(), "LocalRealm"u8.ToArray(),
            static () => true, TimeSpan.FromSeconds(5), token));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        await peer.WaitAsync(TimeSpan.FromSeconds(5), token);
    }

    [Fact]
    public async Task ResponseTimeoutClosesConnection()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener listener = Listen();
        TaskCompletionSource<bool> received = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task peer = PeerWaitsForClientAbortAsync(listener, received, token);

        await using LoginAccountSession session = await OpenAsync(listener, token);
        await Assert.ThrowsAsync<TimeoutException>(() => session.AuthenticateAsync(
            "testacc"u8.ToArray(), "password1".AsMemory(), "LocalRealm"u8.ToArray(),
            static () => true, TimeSpan.FromMilliseconds(150), token));
        await peer.WaitAsync(TimeSpan.FromSeconds(5), token);
    }

    private static TcpListener Listen()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        return listener;
    }

    private static async Task<LoginAccountSession> OpenAsync(TcpListener listener, CancellationToken token)
    {
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        return await LoginAccountSession.OpenAsync("127.0.0.1", port, s_timeouts, token);
    }

    private static async Task<int> PeerAsync(TcpListener listener, byte[] response, CancellationToken token,
        bool validateRequest = false)
    {
        using TcpClient client = await listener.AcceptTcpClientAsync(token);
        NetworkStream stream = client.GetStream();
        await stream.WriteAsync(Convert.FromHexString(SeedWire), token);
        byte[] request = new byte[StandardAccountRequest1060.PacketLength];
        await stream.ReadExactlyAsync(request, token);

        if (validateRequest)
        {
            DecodeClientRequest(request);
            Assert.Equal(276, BinaryPrimitives.ReadUInt16LittleEndian(request));
            Assert.Equal(1060, BinaryPrimitives.ReadUInt16LittleEndian(request.AsSpan(2)));
            Assert.Equal("testacc"u8.ToArray(), request.AsSpan(4, 7).ToArray());
            Assert.Equal("LocalRealm"u8.ToArray(), request.AsSpan(260, 10).ToArray());
            Assert.Equal("B059847E5023FEDCD3BCE92F93A9A71008C9ED3654CD597508C9ED3654CD5975",
                Convert.ToHexString(request.AsSpan(132, 32)));
        }

        if (response.Length == LoginDecodedFrameReader.HeaderLength)
        {
            await stream.WriteAsync(response, token);
        }
        else
        {
            for (int offset = 0; offset < response.Length; offset += 3)
            {
                await stream.WriteAsync(response.AsMemory(offset, Math.Min(3, response.Length - offset)), token);
            }
        }

        return await ObserveCloseAsync(stream, token);
    }

    private static async Task PeerDisconnectsAsync(TcpListener listener, byte[] response, CancellationToken token)
    {
        using TcpClient client = await listener.AcceptTcpClientAsync(token);
        NetworkStream stream = client.GetStream();
        await stream.WriteAsync(Convert.FromHexString(SeedWire), token);
        byte[] request = new byte[StandardAccountRequest1060.PacketLength];
        await stream.ReadExactlyAsync(request, token);
        await stream.WriteAsync(response, token);
    }

    private static async Task<int> PeerObservesNoCredentialsAsync(TcpListener listener, CancellationToken token)
    {
        using TcpClient client = await listener.AcceptTcpClientAsync(token);
        NetworkStream stream = client.GetStream();
        await stream.WriteAsync(Convert.FromHexString(SeedWire), token);
        return await ObserveCloseAsync(stream, token);
    }

    private static async Task PeerWaitsForClientAbortAsync(TcpListener listener,
        TaskCompletionSource<bool> received, CancellationToken token)
    {
        using TcpClient client = await listener.AcceptTcpClientAsync(token);
        NetworkStream stream = client.GetStream();
        await stream.WriteAsync(Convert.FromHexString(SeedWire), token);
        byte[] request = new byte[StandardAccountRequest1060.PacketLength];
        await stream.ReadExactlyAsync(request, token);
        received.TrySetResult(true);
        Assert.Equal(0, await ObserveCloseAsync(stream, token));
    }

    private static async Task<int> ObserveCloseAsync(NetworkStream stream, CancellationToken token)
    {
        byte[] buffer = new byte[1];
        try
        {
            return await stream.ReadAsync(buffer, token);
        }
        catch (IOException exception) when (exception.InnerException is SocketException
        { SocketErrorCode: SocketError.ConnectionReset or SocketError.ConnectionAborted })
        {
            return 0;
        }
    }

    // Independent server-side inverse transform, not the production client stream cipher.
    private static void DecodeClientRequest(Span<byte> frame)
    {
        Span<byte> streamA = stackalloc byte[256];
        Span<byte> streamB = stackalloc byte[256];
        byte key = 0x9D;
        for (int i = 0; i < 256; i++)
        {
            streamA[i] = key;
            key = unchecked((byte)((((byte)(key * 0xFA) + 0x0F) * key) + 0x13));
        }

        key = 0x62;
        for (int i = 0; i < 256; i++)
        {
            streamB[i] = key;
            key = unchecked((byte)((((byte)(0x79 - (byte)(key * 0x5C))) * key) + 0x6D));
        }

        for (int i = 0; i < frame.Length; i++)
        {
            byte value = (byte)(frame[i] ^ 0xAB);
            value = (byte)((value << 4) | (value >> 4));
            frame[i] = (byte)(value ^ streamA[i & 255] ^ streamB[(i >> 8) & 255]);
        }
    }
}
