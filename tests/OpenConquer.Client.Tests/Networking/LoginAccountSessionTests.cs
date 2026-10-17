using System.Net;
using System.Net.Sockets;
using OpenConquer.Networking.Authentication;

namespace OpenConquer.Client.Tests.Networking;

public sealed class LoginAccountSessionTests
{
    private static readonly LoginAccountTimeouts s_timeouts =
        new(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

    private const string EncryptedSeed1059 = "C54869128E317C0F";

    [Fact]
    public async Task FragmentedSeedIsDecodedAndSocketIsClosedOnDispose()
    {
        using TcpListener listener = Listen();
        Task<int> server = SendThenObserveCloseAsync(listener, Convert.FromHexString(EncryptedSeed1059), 1);

        await using (LoginAccountSession session = await LoginAccountSession.OpenAsync(
                         "127.0.0.1", Port(listener), s_timeouts))
        {
            Assert.Equal(0x12345678u, session.Seed);
        }

        Assert.Equal(0, await server.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Theory]
    [InlineData("454869128E317C0F")] // decoded length is zero
    [InlineData("C548AA128E317C0F")] // decoded packet type is 1055, not 1059
    public async Task MalformedOrUnexpectedSeedFailsClosed(string encrypted)
    {
        using TcpListener listener = Listen();
        Task<int> server = SendThenObserveCloseAsync(listener, Convert.FromHexString(encrypted), 8);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            LoginAccountSession.OpenAsync("127.0.0.1", Port(listener), s_timeouts));

        Assert.Equal(0, await server.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task PrematureEofFailsWithoutReturningSession()
    {
        using TcpListener listener = Listen();
        Task server = SendThenDisconnectAsync(listener, Convert.FromHexString("C5486912"));

        await Assert.ThrowsAsync<EndOfStreamException>(() =>
            LoginAccountSession.OpenAsync("127.0.0.1", Port(listener), s_timeouts));

        await server.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task SeedDeadlineCancelsReadAndClosesSocket()
    {
        using TcpListener listener = Listen();
        Task<int> server = AcceptAndObserveCloseAsync(listener);
        LoginAccountTimeouts shortSeed =
            new(TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAsync<TimeoutException>(() =>
            LoginAccountSession.OpenAsync("127.0.0.1", Port(listener), shortSeed));

        Assert.Equal(0, await server.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task CallerCancellationPropagatesAndClosesSocket()
    {
        using TcpListener listener = Listen();
        Task<int> server = AcceptAndObserveCloseAsync(listener);
        using CancellationTokenSource cancellation = new(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            LoginAccountSession.OpenAsync("127.0.0.1", Port(listener), s_timeouts, cancellation.Token));

        Assert.Equal(0, await server.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task InvalidTimeoutsAndHostsFailBeforeOpeningTransport()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            LoginAccountSession.OpenAsync("127.0.0.1", 9958, default));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            LoginAccountSession.OpenAsync("https://example.org", 9958, s_timeouts));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            LoginAccountSession.OpenAsync("127.0.0.1", 0, s_timeouts));
    }

    private static TcpListener Listen()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        return listener;
    }

    private static int Port(TcpListener listener) => ((IPEndPoint)listener.LocalEndpoint).Port;

    private static async Task<int> SendThenObserveCloseAsync(TcpListener listener, byte[] bytes, int chunk)
    {
        using TcpClient client = await listener.AcceptTcpClientAsync();
        NetworkStream stream = client.GetStream();
        for (int offset = 0; offset < bytes.Length; offset += chunk)
        {
            await stream.WriteAsync(bytes.AsMemory(offset, Math.Min(chunk, bytes.Length - offset)));
        }

        byte[] buffer = new byte[1];
        return await stream.ReadAsync(buffer);
    }

    private static async Task SendThenDisconnectAsync(TcpListener listener, byte[] bytes)
    {
        using TcpClient client = await listener.AcceptTcpClientAsync();
        await client.GetStream().WriteAsync(bytes);
    }

    private static async Task<int> AcceptAndObserveCloseAsync(TcpListener listener)
    {
        using TcpClient client = await listener.AcceptTcpClientAsync();
        byte[] buffer = new byte[1];
        return await client.GetStream().ReadAsync(buffer);
    }
}
