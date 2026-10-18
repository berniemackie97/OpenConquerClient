using System.Net;
using System.Net.Sockets;
using OpenConquer.Networking.Authentication;

namespace OpenConquer.Client.Tests.Networking;

public sealed class LoginAccountPostAuthenticationTests
{
    private const string SeedWire = "C54869128E317C0F";
    private const string SuccessWire =
        "BFF0C21A058F75E70B44B1BAB2056F3EB900E32A626FF42466CAF8503A345F0E9D5053BA";
    private const string MacWire =
        "0294E07140E1CE61C615B9ADA77379ED64B513060BD7E779C413ADBF8B5568F24F9E2A3302DCE272C310A0B0825A63FB4E9B2D34";
    private const string EmptyMacWire =
        "0294E07140E1CE61C516AABE84506DF9409127320BD7E779C413ADBF8B5568F24F9E2A3302DCE272C310A0B0825A63FB4E9B2D34";
    private const string ResourceWire =
        "C8D12C3B862E81A0510E6EF46ACE17D746C0AF74C11AA6B2805469FD";

    [Fact]
    public async Task CompleteReportsOnOriginalConnectionWithCorrectCipherPositionAndCloses()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener listener = Listen();
        Task peer = VerifyReportsAsync(listener, Convert.FromHexString(MacWire),
            Convert.FromHexString(ResourceWire), token);

        await using LoginAccountSession session = await OpenAndAuthenticateAsync(listener, token);
        // The session is already on the 1055 success branch, not a new connection.
        // Success factory transfers its lifetime to this disposable result.
        LoginAccountResponse1055 response = SuccessResponse();
        await using LoginAccountExchangeResult result = LoginAccountExchangeResult.Success(response, session);
        Assert.Equal("LoginAccountExchangeResult { IsSuccess = True }", result.ToString());

        await result.CompletePostAuthenticationAsync("001122AABBCC"u8.ToArray(), 5517,
            TimeSpan.FromSeconds(5), token);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            result.CompletePostAuthenticationAsync([], 5517, TimeSpan.FromSeconds(5), token));
        await peer.WaitAsync(TimeSpan.FromSeconds(5), token);
    }

    [Fact]
    public async Task EmptyMacIsAllowedWithoutInventingHardwareIdentity()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener listener = Listen();
        Task peer = VerifyReportsAsync(listener, Convert.FromHexString(EmptyMacWire),
            Convert.FromHexString(ResourceWire), token);

        await using LoginAccountSession session = await OpenAndAuthenticateAsync(listener, token);
        await using LoginAccountExchangeResult result =
            LoginAccountExchangeResult.Success(SuccessResponse(), session);

        await result.CompletePostAuthenticationAsync([], 5517, TimeSpan.FromSeconds(5), token);
        await peer.WaitAsync(TimeSpan.FromSeconds(5), token);
    }

    [Theory]
    [InlineData("001122aabbcc")]
    [InlineData("NOT_A_HARDWARE")]
    public async Task InvalidMacFailsBeforeEitherReportIsWritten(string badMac)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener listener = Listen();
        Task peer = ExpectNoReportsAsync(listener, token);

        await using LoginAccountSession session = await OpenAndAuthenticateAsync(listener, token);
        await using LoginAccountExchangeResult result =
            LoginAccountExchangeResult.Success(SuccessResponse(), session);

        await Assert.ThrowsAsync<ArgumentException>(() => result.CompletePostAuthenticationAsync(
            System.Text.Encoding.ASCII.GetBytes(badMac), 5517, TimeSpan.FromSeconds(5), token));
        await peer.WaitAsync(TimeSpan.FromSeconds(5), token);
    }

    [Fact]
    public async Task CallerCancellationBeforePostAuthWriteClosesWithoutSending()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener listener = Listen();
        Task peer = ExpectNoReportsAsync(listener, token);
        await using LoginAccountSession session = await OpenAndAuthenticateAsync(listener, token);
        await using LoginAccountExchangeResult result =
            LoginAccountExchangeResult.Success(SuccessResponse(), session);
        using CancellationTokenSource canceled = CancellationTokenSource.CreateLinkedTokenSource(token);
        canceled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            result.CompletePostAuthenticationAsync([], 5517, TimeSpan.FromSeconds(5), canceled.Token));
        await peer.WaitAsync(TimeSpan.FromSeconds(5), token);
    }

    [Fact]
    public async Task DisposedAccountResultCannotSendReports()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener listener = Listen();
        Task peer = ExpectNoReportsAsync(listener, token);
        await using LoginAccountSession session = await OpenAndAuthenticateAsync(listener, token);
        await using LoginAccountExchangeResult result =
            LoginAccountExchangeResult.Success(SuccessResponse(), session);
        await result.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            result.CompletePostAuthenticationAsync([], 5517, TimeSpan.FromSeconds(5), token));
        await peer.WaitAsync(TimeSpan.FromSeconds(5), token);
    }

    [Fact]
    public async Task InvalidReportTimeoutIsRejectedBeforeSendingAndDoesNotExhaustAttempt()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener listener = Listen();
        Task peer = VerifyReportsAsync(listener, Convert.FromHexString(EmptyMacWire),
            Convert.FromHexString(ResourceWire), token);
        await using LoginAccountSession session = await OpenAndAuthenticateAsync(listener, token);
        await using LoginAccountExchangeResult result =
            LoginAccountExchangeResult.Success(SuccessResponse(), session);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            result.CompletePostAuthenticationAsync([], 5517, TimeSpan.Zero, token));
        await result.CompletePostAuthenticationAsync([], 5517, TimeSpan.FromSeconds(5), token);
        await peer.WaitAsync(TimeSpan.FromSeconds(5), token);
    }

    private static LoginAccountResponse1055 SuccessResponse()
    {
        byte[] frame = new byte[36];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(frame, 36);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), 1055);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(4), 0x11223344);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(8), 0x89ABCDEF);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(12), 5816);
        "127.0.0.1"u8.CopyTo(frame.AsSpan(20));
        Assert.True(LoginAccountResponse1055.TryRead(frame, out LoginAccountResponse1055 parsed));
        return parsed;
    }

    private static TcpListener Listen()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        return listener;
    }

    private static async Task<LoginAccountSession> OpenAndAuthenticateAsync(TcpListener listener,
        CancellationToken token)
    {
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        LoginAccountSession session = await LoginAccountSession.OpenAsync("127.0.0.1", port,
            new LoginAccountTimeouts(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5)), token);
        try
        {
            LoginAccountResponse1055 response = await session.AuthenticateAsync(
                "testacc"u8.ToArray(), "password1".AsMemory(), "LocalRealm"u8.ToArray(),
                static () => true, TimeSpan.FromSeconds(5), token);
            Assert.True(response.IsSuccess);
            Assert.Equal(0x11223344u, response.SessionToken);
            return session;
        }
        catch
        {
            await session.DisposeAsync();
            throw;
        }
    }

    private static async Task VerifyReportsAsync(TcpListener listener,
        byte[] expectedMacWire, byte[] expectedResourceWire, CancellationToken token)
    {
        using TcpClient client = await listener.AcceptTcpClientAsync(token);
        NetworkStream stream = client.GetStream();
        await stream.WriteAsync(Convert.FromHexString(SeedWire), token);
        byte[] request = new byte[276];
        await stream.ReadExactlyAsync(request, token);
        await stream.WriteAsync(Convert.FromHexString(SuccessWire), token);

        byte[] macWire = new byte[52];
        await stream.ReadExactlyAsync(macWire, token);
        Assert.Equal(expectedMacWire, macWire);
        byte[] resourceWire = new byte[28];
        await stream.ReadExactlyAsync(resourceWire, token);
        Assert.Equal(expectedResourceWire, resourceWire);
        Assert.Equal(0, await ObserveCloseAsync(stream, token));
    }

    private static async Task ExpectNoReportsAsync(TcpListener listener, CancellationToken token)
    {
        using TcpClient client = await listener.AcceptTcpClientAsync(token);
        NetworkStream stream = client.GetStream();
        await stream.WriteAsync(Convert.FromHexString(SeedWire), token);
        byte[] request = new byte[276];
        await stream.ReadExactlyAsync(request, token);
        await stream.WriteAsync(Convert.FromHexString(SuccessWire), token);
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
}
