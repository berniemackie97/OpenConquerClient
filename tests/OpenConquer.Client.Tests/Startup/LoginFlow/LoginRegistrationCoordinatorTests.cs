using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using OpenConquer.Client.Startup.Catalog;
using OpenConquer.Client.Startup.LoginFlow;
using OpenConquer.Client.Tests.Networking;
using OpenConquer.Client.Tests.Startup.Catalog;
using OpenConquer.Client.UI.Login;
using OpenConquer.Client.UI.Login.Account;
using OpenConquer.Client.UI.Login.Credentials;
using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Client.UI.Login.Status;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.Startup.LoginFlow;

public sealed class LoginRegistrationCoordinatorTests : IDisposable
{
    private static readonly DateTimeOffset s_now = new(2026, 10, 17, 22, 0, 0, TimeSpan.Zero);
    private readonly string _folder = Directory.CreateTempSubdirectory("oc-registration-flow-").FullName;

    [Fact]
    public async Task SignUpTogglesTheFlowAndCancelKeepsNothing()
    {
        using LoginScreen screen = CreateScreen(null, "newuser", "secret1");
        await using LoginRegistrationCoordinator registration = new();

        registration.Toggle();
        Assert.Equal(LoginRegistrationStep.EnterCredentials, registration.Step);
        Assert.Equal(LoginRegistrationNotice.Begin, Notice(registration));

        Assert.True(registration.TrySubmit(screen, null));
        Assert.Equal(LoginRegistrationStep.ConfirmPassword, registration.Step);

        registration.Toggle();
        Assert.Equal(LoginRegistrationStep.None, registration.Step);
        Assert.Null(registration.Feedback.Notice);
        Assert.False(registration.TrySubmit(screen, null));
    }

    [Theory]
    [InlineData("", "secret1", (int)LoginFailureKind.EmptyAccount, 0u)]
    [InlineData("newuser", "", (int)LoginFailureKind.EmptyPassword, 0u)]
    [InlineData("abc", "secret1", (int)LoginFailureKind.RegistrationNotice, (uint)LoginRegistrationNotice.InvalidAccountName)]
    [InlineData("abcdefghijklmnopq", "secret1", (int)LoginFailureKind.RegistrationNotice, (uint)LoginRegistrationNotice.InvalidAccountName)]
    [InlineData("newuser", "12345", (int)LoginFailureKind.RegistrationNotice, (uint)LoginRegistrationNotice.InvalidPassword)]
    [InlineData("newuser", "123456789012345", (int)LoginFailureKind.RegistrationNotice, (uint)LoginRegistrationNotice.InvalidPassword)]
    public async Task FirstEntryEnforcesPolicyAndKeepsTheStep(string account, string password, int kind, uint code)
    {
        using LoginScreen screen = CreateScreen(null, account, password);
        await using LoginRegistrationCoordinator registration = new();
        registration.Toggle();

        Assert.False(registration.TrySubmit(screen, null));

        Assert.Equal(LoginRegistrationStep.EnterCredentials, registration.Step);
        Assert.Equal((LoginFailureKind)kind, registration.Feedback.Notice!.Kind);
        Assert.Equal(code, registration.Feedback.Notice.Code);
    }

    [Fact]
    public async Task MismatchedConfirmationRestartsWithoutSending()
    {
        using LoginScreen screen = CreateScreen(null, "newuser", "secret1");
        await using LoginRegistrationCoordinator registration = new();
        registration.Toggle();
        Assert.True(registration.TrySubmit(screen, null));
        Assert.Equal(0, screen.Credentials.PasswordLength);

        Type(screen, password: "secret2");
        Assert.False(registration.TrySubmit(screen, null));

        Assert.Equal(LoginRegistrationStep.EnterCredentials, registration.Step);
        Assert.Equal(LoginRegistrationNotice.PasswordMismatch, Notice(registration));
        Assert.Equal(0, screen.Credentials.PasswordLength);
    }

    [Fact]
    public async Task EditedAccountBetweenEntriesStartsOver()
    {
        using LoginScreen screen = CreateScreen(null, "newuser", "secret1");
        await using LoginRegistrationCoordinator registration = new();
        registration.Toggle();
        Assert.True(registration.TrySubmit(screen, null));

        Type(screen, account: "x", password: "secret1");
        Assert.False(registration.TrySubmit(screen, null));

        Assert.Equal(LoginRegistrationStep.EnterCredentials, registration.Step);
        Assert.Equal(LoginRegistrationNotice.Begin, Notice(registration));
    }

    [Fact]
    public async Task ConfirmedEntryWithoutUsableRealmUsesTheLoginRealmResource()
    {
        using LoginScreen screen = CreateScreen(null, "newuser", "secret1");
        await using LoginRegistrationCoordinator registration = new();
        registration.Toggle();
        Assert.True(registration.TrySubmit(screen, null));
        Type(screen, password: "secret1");

        Assert.False(registration.TrySubmit(screen, null));

        Assert.Equal(LoginRegistrationStep.EnterCredentials, registration.Step);
        Assert.Equal(LoginFailureKind.Resource, registration.Feedback.Notice!.Kind);
        Assert.Equal(10012u, registration.Feedback.Notice.Code);
    }

    [Fact]
    public async Task CreatedAccountLeavesTheFlowWithTheNamePrefilled()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener listener = Listen();
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SignedLoginCatalog catalog = CreateSignedLocalCatalog(signer, Port(listener));
        using LoginScreen screen = CreateScreen(catalog, "testacc", "password1");
        Task<byte[]> peer = RegisterPeerAsync(listener, 0, token);

        await using LoginRegistrationCoordinator registration = new(new FixedClock(s_now));
        registration.Toggle();
        Assert.True(registration.TrySubmit(screen, catalog));
        Type(screen, password: "password1");
        Assert.True(registration.TrySubmit(screen, catalog));
        Assert.Equal(LoginRegistrationStep.Submitting, registration.Step);
        Assert.Equal(0, screen.Credentials.PasswordLength);

        byte[] request = await peer.WaitAsync(TimeSpan.FromSeconds(5), token);
        await CollectAsync(registration, catalog);

        Assert.Equal(LoginRegistrationStep.None, registration.Step);
        Assert.Equal(LoginRegistrationNotice.Created, Notice(registration));
        Assert.Equal("testacc", screen.Credentials.AccountDisplay);
        AccountServerTestWire.DecodeClientRequest(request);
        Assert.Equal(0xF001, BinaryPrimitives.ReadUInt16LittleEndian(request.AsSpan(2)));
        Assert.Equal(AccountServerTestWire.TestAccountCredential, Convert.ToHexString(request.AsSpan(132, 32)));
    }

    [Theory]
    [InlineData(1u, (int)LoginRegistrationNotice.Rejected)]
    [InlineData(2u, (int)LoginRegistrationNotice.AccountNameTaken)]
    [InlineData(3u, (int)LoginRegistrationNotice.Failed)]
    [InlineData(4u, (int)LoginRegistrationNotice.Unavailable)]
    public async Task RefusedRegistrationReturnsToEntry(uint code, int expected)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener listener = Listen();
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SignedLoginCatalog catalog = CreateSignedLocalCatalog(signer, Port(listener));
        using LoginScreen screen = CreateScreen(catalog, "testacc", "password1");
        Task<byte[]> peer = RegisterPeerAsync(listener, code, token);

        await using LoginRegistrationCoordinator registration = new(new FixedClock(s_now));
        await SubmitConfirmedAsync(registration, screen, catalog);
        await peer.WaitAsync(TimeSpan.FromSeconds(5), token);
        await CollectAsync(registration, catalog);

        Assert.Equal(LoginRegistrationStep.EnterCredentials, registration.Step);
        Assert.Equal((LoginRegistrationNotice)expected, Notice(registration));
    }

    [Fact]
    public async Task UnreachableAccountServerUsesTheNativeConnectResource()
    {
        int port;
        using (TcpListener closed = Listen())
        {
            port = Port(closed);
        }

        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SignedLoginCatalog catalog = CreateSignedLocalCatalog(signer, port);
        using LoginScreen screen = CreateScreen(catalog, "testacc", "password1");

        await using LoginRegistrationCoordinator registration = new(new FixedClock(s_now));
        await SubmitConfirmedAsync(registration, screen, catalog);
        await CollectAsync(registration, catalog);

        Assert.Equal(LoginRegistrationStep.EnterCredentials, registration.Step);
        Assert.Equal(LoginFailureKind.Resource, registration.Feedback.Notice!.Kind);
        Assert.Equal(10397u, registration.Feedback.Notice.Code);
    }

    [Fact]
    public async Task LostReplyClaimsNoOutcomeAndUsesTheNativeLossResource()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener listener = Listen();
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SignedLoginCatalog catalog = CreateSignedLocalCatalog(signer, Port(listener));
        using LoginScreen screen = CreateScreen(catalog, "testacc", "password1");
        Task peer = ClosingPeerAsync(listener, token);

        await using LoginRegistrationCoordinator registration = new(new FixedClock(s_now));
        await SubmitConfirmedAsync(registration, screen, catalog);
        await peer.WaitAsync(TimeSpan.FromSeconds(5), token);
        await CollectAsync(registration, catalog);

        Assert.Equal(LoginRegistrationStep.EnterCredentials, registration.Step);
        Assert.Equal(100116u, registration.Feedback.Notice!.Code);
    }

    [Fact]
    public async Task DisposedCoordinatorRejectsUse()
    {
        using LoginScreen screen = CreateScreen(null, "newuser", "secret1");
        LoginRegistrationCoordinator registration = new();
        registration.Toggle();
        Assert.True(registration.TrySubmit(screen, null));
        await registration.DisposeAsync();
        await registration.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(registration.Toggle);
        Assert.Throws<ObjectDisposedException>(() => registration.TrySubmit(screen, null));
    }

    private static LoginRegistrationNotice Notice(LoginRegistrationCoordinator registration)
    {
        LoginFailure notice = Assert.IsType<LoginFailure>(registration.Feedback.Notice);
        Assert.Equal(LoginFailureKind.RegistrationNotice, notice.Kind);
        return (LoginRegistrationNotice)notice.Code;
    }

    private static async Task SubmitConfirmedAsync(LoginRegistrationCoordinator registration, LoginScreen screen, SignedLoginCatalog catalog)
    {
        registration.Toggle();
        Assert.True(registration.TrySubmit(screen, catalog));
        Type(screen, password: "password1");
        Assert.True(registration.TrySubmit(screen, catalog));
        await Task.CompletedTask;
    }

    private static async Task CollectAsync(LoginRegistrationCoordinator registration, SignedLoginCatalog catalog)
    {
        using CancellationTokenSource budget = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(12));
        while (registration.Step == LoginRegistrationStep.Submitting)
        {
            registration.Update(catalog);
            await Task.Delay(10, budget.Token);
        }
    }

    private LoginScreen CreateScreen(SignedLoginCatalog? catalog, string account, string password)
    {
        LoginStageLayout layout = LoginStageLayout.Create(new LogicalRenderSize(1024, 768));
        LoginScreen screen = new(layout, new LoginAccountHistory(_folder), "5517", _ => { });
        if (catalog is not null)
        {
            screen.Selection.ReplaceCatalog(catalog.View);
            Assert.True(screen.Selection.SelectRegionSlot(0));
            Assert.True(screen.Selection.SelectServerSlot(0));
        }

        screen.Advance(TimeSpan.Zero);
        Type(screen, account, password);
        return screen;
    }

    private static void Type(LoginScreen screen, string? account = null, string? password = null)
    {
        LoginCredentialLayout fields = LoginCredentialLayout.Create(screen.Layout);
        if (account is not null)
        {
            Assert.True(screen.Credentials.PointerPressed(true, fields.AccountField.X + 2, fields.AccountField.Y + 2));
            while (screen.Credentials.AccountLength > 0)
            {
                screen.Credentials.KeyPressed(new(Platform.Windowing.Desktop.DesktopKey.End, default, false));
                screen.Credentials.KeyPressed(new(Platform.Windowing.Desktop.DesktopKey.Backspace, default, false));
            }

            foreach (char letter in account)
            {
                screen.Credentials.TextEntered(letter);
            }
        }

        if (password is not null)
        {
            Assert.True(screen.Credentials.PointerPressed(true, fields.PasswordField.X + 2, fields.PasswordField.Y + 2));
            foreach (char letter in password)
            {
                screen.Credentials.TextEntered(letter);
            }
        }
    }

    private static TcpListener Listen()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        return listener;
    }

    private static int Port(TcpListener listener) => ((IPEndPoint)listener.LocalEndpoint).Port;

    private static async Task<byte[]> RegisterPeerAsync(TcpListener listener, uint code, CancellationToken token)
    {
        using TcpClient client = await listener.AcceptTcpClientAsync(token);
        NetworkStream stream = client.GetStream();
        await stream.WriteAsync(Convert.FromHexString(AccountServerTestWire.SeedWire), token);
        byte[] request = new byte[260];
        await stream.ReadExactlyAsync(request, token);
        await stream.WriteAsync(AccountServerTestWire.RegistrationResultWire(code), token);
        return request;
    }

    private static async Task ClosingPeerAsync(TcpListener listener, CancellationToken token)
    {
        using TcpClient client = await listener.AcceptTcpClientAsync(token);
        NetworkStream stream = client.GetStream();
        await stream.WriteAsync(Convert.FromHexString(AccountServerTestWire.SeedWire), token);
        await stream.ReadExactlyAsync(new byte[260], token);
    }

    private static SignedLoginCatalog CreateSignedLocalCatalog(ECDsa signer, int port)
    {
        byte[] data = Encoding.UTF8.GetBytes(
            "{\"schemaVersion\":1,\"productId\":\"OpenConquer\",\"sequence\":7," +
            "\"issuedAt\":\"2026-10-17T21:00:00Z\",\"expiresAt\":\"2026-10-17T23:00:00Z\"," +
            "\"regions\":[{\"id\":\"local\",\"name\":\"Local\",\"servers\":[{" +
            "\"id\":\"local-realm\",\"name\":\"Local Realm\"," +
            "\"protocolServerName\":\"LocalRealm\",\"accountHost\":\"127.0.0.1\"," +
            "\"accountPort\":" + port + "}]}]}");
        byte[] signature = signer.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return SignedLoginCatalog.Read(data, signature, SignedLoginCatalogTests.Authority(signer), s_now);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);
}
