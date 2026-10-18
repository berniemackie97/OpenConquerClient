using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using OpenConquer.Client.Startup.Catalog;
using OpenConquer.Client.UI.Login.Selection;
using OpenConquer.Networking.Authentication;

namespace OpenConquer.Client.Tests.Startup.Catalog;

public sealed class LoginSignedAccountExchangeTests
{
    private static readonly DateTimeOffset s_now =
        new(2026, 10, 17, 22, 0, 0, TimeSpan.Zero);
    private static readonly LoginAccountTimeouts s_timeouts =
        new(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

    private const string SeedWire = "C54869128E317C0F";
    private const string SuccessWire =
        "BFF0C21A058F75E70B44B1BAB2056F3EB900E32A626FF42466CAF8503A345F0E9D5053BA";
    private const string FailureWire =
        "BFF0C21A41BC57F6B4980B2239646F3E6D00E32A714C87C66528FBB229345F0E9D5053BA";

    [Fact]
    public async Task CurrentSignedLoopbackSelectionPreservesNativeSuccessContinuation()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener listener = Listen();
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SignedLoginCatalog catalog = Catalog(signer, "127.0.0.1", Port(listener));
        LoginServerSelection selection = Selection(catalog);
        FixedClock clock = new(s_now);

        Task<int> peer = SendSeedReadRequestReplyAsync(listener, Convert.FromHexString(SuccessWire), token);
        await using (LoginAccountExchangeResult result = await LoginSignedAccountExchange.AuthenticateAsync(
            catalog, selection, "testacc"u8.ToArray(), "password1".AsMemory(),
            s_timeouts, clock, token))
        {
            Assert.True(result.IsSuccess);
            // A valid 1055 is an intermediate AccountServer success, not world admission.
            Assert.True(result.IsSuccess);
            Assert.Equal(0u, result.FailureCode);
            Assert.Equal("LoginAccountExchangeResult { IsSuccess = True }", result.ToString());
        }

        Assert.Equal(0, await peer.WaitAsync(TimeSpan.FromSeconds(5), token));
    }

    [Fact]
    public async Task FailureClosesAccountConnectionAndDoesNotExposeContinuation()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener listener = Listen();
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SignedLoginCatalog catalog = Catalog(signer, "127.0.0.1", Port(listener));
        Task<int> peer = SendSeedReadRequestReplyAsync(listener, Convert.FromHexString(FailureWire), token);

        await using LoginAccountExchangeResult result = await LoginSignedAccountExchange.AuthenticateAsync(
            catalog, Selection(catalog), "testacc"u8.ToArray(), "password1".AsMemory(),
            s_timeouts, new FixedClock(s_now), token);

        Assert.False(result.IsSuccess);
        Assert.Equal(20u, result.FailureCode);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            result.CompletePostAuthenticationAsync([], 5517, TimeSpan.FromSeconds(5), token));
        Assert.Equal("LoginAccountExchangeResult { IsSuccess = False }", result.ToString());
        Assert.Equal(0, await peer.WaitAsync(TimeSpan.FromSeconds(5), token));
    }

    [Fact]
    public async Task SelectionReplacementDuringSeedRevokesCredentialSubmission()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener listener = Listen();
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SignedLoginCatalog catalog = Catalog(signer, "127.0.0.1", Port(listener));
        LoginServerSelection selection = Selection(catalog);
        TaskCompletionSource<bool> accepted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> sendSeed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<int> peer = DelayedSeedNoCredentialPeerAsync(listener, accepted, sendSeed, token);

        Task<LoginAccountExchangeResult> exchange = LoginSignedAccountExchange.AuthenticateAsync(
            catalog, selection, "testacc"u8.ToArray(), "password1".AsMemory(),
            s_timeouts, new FixedClock(s_now), token);

        await accepted.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
        selection.ReplaceCatalog(catalog.View);
        Assert.True(selection.SelectRegionSlot(0));
        Assert.True(selection.SelectServerSlot(0));
        sendSeed.TrySetResult(true);

        await Assert.ThrowsAsync<InvalidOperationException>(() => exchange);
        Assert.Equal(0, await peer.WaitAsync(TimeSpan.FromSeconds(5), token));
    }

    [Fact]
    public async Task CatalogExpiryDuringSeedRevokesCredentialSubmission()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener listener = Listen();
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SignedLoginCatalog catalog = Catalog(signer, "127.0.0.1", Port(listener));
        LoginServerSelection selection = Selection(catalog);
        FixedClock clock = new(s_now);
        TaskCompletionSource<bool> accepted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> sendSeed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<int> peer = DelayedSeedNoCredentialPeerAsync(listener, accepted, sendSeed, token);

        Task<LoginAccountExchangeResult> exchange = LoginSignedAccountExchange.AuthenticateAsync(
            catalog, selection, "testacc"u8.ToArray(), "password1".AsMemory(),
            s_timeouts, clock, token);

        await accepted.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
        clock.Set(new DateTimeOffset(2026, 10, 18, 0, 0, 0, TimeSpan.Zero));
        sendSeed.TrySetResult(true);

        await Assert.ThrowsAsync<InvalidOperationException>(() => exchange);
        Assert.Equal(0, await peer.WaitAsync(TimeSpan.FromSeconds(5), token));
    }

    [Fact]
    public async Task UntrustedOrRemoteSelectionFailsBeforeConnecting()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SignedLoginCatalog remote = Catalog(signer, "login.example.org", 9958);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            LoginSignedAccountExchange.AuthenticateAsync(remote, Selection(remote),
                "testacc"u8.ToArray(), "password1".AsMemory(), s_timeouts, new FixedClock(s_now), token));

        LoginServerSelection unrelated = new();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            LoginSignedAccountExchange.AuthenticateAsync(remote, unrelated,
                "testacc"u8.ToArray(), "password1".AsMemory(), s_timeouts, new FixedClock(s_now), token));
    }

    private static LoginServerSelection Selection(SignedLoginCatalog catalog)
    {
        LoginServerSelection selection = new();
        selection.ReplaceCatalog(catalog.View);
        Assert.True(selection.SelectRegionSlot(0));
        Assert.True(selection.SelectServerSlot(0));
        return selection;
    }

    private static SignedLoginCatalog Catalog(ECDsa signer, string host, int port)
    {
        byte[] payload = Encoding.UTF8.GetBytes(
            $"{{\"schemaVersion\":1,\"productId\":\"OpenConquer\",\"sequence\":7," +
            $"\"issuedAt\":\"2026-10-17T21:00:00Z\",\"expiresAt\":\"2026-10-17T23:00:00Z\"," +
            $"\"regions\":[{{\"id\":\"north\",\"name\":\"North\",\"servers\":[" +
            $"{{\"id\":\"realm-1\",\"name\":\"Realm One\",\"protocolServerName\":\"LocalRealm\"," +
            $"\"accountHost\":\"{host}\",\"accountPort\":{port}}}]}}]}}");
        byte[] signature = signer.SignData(payload, HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return SignedLoginCatalog.Read(payload, signature,
            SignedLoginCatalogTests.Authority(signer), s_now);
    }

    private static TcpListener Listen()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        return listener;
    }

    private static int Port(TcpListener listener) => ((IPEndPoint)listener.LocalEndpoint).Port;

    private static async Task<int> SendSeedReadRequestReplyAsync(TcpListener listener,
        byte[] response, CancellationToken token)
    {
        using TcpClient client = await listener.AcceptTcpClientAsync(token);
        NetworkStream stream = client.GetStream();
        await stream.WriteAsync(Convert.FromHexString(SeedWire), token);
        byte[] request = new byte[276];
        await stream.ReadExactlyAsync(request, token);
        await stream.WriteAsync(response, token);
        return await ObserveCloseAsync(stream, token);
    }

    private static async Task<int> DelayedSeedNoCredentialPeerAsync(TcpListener listener,
        TaskCompletionSource<bool> accepted, TaskCompletionSource<bool> sendSeed, CancellationToken token)
    {
        using TcpClient client = await listener.AcceptTcpClientAsync(token);
        accepted.TrySetResult(true);
        await sendSeed.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
        NetworkStream stream = client.GetStream();
        await stream.WriteAsync(Convert.FromHexString(SeedWire), token);
        return await ObserveCloseAsync(stream, token);
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

    private sealed class FixedClock(DateTimeOffset initial) : TimeProvider
    {
        private DateTimeOffset _current = initial;

        public override DateTimeOffset GetUtcNow() => _current;

        internal void Set(DateTimeOffset value) => _current = value;
    }
}
