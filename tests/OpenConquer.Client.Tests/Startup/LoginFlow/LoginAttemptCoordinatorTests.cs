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
using OpenConquer.Networking.Game.Handshake;
using OpenConquer.Networking.Game.Login;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.Startup.LoginFlow;

public sealed class LoginAttemptCoordinatorTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("oc-login-flow-").FullName;

    [Fact]
    public async Task EmptyAccountIsRejectedBeforeRealmAndRetryValidation()
    {
        using LoginScreen login = new(LoginStageLayout.Create(new LogicalRenderSize(1024, 768)),
            new LoginAccountHistory(_folder), "5517", _ => { });
        await using LoginAttemptCoordinator attempts = new(_folder);
        Assert.False(attempts.TryStart(login, null));
        Assert.Equal(LoginAttemptStatus.EnterCredentials, attempts.Status);
        attempts.Update(null);
        Assert.Equal(LoginAttemptStatus.EnterCredentials, attempts.Status);
    }

    [Fact]
    public async Task WorldContentFailureReturnsToRetryableLoginWithAnError()
    {
        using LoginScreen login = new(LoginStageLayout.Create(new LogicalRenderSize(1024, 768)),
            new LoginAccountHistory(_folder), "5517", _ => { });
        await using LoginAttemptCoordinator attempts = new(_folder);

        attempts.FailWorldContent();

        Assert.Equal(LoginAttemptStatus.Unavailable, attempts.Status);
        Assert.Equal(LoginFailureKind.WorldContent, attempts.Feedback.Failure!.Kind);
        login.ApplyFeedback(attempts.Feedback, OpenConquer.Client.Tests.UI.Login.LoginErrorTests.Messages());
        Assert.Equal("Required world content is unavailable.", login.Error.Text);
        Assert.False(attempts.TryStart(login, null));
        Assert.Equal(LoginAttemptStatus.EnterCredentials, attempts.Status);
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
    public async Task RealmChangeDuringSeedCancelsAttemptBeforeCredentialTransmission()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        DateTimeOffset now = new(2026, 10, 17, 22, 0, 0, TimeSpan.Zero);
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SignedLoginCatalog catalog = CreateSignedLocalCatalog(signer, port, now);
        using LoginScreen screen = CreateFilledScreen(catalog);

        TaskCompletionSource<bool> accepted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task peer = HoldAccountSeedAsync(listener, accepted, release, token);

        await using LoginAttemptCoordinator attempts = new(_folder, new FixedClock(now));
        Assert.True(attempts.TryStart(screen, catalog));
        await accepted.Task.WaitAsync(TimeSpan.FromSeconds(5), token);

        screen.Selection.ReplaceCatalog(catalog.View);
        attempts.Update(catalog);
        release.TrySetResult(true);
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

        Assert.Equal(LoginAttemptStatus.Cancelled, attempts.Status);
    }

    private static async Task HoldAccountSeedAsync(TcpListener listener,
        TaskCompletionSource<bool> accepted, TaskCompletionSource<bool> release, CancellationToken token)
    {
        using TcpClient client = await listener.AcceptTcpClientAsync(token);
        accepted.TrySetResult(true);
        await release.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
        // Deliberately no seed bytes: cancelled selection cannot transmit a 1060.
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
        Assert.Equal(20u, attempts.Feedback.Failure!.Code);
        screen.ApplyFeedback(attempts.Feedback, OpenConquer.Client.Tests.UI.Login.LoginErrorTests.Messages());
        Assert.Equal("Failed to login: Server is busy.(20)", screen.Error.Text);
        Assert.False(attempts.TryStart(screen, catalog));
        Assert.Equal(LoginFailureKind.EmptyPassword, attempts.Feedback.Failure!.Kind);

        LoginCredentialLayout fields = LoginCredentialLayout.Create(screen.Layout);
        screen.Credentials.PointerPressed(true, fields.PasswordField.X + 2, fields.PasswordField.Y + 2);
        screen.Credentials.TextEntered('x');
        Assert.False(attempts.TryStart(screen, catalog));
        Assert.Equal(LoginAttemptStatus.RetryLater, attempts.Status);
        Assert.Equal(49995u, attempts.Feedback.Failure!.Code);
    }

    [Fact]
    public async Task UnavailableAccountUsesConnectResourceAndClearsPassword()
    {
        using Socket reserved = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        reserved.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        int port = ((IPEndPoint)reserved.LocalEndPoint!).Port;
        reserved.Dispose();
        DateTimeOffset now = new(2026, 10, 17, 22, 0, 0, TimeSpan.Zero);
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SignedLoginCatalog catalog = CreateSignedLocalCatalog(signer, port, now);
        using LoginScreen screen = CreateFilledScreen(catalog);
        await using LoginAttemptCoordinator attempts = new(_folder, new FixedClock(now));
        Assert.True(attempts.TryStart(screen, catalog));
        await CollectAsync(attempts, catalog);
        Assert.Equal(LoginAttemptStatus.Unavailable, attempts.Status);
        Assert.Equal(10397u, attempts.Feedback.Failure!.Code);
        Assert.Equal(0, screen.Credentials.PasswordLength);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AccountEofAndReadDeadlineUseAccountLossResource(bool silent)
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        DateTimeOffset now = new(2026, 10, 17, 22, 0, 0, TimeSpan.Zero);
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SignedLoginCatalog catalog = CreateSignedLocalCatalog(signer, ((IPEndPoint)listener.LocalEndpoint).Port, now);
        using LoginScreen screen = CreateFilledScreen(catalog);
        await using LoginAttemptCoordinator attempts = new(_folder, new FixedClock(now));
        Assert.True(attempts.TryStart(screen, catalog));
        using TcpClient peer = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken);
        if (!silent)
        {
            peer.Dispose();
        }
        await CollectAsync(attempts, catalog);
        Assert.Equal(silent ? LoginAttemptStatus.Timeout : LoginAttemptStatus.Unavailable, attempts.Status);
        Assert.Equal(100116u, attempts.Feedback.Failure!.Code);
        Assert.Equal(0, screen.Credentials.PasswordLength);
        if (silent)
        {
            byte[] unexpected = new byte[1];
            Assert.Equal(0, await peer.GetStream().ReadAsync(unexpected, TestContext.Current.CancellationToken));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RedirectedGameFailureReachesPresentationAndClosesBothTransports(bool refusal)
    {
        using CancellationTokenSource budget = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(15));
        using TcpListener account = new(IPAddress.Loopback, 0);
        account.Start();
        using TcpListener game = new(IPAddress.Loopback, 0);
        game.Start();
        int gamePort = ((IPEndPoint)game.LocalEndpoint).Port;
        Task gamePeer = refusal
            ? Networking.GameServerHandshakeSessionTests.RefuseCoordinatorLoginAsync(game, budget.Token)
            : Task.CompletedTask;
        if (!refusal)
        {
            game.Stop();
        }
        DateTimeOffset now = new(2026, 10, 17, 22, 0, 0, TimeSpan.Zero);
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SignedLoginCatalog catalog = CreateSignedLocalCatalog(signer, ((IPEndPoint)account.LocalEndpoint).Port, now);
        using LoginScreen screen = CreateFilledScreen(catalog);
        Task accountPeer = RedirectAccountAsync(account, gamePort, budget.Token);
        await using LoginAttemptCoordinator attempts = new(_folder, new FixedClock(now));
        Assert.True(attempts.TryStart(screen, catalog));
        await CollectAsync(attempts, catalog);
        Assert.Equal(refusal ? LoginAttemptStatus.GameRefused : LoginAttemptStatus.Unavailable, attempts.Status);
        if (refusal)
        {
            Assert.Equal(LoginFailureKind.GameRefusal, attempts.Feedback.Failure!.Kind);
            screen.ApplyFeedback(attempts.Feedback, UI.Login.LoginErrorTests.Messages());
            Assert.Equal("The server is full. Please try again later.", screen.Error.Text);
            Assert.Equal(2, screen.Error.Frame);
        }
        else
        {
            Assert.Equal(100100u, attempts.Feedback.Failure!.Code);
        }
        Assert.Equal(0, screen.Credentials.PasswordLength);
        await Task.WhenAll(accountPeer, gamePeer).WaitAsync(budget.Token);
    }

    [Fact]
    public async Task ExistingCharacterWorldAdmission_TransfersGameSessionAndMapSnapshotOnce()
    {
        using CancellationTokenSource budget = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(15));
        using TcpListener account = new(IPAddress.Loopback, 0);
        account.Start();
        using TcpListener game = new(IPAddress.Loopback, 0);
        game.Start();

        Task accountPeer = RedirectAccountAsync(account, ((IPEndPoint)game.LocalEndpoint).Port, budget.Token);
        Task gamePeer = Networking.GameServerHandshakeSessionTests.AcceptedCoordinatorWorldPeerAsync(game, budget.Token);
        DateTimeOffset now = new(2026, 10, 17, 22, 0, 0, TimeSpan.Zero);
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SignedLoginCatalog catalog = CreateSignedLocalCatalog(signer, ((IPEndPoint)account.LocalEndpoint).Port, now);
        using LoginScreen screen = CreateFilledScreen(catalog);
        await using LoginAttemptCoordinator attempts = new(_folder, new FixedClock(now));

        Assert.True(attempts.TryStart(screen, catalog));
        await CollectAsync(attempts, catalog);
        Assert.Equal(LoginAttemptStatus.CharacterReceived, attempts.Status);
        Assert.True(attempts.TryTakeAdmittedWorld(out GameServerHandshakeSession? accepted,
            out GameWorldEntrySnapshot? admitted));
        Assert.NotNull(accepted);
        Assert.NotNull(admitted);
        Assert.Equal(0x11223344u, admitted.Character.EntityId);
        Assert.Equal(1002u, admitted.MapId);
        Assert.Equal(1015u, admitted.MapDataId);
        Assert.False(attempts.TryTakeAdmittedWorld(out _, out _));

        await accepted.DisposeAsync();
        await Task.WhenAll(accountPeer, gamePeer).WaitAsync(budget.Token);
    }

    private static async Task CollectAsync(LoginAttemptCoordinator attempts, SignedLoginCatalog catalog)
    {
        using CancellationTokenSource budget = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(12));
        while (attempts.Status is LoginAttemptStatus.ConnectingAccount or LoginAttemptStatus.ConnectingGame or LoginAttemptStatus.ReadingCharacter)
        {
            attempts.Update(catalog);
            await Task.Delay(10, budget.Token);
        }
    }

    private static async Task RedirectAccountAsync(TcpListener listener, int port, CancellationToken token)
    {
        using TcpClient client = await listener.AcceptTcpClientAsync(token);
        NetworkStream stream = client.GetStream();
        await stream.WriteAsync(Convert.FromHexString("C54869128E317C0F"), token);
        byte[] request = new byte[276];
        await stream.ReadExactlyAsync(request, token);
        byte[] redirect = Convert.FromHexString("BFF0C21A058F75E70B44B1BAB2056F3EB900E32A626FF42466CAF8503A345F0E9D5053BA");
        // Patch only the port in the independent 1055 fixture (original 5816).
        // The account inverse cipher rotates plaintext XOR differences by four bits.
        for (int index = 0; index < 4; index++)
        {
            byte difference = (byte)((5816 ^ port) >> (8 * index));
            redirect[12 + index] ^= (byte)((difference << 4) | (difference >> 4));
        }
        await stream.WriteAsync(redirect, token);
        byte[] reports = new byte[80];
        await stream.ReadExactlyAsync(reports, token);
        Assert.Equal(0, await stream.ReadAsync(new byte[1], token));
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
