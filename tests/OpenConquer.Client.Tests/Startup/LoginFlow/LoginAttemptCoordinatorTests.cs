using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using OpenConquer.Client.Startup.Catalog;
using OpenConquer.Client.Startup.LoginFlow;
using OpenConquer.Client.Tests.Startup.Catalog;
using OpenConquer.Client.UI.Login;
using OpenConquer.Client.UI.Login.Account;
using OpenConquer.Client.UI.Login.Credentials;
using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Client.UI.Login.Status;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.Startup.LoginFlow;

public sealed class LoginAttemptCoordinatorTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("oc-login-flow-").FullName;

    [Fact]
    public async Task NoSignedRealmCannotStartOrConsumeCredentials()
    {
        using LoginScreen login = new(LoginStageLayout.Create(new LogicalRenderSize(1024, 768)),
            new LoginAccountHistory(_folder), "5517", _ => { });
        await using LoginAttemptCoordinator attempts = new(_folder);
        Assert.False(attempts.TryStart(login, null));
        Assert.Equal(LoginAttemptStatus.SelectRealm, attempts.Status);
        attempts.Update(null);
        Assert.Equal(LoginAttemptStatus.SelectRealm, attempts.Status);
    }

    [Fact]
    public async Task DisposedAttemptCoordinatorRejectsNewSubmissions()
    {
        using LoginScreen login = new(LoginStageLayout.Create(new LogicalRenderSize(1024, 768)),
            new LoginAccountHistory(_folder), "5517", _ => { });
        LoginAttemptCoordinator attempts = new(_folder);
        await attempts.DisposeAsync();
        await attempts.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => attempts.TryStart(login, null));
    }

    [Fact]
    public async Task SuccessfulAccountReportsSendBothNativeFramesBeforeGameHandoff()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        DateTimeOffset now = new(2026, 10, 17, 22, 0, 0, TimeSpan.Zero);
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SignedLoginCatalog catalog = CreateSignedLocalCatalog(signer, port, now);
        using LoginScreen screen = CreateFilledScreen(catalog);

        Task peer = AcceptAccountAndReadNativeReportsAsync(listener, token);
        await using LoginAttemptCoordinator attempts = new(_folder, new FixedClock(now));
        Assert.True(attempts.TryStart(screen, catalog));
        Assert.Equal(0, screen.Credentials.PasswordLength);

        // This cannot complete if the 1100 account MAC mistakenly receives
        // the six-byte raw hardware key expected by GameServer proof 1052.
        await peer.WaitAsync(TimeSpan.FromSeconds(5), token);
        await attempts.DisposeAsync();
    }

    [Fact]
    public async Task RealEncryptedLoopbackAccountRefusalReturnsToLoginWithoutLeakingCredentials()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        DateTimeOffset now = new(2026, 10, 17, 22, 0, 0, TimeSpan.Zero);
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        SignedLoginCatalog catalog = CreateSignedLocalCatalog(signer, port, now);
        using LoginScreen screen = CreateFilledScreen(catalog);

        Task peer = RefuseAccountAsync(listener, token);
        await using LoginAttemptCoordinator attempts = new(_folder, new FixedClock(now));
        Assert.True(attempts.TryStart(screen, catalog));
        Assert.Equal(0, screen.Credentials.PasswordLength);
        Assert.False(attempts.TryStart(screen, catalog));

        await peer.WaitAsync(TimeSpan.FromSeconds(5), token);
        using CancellationTokenSource budget = CancellationTokenSource.CreateLinkedTokenSource(token);
        budget.CancelAfter(TimeSpan.FromSeconds(5));
        while (attempts.Status == LoginAttemptStatus.ConnectingAccount)
        {
            attempts.Update(catalog);
            if (attempts.Status != LoginAttemptStatus.ConnectingAccount)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10), budget.Token);
        }

        Assert.Equal(LoginAttemptStatus.AccountRefused, attempts.Status);
        Assert.True(attempts.TryStart(screen, catalog) is false);
        Assert.Equal(LoginAttemptStatus.RetryLater, attempts.Status);
    }

    private LoginScreen CreateFilledScreen(SignedLoginCatalog catalog)
    {
        LoginStageLayout layout = LoginStageLayout.Create(new LogicalRenderSize(1024, 768));
        LoginScreen screen = new(layout, new LoginAccountHistory(_folder), "5517", _ => { });
        screen.Selection.ReplaceCatalog(catalog.View);
        Assert.True(screen.Selection.SelectRegionSlot(0));
        Assert.True(screen.Selection.SelectServerSlot(0));
        screen.Advance(TimeSpan.Zero);

        LoginCredentialLayout fields = LoginCredentialLayout.Create(layout);
        Assert.True(screen.Credentials.PointerPressed(true, fields.AccountField.X + 2,
            fields.AccountField.Y + 2));
        foreach (char letter in "testacc")
        {
            screen.Credentials.TextEntered(letter);
        }

        Assert.True(screen.Credentials.PointerPressed(true, fields.PasswordField.X + 2,
            fields.PasswordField.Y + 2));
        foreach (char letter in "password1")
        {
            screen.Credentials.TextEntered(letter);
        }

        return screen;
    }

    private static SignedLoginCatalog CreateSignedLocalCatalog(ECDsa signer, int port, DateTimeOffset now)
    {
        byte[] data = Encoding.UTF8.GetBytes(
            "{\"schemaVersion\":1,\"productId\":\"OpenConquer\",\"sequence\":7," +
            "\"issuedAt\":\"2026-10-17T21:00:00Z\",\"expiresAt\":\"2026-10-17T23:00:00Z\"," +
            "\"regions\":[{\"id\":\"local\",\"name\":\"Local\",\"servers\":[{" +
            "\"id\":\"local-realm\",\"name\":\"Local Realm\"," +
            "\"protocolServerName\":\"LocalRealm\",\"accountHost\":\"127.0.0.1\"," +
            "\"accountPort\":" + port + "}]}]}");
        byte[] signature = signer.SignData(data, HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return SignedLoginCatalog.Read(data, signature,
            SignedLoginCatalogTests.Authority(signer), now);
    }

    private static async Task AcceptAccountAndReadNativeReportsAsync(
        TcpListener listener, CancellationToken token)
    {
        using TcpClient client = await listener.AcceptTcpClientAsync(token);
        NetworkStream stream = client.GetStream();
        await stream.WriteAsync(Convert.FromHexString("C54869128E317C0F"), token);
        byte[] request = new byte[276];
        await stream.ReadExactlyAsync(request, token);
        await stream.WriteAsync(Convert.FromHexString(
            "BFF0C21A058F75E70B44B1BAB2056F3EB900E32A626FF42466CAF8503A345F0E9D5053BA"), token);

        // 1100 is 52 bytes and the AccountServer form of 1052 is 28.
        // They share continuous directional encryption and cannot be
        // reconstructed by the GameServer 1052 writer.
        byte[] reports = new byte[52 + 28];
        await stream.ReadExactlyAsync(reports, token);
    }

    private static async Task RefuseAccountAsync(TcpListener listener, CancellationToken token)
    {
        using TcpClient accepted = await listener.AcceptTcpClientAsync(token);
        NetworkStream stream = accepted.GetStream();
        await stream.WriteAsync(Convert.FromHexString("C54869128E317C0F"), token);
        byte[] request = new byte[276];
        await stream.ReadExactlyAsync(request, token);
        await stream.WriteAsync(Convert.FromHexString(
            "BFF0C21A41BC57F6B4980B2239646F3E6D00E32A714C87C66528FBB229345F0E9D5053BA"), token);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);
}
