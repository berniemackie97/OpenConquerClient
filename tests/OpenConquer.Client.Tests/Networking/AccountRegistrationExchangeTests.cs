using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using OpenConquer.Networking.Authentication;

namespace OpenConquer.Client.Tests.Networking;

public sealed class AccountRegistrationExchangeTests
{
    private static readonly LoginAccountTimeouts s_timeouts = new(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

    [Theory]
    [InlineData(0u, (uint)AccountRegistrationResult.Created)]
    [InlineData(1u, (uint)AccountRegistrationResult.InvalidRequest)]
    [InlineData(2u, (uint)AccountRegistrationResult.AccountNameTaken)]
    [InlineData(3u, (uint)AccountRegistrationResult.Failed)]
    [InlineData(4u, (uint)AccountRegistrationResult.Unavailable)]
    public async Task RegisterSendsSharedCredentialEnvelopeAndAlwaysCloses(uint code, uint expected)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener listener = Listen();
        Task<byte[]> peer = PeerAsync(listener, AccountServerTestWire.RegistrationResultWire(code), token);

        LoginAccountSession session = await OpenAsync(listener, token);
        AccountRegistrationResult result = await session.RegisterAsync("testacc"u8.ToArray(), "password1".AsMemory(),
            static () => true, TimeSpan.FromSeconds(5), token);

        Assert.Equal((AccountRegistrationResult)expected, result);
        byte[] request = await peer.WaitAsync(TimeSpan.FromSeconds(5), token);
        AccountServerTestWire.DecodeClientRequest(request);
        Assert.Equal(260, BinaryPrimitives.ReadUInt16LittleEndian(request));
        Assert.Equal(0xF001, BinaryPrimitives.ReadUInt16LittleEndian(request.AsSpan(2)));
        Assert.Equal("testacc"u8.ToArray(), request.AsSpan(4, 7).ToArray());
        Assert.All(request.AsSpan(11, 121).ToArray(), static value => Assert.Equal(0, value));
        Assert.Equal(AccountServerTestWire.TestAccountCredential, Convert.ToHexString(request.AsSpan(132, 32)));
        Assert.All(request.AsSpan(164, 96).ToArray(), static value => Assert.Equal(0, value));

        // The session closed itself; a second exchange is refused.
        await Assert.ThrowsAsync<ObjectDisposedException>(() => session.RegisterAsync("testacc"u8.ToArray(),
            "password1".AsMemory(), static () => true, TimeSpan.FromSeconds(5), token));
    }

    [Fact]
    public async Task UndefinedResultCodeIsAProtocolError()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener listener = Listen();
        Task<byte[]> peer = PeerAsync(listener, AccountServerTestWire.RegistrationResultWire(5), token);

        LoginAccountSession session = await OpenAsync(listener, token);
        await Assert.ThrowsAsync<InvalidDataException>(() => session.RegisterAsync("testacc"u8.ToArray(),
            "password1".AsMemory(), static () => true, TimeSpan.FromSeconds(5), token));
        await peer.WaitAsync(TimeSpan.FromSeconds(5), token);
    }

    [Fact]
    public async Task AuthenticationReplyIsRejectedFromItsHeader()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener listener = Listen();
        Task<byte[]> peer = PeerAsync(listener, Convert.FromHexString(AccountServerTestWire.AuthenticationHeaderWire), token);

        LoginAccountSession session = await OpenAsync(listener, token);
        await Assert.ThrowsAsync<InvalidDataException>(() => session.RegisterAsync("testacc"u8.ToArray(),
            "password1".AsMemory(), static () => true, TimeSpan.FromSeconds(5), token));
        await peer.WaitAsync(TimeSpan.FromSeconds(5), token);
    }

    [Fact]
    public async Task AuthorityRejectionNeverWritesCredentials()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener listener = Listen();
        Task<int> peer = PeerObservesNoRequestAsync(listener, token);

        LoginAccountSession session = await OpenAsync(listener, token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.RegisterAsync("testacc"u8.ToArray(),
            "password1".AsMemory(), static () => false, TimeSpan.FromSeconds(5), token));

        Assert.Equal(0, await peer.WaitAsync(TimeSpan.FromSeconds(5), token));
    }

    [Fact]
    public async Task MissingResponseTimesOutAndCloses()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener listener = Listen();
        Task<byte[]> peer = PeerAsync(listener, [], token);

        LoginAccountSession session = await OpenAsync(listener, token);
        await Assert.ThrowsAsync<TimeoutException>(() => session.RegisterAsync("testacc"u8.ToArray(),
            "password1".AsMemory(), static () => true, TimeSpan.FromMilliseconds(150), token));
        await peer.WaitAsync(TimeSpan.FromSeconds(5), token);
    }

    [Fact]
    public async Task OneSessionCarriesOneCredentialExchange()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener listener = Listen();
        TaskCompletionSource<bool> received = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task peer = PeerWaitsForAbortAsync(listener, received, token);

        await using LoginAccountSession session = await OpenAsync(listener, token);
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        Task<LoginAccountResponse1055> login = session.AuthenticateAsync("testacc"u8.ToArray(), "password1".AsMemory(),
            "LocalRealm"u8.ToArray(), static () => true, TimeSpan.FromSeconds(5), cancellation.Token);
        await received.Task.WaitAsync(TimeSpan.FromSeconds(5), token);

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.RegisterAsync("testacc"u8.ToArray(),
            "password1".AsMemory(), static () => true, TimeSpan.FromSeconds(5), token));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => login);
        await peer.WaitAsync(TimeSpan.FromSeconds(5), token);
    }

    [Theory]
    [InlineData(7, 0xF002, 0u)]
    [InlineData(8, 1055, 0u)]
    [InlineData(8, 0xF002, 5u)]
    public void ResponseReaderRejectsMalformedFrames(int length, int type, uint code)
    {
        byte[] frame = new byte[8];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, (ushort)length);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), (ushort)type);
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(4), code);

        Assert.False(AccountRegistrationResponseF002.TryRead(frame, out _));
        Assert.False(AccountRegistrationResponseF002.TryRead(frame.AsSpan(0, 7), out _));
    }

    [Fact]
    public void RequestWriterValidatesFields()
    {
        byte[] destination = new byte[AccountRegistrationRequestF001.PacketLength];
        byte[] credential = new byte[32];

        Assert.Throws<ArgumentException>(() => AccountRegistrationRequestF001.Write(new byte[259], "a"u8, credential));
        Assert.Throws<ArgumentException>(() => AccountRegistrationRequestF001.Write(destination, ReadOnlySpan<byte>.Empty, credential));
        Assert.Throws<ArgumentException>(() => AccountRegistrationRequestF001.Write(destination, new byte[129], credential));
        Assert.Throws<ArgumentException>(() => AccountRegistrationRequestF001.Write(destination, "a\0b"u8, credential));
        Assert.Throws<ArgumentException>(() => AccountRegistrationRequestF001.Write(destination, "abc"u8, new byte[31]));
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

    private static async Task<byte[]> PeerAsync(TcpListener listener, byte[] response, CancellationToken token)
    {
        using TcpClient client = await listener.AcceptTcpClientAsync(token);
        NetworkStream stream = client.GetStream();
        await stream.WriteAsync(Convert.FromHexString(AccountServerTestWire.SeedWire), token);
        byte[] request = new byte[AccountRegistrationRequestF001.PacketLength];
        await stream.ReadExactlyAsync(request, token);
        if (response.Length > 0)
        {
            await stream.WriteAsync(response, token);
        }

        Assert.Equal(0, await ObserveCloseAsync(stream, token));
        return request;
    }

    private static async Task<int> PeerObservesNoRequestAsync(TcpListener listener, CancellationToken token)
    {
        using TcpClient client = await listener.AcceptTcpClientAsync(token);
        NetworkStream stream = client.GetStream();
        await stream.WriteAsync(Convert.FromHexString(AccountServerTestWire.SeedWire), token);
        return await ObserveCloseAsync(stream, token);
    }

    private static async Task PeerWaitsForAbortAsync(TcpListener listener, TaskCompletionSource<bool> received, CancellationToken token)
    {
        using TcpClient client = await listener.AcceptTcpClientAsync(token);
        NetworkStream stream = client.GetStream();
        await stream.WriteAsync(Convert.FromHexString(AccountServerTestWire.SeedWire), token);
        await stream.ReadExactlyAsync(new byte[StandardAccountRequest1060.PacketLength], token);
        received.TrySetResult(true);
        Assert.Equal(0, await ObserveCloseAsync(stream, token));
    }

    private static async Task<int> ObserveCloseAsync(NetworkStream stream, CancellationToken token)
    {
        try
        {
            return await stream.ReadAsync(new byte[1], token);
        }
        catch (IOException exception) when (exception.InnerException is SocketException
        { SocketErrorCode: SocketError.ConnectionReset or SocketError.ConnectionAborted })
        {
            return 0;
        }
    }
}
