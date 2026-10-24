using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using OpenConquer.Client.Startup.Catalog;
using OpenConquer.Client.Startup.LoginFlow;
using OpenConquer.Client.Tests.Networking;
using OpenConquer.Client.Tests.Startup.Catalog;
using OpenConquer.Client.Tests.UI.Login.Registration;
using OpenConquer.Client.UI.Login.Registration;
using OpenConquer.Client.UI.Login.Selection;
using OpenConquer.Client.UI.Login.Status;

namespace OpenConquer.Client.Tests.Startup.LoginFlow;

public sealed class LoginRegistrationCoordinatorTests
{
    private static readonly DateTimeOffset s_now = new(2026, 10, 17, 22, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task OpeningWithoutAUsableRealmRefusesWithErrorText()
    {
        await using LoginRegistrationCoordinator registration = new(new FixedClock(s_now));

        registration.Open(new LoginServerSelection(), null);

        Assert.Equal(LoginRegistrationStatus.Closed, registration.Status);
        Assert.Equal(LoginRegistrationNotice.ServerNotSelected, Notice(registration));
    }

    [Fact]
    public async Task OpeningForAUsableRealmEditsAndCloseLeavesNothingBehind()
    {
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SignedLoginCatalog catalog = CreateSignedLocalCatalog(signer, 1);
        await using LoginRegistrationCoordinator registration = new(new FixedClock(s_now));

        registration.Open(Select(catalog), catalog);
        Assert.Equal(LoginRegistrationStatus.Editing, registration.Status);
        Assert.Null(registration.Feedback.Notice);

        LoginRegistrationFeedback editing = registration.Feedback;
        registration.Open(Select(catalog), catalog);
        Assert.Same(editing, registration.Feedback);

        registration.Close();
        Assert.Equal(LoginRegistrationStatus.Closed, registration.Status);
        Assert.Null(registration.Feedback.Notice);
        using LoginRegistrationForm form = LoginRegistrationFormTests.Open();
        Assert.False(registration.TrySubmit(form, Select(catalog), catalog));
    }

    [Theory]
    [InlineData("", "secret1", "secret1", (int)LoginRegistrationNotice.InvalidAccountName, (int)LoginRegistrationField.Account)]
    [InlineData("abc", "secret1", "secret1", (int)LoginRegistrationNotice.InvalidAccountName, (int)LoginRegistrationField.Account)]
    [InlineData("newuser", "", "", (int)LoginRegistrationNotice.InvalidPassword, (int)LoginRegistrationField.Password)]
    [InlineData("newuser", "12345", "12345", (int)LoginRegistrationNotice.InvalidPassword, (int)LoginRegistrationField.Password)]
    [InlineData("newuser", "secret1", "secret2", (int)LoginRegistrationNotice.PasswordMismatch, (int)LoginRegistrationField.Password)]
    [InlineData("newuser", "secret1", "", (int)LoginRegistrationNotice.PasswordMismatch, (int)LoginRegistrationField.Password)]
    public async Task EntriesAreCheckedInFieldOrderWithoutSending(string account, string password, string confirmation, int notice, int field)
    {
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SignedLoginCatalog catalog = CreateSignedLocalCatalog(signer, 1);
        LoginServerSelection selection = Select(catalog);
        await using LoginRegistrationCoordinator registration = new(new FixedClock(s_now));
        registration.Open(selection, catalog);
        using LoginRegistrationForm form = LoginRegistrationFormTests.Open();
        LoginRegistrationFormTests.Fill(form, account, password, confirmation);

        Assert.False(registration.TrySubmit(form, selection, catalog));

        Assert.Equal(LoginRegistrationStatus.Editing, registration.Status);
        Assert.Equal((LoginRegistrationNotice)notice, Notice(registration));
        Assert.Equal((LoginRegistrationField)field, registration.Feedback.Focus);
        Assert.Equal(password.Length, form.Field(LoginRegistrationField.Password).Length);
    }

    [Fact]
    public async Task ValidEntriesWithoutAUsableRealmStayInTheDialog()
    {
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SignedLoginCatalog catalog = CreateSignedLocalCatalog(signer, 1);
        await using LoginRegistrationCoordinator registration = new(new FixedClock(s_now));
        registration.Open(Select(catalog), catalog);
        using LoginRegistrationForm form = LoginRegistrationFormTests.Open();
        LoginRegistrationFormTests.Fill(form, "newuser", "secret1", "secret1");

        Assert.False(registration.TrySubmit(form, new LoginServerSelection(), null));

        Assert.Equal(LoginRegistrationStatus.Editing, registration.Status);
        Assert.Equal(LoginRegistrationNotice.ServerNotSelected, Notice(registration));
        Assert.Equal(7, form.Field(LoginRegistrationField.Password).Length);
    }

    [Fact]
    public async Task CreatedAccountSendsOneRequestAndReportsCreation()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener listener = Listen();
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SignedLoginCatalog catalog = CreateSignedLocalCatalog(signer, Port(listener));
        Task<byte[]> peer = RegisterPeerAsync(listener, 0, token);
        await using LoginRegistrationCoordinator registration = new(new FixedClock(s_now));
        using LoginRegistrationForm form = LoginRegistrationFormTests.Open();

        LoginServerSelection selection = Submit(registration, form, catalog);
        Assert.Equal(LoginRegistrationStatus.Submitting, registration.Status);
        Assert.Equal(0, form.Field(LoginRegistrationField.Password).Length);
        Assert.Equal(0, form.Field(LoginRegistrationField.Confirm).Length);
        Assert.False(registration.TrySubmit(form, selection, catalog));

        byte[] request = await peer.WaitAsync(TimeSpan.FromSeconds(5), token);
        await CollectAsync(registration, catalog);

        Assert.Equal(LoginRegistrationStatus.Created, registration.Status);
        Assert.Equal(LoginRegistrationNotice.Created, Notice(registration));
        Assert.Equal("testacc", form.AccountName);
        AccountServerTestWire.DecodeClientRequest(request);
        Assert.Equal(0xF001, BinaryPrimitives.ReadUInt16LittleEndian(request.AsSpan(2)));
        Assert.Equal(AccountServerTestWire.TestAccountCredential, Convert.ToHexString(request.AsSpan(132, 32)));
    }

    [Theory]
    [InlineData(1u, (int)LoginRegistrationNotice.Rejected, (int)LoginRegistrationField.Account)]
    [InlineData(2u, (int)LoginRegistrationNotice.AccountNameTaken, (int)LoginRegistrationField.Account)]
    [InlineData(3u, (int)LoginRegistrationNotice.Failed, (int)LoginRegistrationField.Password)]
    [InlineData(4u, (int)LoginRegistrationNotice.Unavailable, (int)LoginRegistrationField.Password)]
    public async Task RefusedRegistrationReturnsToTheDialog(uint code, int notice, int field)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener listener = Listen();
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SignedLoginCatalog catalog = CreateSignedLocalCatalog(signer, Port(listener));
        Task<byte[]> peer = RegisterPeerAsync(listener, code, token);
        await using LoginRegistrationCoordinator registration = new(new FixedClock(s_now));
        using LoginRegistrationForm form = LoginRegistrationFormTests.Open();

        Submit(registration, form, catalog);
        await peer.WaitAsync(TimeSpan.FromSeconds(5), token);
        await CollectAsync(registration, catalog);

        Assert.Equal(LoginRegistrationStatus.Editing, registration.Status);
        Assert.Equal((LoginRegistrationNotice)notice, Notice(registration));
        Assert.Equal((LoginRegistrationField)field, registration.Feedback.Focus);
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
        await using LoginRegistrationCoordinator registration = new(new FixedClock(s_now));
        using LoginRegistrationForm form = LoginRegistrationFormTests.Open();

        Submit(registration, form, catalog);
        await CollectAsync(registration, catalog);

        Assert.Equal(LoginRegistrationStatus.Editing, registration.Status);
        Assert.Equal(LoginFailureKind.Resource, registration.Feedback.Notice!.Kind);
        Assert.Equal(10397u, registration.Feedback.Notice.Code);
        Assert.Equal(LoginRegistrationField.Password, registration.Feedback.Focus);
    }

    [Fact]
    public async Task LostReplyClaimsNoOutcome()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener listener = Listen();
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SignedLoginCatalog catalog = CreateSignedLocalCatalog(signer, Port(listener));
        Task peer = ClosingPeerAsync(listener, token);
        await using LoginRegistrationCoordinator registration = new(new FixedClock(s_now));
        using LoginRegistrationForm form = LoginRegistrationFormTests.Open();

        Submit(registration, form, catalog);
        await peer.WaitAsync(TimeSpan.FromSeconds(5), token);
        await CollectAsync(registration, catalog);

        Assert.Equal(LoginRegistrationStatus.Editing, registration.Status);
        Assert.Equal(LoginRegistrationNotice.OutcomeUnknown, Notice(registration));
    }

    [Fact]
    public async Task RevokedCatalogCancelsTheRequestAndClaimsNoOutcome()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        using TcpListener listener = Listen();
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SignedLoginCatalog catalog = CreateSignedLocalCatalog(signer, Port(listener));
        TaskCompletionSource received = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task peer = SilentPeerAsync(listener, received, token);
        await using LoginRegistrationCoordinator registration = new(new FixedClock(s_now));
        using LoginRegistrationForm form = LoginRegistrationFormTests.Open();

        Submit(registration, form, catalog);
        await received.Task.WaitAsync(TimeSpan.FromSeconds(5), token);
        await CollectAsync(registration, null);
        await peer.WaitAsync(TimeSpan.FromSeconds(5), token);

        Assert.Equal(LoginRegistrationStatus.Editing, registration.Status);
        Assert.Equal(LoginRegistrationNotice.OutcomeUnknown, Notice(registration));
    }

    [Fact]
    public async Task DisposedCoordinatorRejectsUse()
    {
        using LoginRegistrationForm form = LoginRegistrationFormTests.Open();
        LoginRegistrationCoordinator registration = new();
        await registration.DisposeAsync();
        await registration.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => registration.Open(new LoginServerSelection(), null));
        Assert.Throws<ObjectDisposedException>(registration.Close);
        Assert.Throws<ObjectDisposedException>(() => registration.TrySubmit(form, new LoginServerSelection(), null));
    }

    [Theory]
    [InlineData("abcd", true)]
    [InlineData("abcdefghijklmnop", true)]
    [InlineData("abc", false)]
    [InlineData("abcdefghijklmnopq", false)]
    [InlineData("ab cd", false)]
    [InlineData("abcé", false)]
    public void AccountPolicyChecksLengthAndAlphabet(string name, bool valid) =>
        Assert.Equal(valid, LoginRegistrationPolicy.IsValidAccount(name));

    [Theory]
    [InlineData("secret", true)]
    [InlineData("12345678901234", true)]
    [InlineData("12345", false)]
    [InlineData("123456789012345", false)]
    [InlineData("secret 1", false)]
    public void PasswordPolicyChecksLengthAndAlphabet(string password, bool valid) =>
        Assert.Equal(valid, LoginRegistrationPolicy.IsValidPassword(password));

    private static LoginRegistrationNotice Notice(LoginRegistrationCoordinator registration)
    {
        LoginFailure notice = Assert.IsType<LoginFailure>(registration.Feedback.Notice);
        Assert.Equal(LoginFailureKind.RegistrationNotice, notice.Kind);
        return (LoginRegistrationNotice)notice.Code;
    }

    /// <summary>Opens the flow and submits the account the shared credential vector was computed for.</summary>
    private static LoginServerSelection Submit(LoginRegistrationCoordinator registration, LoginRegistrationForm form, SignedLoginCatalog catalog)
    {
        LoginServerSelection selection = Select(catalog);
        registration.Open(selection, catalog);
        LoginRegistrationFormTests.Fill(form, "testacc", "password1", "password1");
        Assert.True(registration.TrySubmit(form, selection, catalog));
        return selection;
    }

    private static async Task CollectAsync(LoginRegistrationCoordinator registration, SignedLoginCatalog? catalog)
    {
        using CancellationTokenSource budget = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(12));
        while (registration.Status == LoginRegistrationStatus.Submitting)
        {
            registration.Update(catalog);
            await Task.Delay(10, budget.Token);
        }
    }

    private static LoginServerSelection Select(SignedLoginCatalog catalog)
    {
        LoginServerSelection selection = new();
        selection.ReplaceCatalog(catalog.View);
        Assert.True(selection.SelectRegionSlot(0));
        Assert.True(selection.SelectServerSlot(0));
        return selection;
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

    /// <summary>Takes the request and never answers; returns once the client closes the connection.</summary>
    private static async Task SilentPeerAsync(TcpListener listener, TaskCompletionSource received, CancellationToken token)
    {
        using TcpClient client = await listener.AcceptTcpClientAsync(token);
        NetworkStream stream = client.GetStream();
        await stream.WriteAsync(Convert.FromHexString(AccountServerTestWire.SeedWire), token);
        await stream.ReadExactlyAsync(new byte[260], token);
        received.SetResult();
        try
        {
            while (await stream.ReadAsync(new byte[1], token) > 0)
            {
            }
        }
        catch (IOException)
        {
            // A reset is as good as a close.
        }
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
}
