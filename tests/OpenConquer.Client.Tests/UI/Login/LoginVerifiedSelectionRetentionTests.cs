using System.Security.Cryptography;
using System.Text.Json;
using OpenConquer.Client.Startup.Catalog;
using OpenConquer.Client.Tests.Startup.Catalog;
using OpenConquer.Client.UI.Login;
using OpenConquer.Platform.Windowing.Desktop;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Login;

public sealed class LoginVerifiedSelectionRetentionTests
{
    private static readonly DateTimeOffset s_now = new(2026, 10, 11, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void InitialAcquisitionCannotSelectARealmWithoutUserAction()
    {
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SignedLoginCatalog current = Catalog(signer, 7);
        (LoginServerSelection selection, LoginSelectionInput input) = CreateInput();

        LoginVerifiedSelectionRetention.Apply(null, current, s_now, input, selection);

        Assert.Same(current.View, selection.Catalog);
        Assert.Null(selection.SelectedRegion);
        Assert.Null(selection.SelectedServer);
    }

    [Fact]
    public void AuthenticatedRenewalRetainsAccountAndChoiceButZeroesPassword()
    {
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SignedLoginCatalog previous = Catalog(signer, 7);
        SignedLoginCatalog current = Catalog(signer, 8);
        (LoginServerSelection selection, LoginSelectionInput input) = CreateInput();
        LoginVerifiedSelectionRetention.Apply(null, previous, s_now, input, selection);
        SelectFirst(selection);
        LoginCredentialLayout layout = LoginCredentialLayout.Create(LoginVisualLayout.Create(new LogicalRenderSize(1024, 768)));
        using LoginCredentialInput credentials = new(selection, layout);
        Assert.True(credentials.PointerPressed(true, layout.AccountField.X + 1, layout.AccountField.Y + 1));
        credentials.TextEntered('A');
        Assert.True(credentials.KeyPressed(new DesktopKeyEvent(DesktopKey.Tab, DesktopKeyModifiers.None, false)));
        credentials.TextEntered('P');
        Assert.Equal(1, credentials.PasswordLength);

        LoginVerifiedSelectionRetention.Apply(previous, current, s_now, input, selection);
        credentials.Synchronize();

        Assert.Same(current.View, selection.Catalog);
        Assert.Same(current.View.Regions[0], selection.SelectedRegion);
        Assert.Same(current.View.Regions[0].Servers[0], selection.SelectedServer);
        // Revalidated signed-directory data may preserve the account name, never the password.
        // Selection remains bound only when signed endpoint identity is unchanged.
        Assert.Equal("A", credentials.AccountDisplay);
        Assert.Equal(1, credentials.AccountLength);
        Assert.Equal(0, credentials.PasswordLength);
        Assert.Equal(LoginCredentialFocus.Password, credentials.Focus);
        Assert.True(credentials.Enabled);
    }

    [Fact]
    public void StableIdentifiersCanMoveAcrossRegionAndServerPages()
    {
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SignedLoginCatalog previous = Catalog(signer, 7);
        SignedLoginCatalog current = Catalog(signer, 8, regionIndex: 16, serverIndex: 12);
        (LoginServerSelection selection, LoginSelectionInput input) = CreateInput();
        LoginVerifiedSelectionRetention.Apply(null, previous, s_now, input, selection);
        SelectFirst(selection);

        LoginVerifiedSelectionRetention.Apply(previous, current, s_now, input, selection);

        Assert.Equal(1, selection.RegionPage);
        Assert.Equal(1, selection.ServerPage);
        Assert.Equal(16, selection.SelectedRegionIndex);
        Assert.Equal(12, selection.SelectedServerIndex);
        Assert.Equal(0, selection.SelectedRegionSlot);
        Assert.Equal(0, selection.SelectedServerSlot);
        Assert.Equal("local", selection.SelectedRegion?.Id);
        Assert.Equal("local-realm", selection.SelectedServer?.Id);
    }

    [Theory]
    [InlineData("new-host.example.org", 9958, "LocalRealm", "g01s01", "Local Development", "OpenConquer Local", false)]
    [InlineData("127.0.0.1", 9959, "LocalRealm", "g01s01", "Local Development", "OpenConquer Local", false)]
    [InlineData("127.0.0.1", 9958, "OtherRealm", "g01s01", "Local Development", "OpenConquer Local", false)]
    [InlineData("127.0.0.1", 9958, "LocalRealm", "g01s02", "Local Development", "OpenConquer Local", false)]
    [InlineData("127.0.0.1", 9958, "LocalRealm", "g01s01", "Local Development", "Other Local", false)]
    [InlineData("127.0.0.1", 9958, "LocalRealm", "g01s01", "Local Development", "OpenConquer Local", true)]
    public void ChangedOrRemovedServerCannotRetainASelectedEndpoint(string host, int port, string protocol, string iconId, string regionName, string serverName, bool removeServer)
    {
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SignedLoginCatalog previous = Catalog(signer, 7);
        SignedLoginCatalog current = Catalog(signer, 8, host: host, port: port, protocol: protocol, iconId: iconId,
            regionName: regionName, serverName: serverName, includeServer: !removeServer);
        (LoginServerSelection selection, LoginSelectionInput input) = CreateInput();
        LoginVerifiedSelectionRetention.Apply(null, previous, s_now, input, selection);
        SelectFirst(selection);

        LoginVerifiedSelectionRetention.Apply(previous, current, s_now, input, selection);

        Assert.Equal("local", selection.SelectedRegion?.Id);
        Assert.Null(selection.SelectedServer);
    }

    [Theory]
    [InlineData("Other Region", "local")]
    [InlineData("Local Development", "other-region")]
    public void RegionIdentityChangesInvalidateBothSelections(string regionName, string regionId)
    {
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SignedLoginCatalog previous = Catalog(signer, 7);
        SignedLoginCatalog current = Catalog(signer, 8, regionName: regionName, regionId: regionId);
        (LoginServerSelection selection, LoginSelectionInput input) = CreateInput();
        LoginVerifiedSelectionRetention.Apply(null, previous, s_now, input, selection);
        SelectFirst(selection);

        LoginVerifiedSelectionRetention.Apply(previous, current, s_now, input, selection);

        Assert.Null(selection.SelectedRegion);
        Assert.Null(selection.SelectedServer);
    }

    [Fact]
    public void ExpiryAndReacquisitionRequireExplicitReselection()
    {
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SignedLoginCatalog previous = Catalog(signer, 7);
        SignedLoginCatalog current = Catalog(signer, 8);
        (LoginServerSelection selection, LoginSelectionInput input) = CreateInput();
        LoginVerifiedSelectionRetention.Apply(null, previous, s_now, input, selection);
        SelectFirst(selection);

        LoginVerifiedSelectionRetention.Apply(previous, null, s_now, input, selection);
        Assert.Empty(selection.Catalog.Regions);
        Assert.Null(selection.SelectedRegion);
        LoginVerifiedSelectionRetention.Apply(null, current, s_now, input, selection);
        Assert.Same(current.View, selection.Catalog);
        Assert.Null(selection.SelectedRegion);
        Assert.Null(selection.SelectedServer);
    }

    [Fact]
    public void ExpiredOriginalCannotCarryAnOldChoiceIntoAnImmediatelyAvailableRenewal()
    {
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SignedLoginCatalog previous = Catalog(signer, 7);
        SignedLoginCatalog current = Catalog(signer, 8, expiresAt: "2026-10-11T23:00:00Z");
        (LoginServerSelection selection, LoginSelectionInput input) = CreateInput();
        LoginVerifiedSelectionRetention.Apply(null, previous, s_now, input, selection);
        SelectFirst(selection);

        LoginVerifiedSelectionRetention.Apply(previous, current, previous.ExpiresAt, input, selection);

        Assert.Same(current.View, selection.Catalog);
        Assert.Null(selection.SelectedRegion);
        Assert.Null(selection.SelectedServer);
    }

    [Fact]
    public void ExpiredIncomingPublicationIsNotPresentedEvenIfItsSignatureWasValid()
    {
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SignedLoginCatalog previous = Catalog(signer, 7);
        SignedLoginCatalog current = Catalog(signer, 8);
        (LoginServerSelection selection, LoginSelectionInput input) = CreateInput();
        LoginVerifiedSelectionRetention.Apply(null, previous, s_now, input, selection);
        SelectFirst(selection);

        LoginVerifiedSelectionRetention.Apply(previous, current, current.ExpiresAt, input, selection);

        Assert.Empty(selection.Catalog.Regions);
        Assert.Null(selection.SelectedServer);
    }

    [Fact]
    public void UnrelatedOrStalePreviousCatalogCannotRebindASelection()
    {
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SignedLoginCatalog previous = Catalog(signer, 7);
        SignedLoginCatalog unrelated = Catalog(signer, 9);
        SignedLoginCatalog current = Catalog(signer, 10);
        (LoginServerSelection selection, LoginSelectionInput input) = CreateInput();
        LoginVerifiedSelectionRetention.Apply(null, previous, s_now, input, selection);
        SelectFirst(selection);

        LoginVerifiedSelectionRetention.Apply(unrelated, current, s_now, input, selection);

        Assert.Same(current.View, selection.Catalog);
        Assert.Null(selection.SelectedRegion);
        Assert.Null(selection.SelectedServer);
    }

    [Fact]
    public void RenewingCatalogCancelsPendingPointerCaptureEvenWhenChoiceIsRestored()
    {
        using ECDsa signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        SignedLoginCatalog previous = Catalog(signer, 7);
        SignedLoginCatalog current = Catalog(signer, 8);
        (LoginServerSelection selection, LoginSelectionInput input) = CreateInput();
        LoginVerifiedSelectionRetention.Apply(null, previous, s_now, input, selection);
        input.PointerPressed(true, 60, 200);
        LoginVerifiedSelectionRetention.Apply(previous, current, s_now, input, selection);
        input.PointerReleased(true, 60, 200);
        Assert.Null(selection.SelectedRegion);

        SelectFirst(selection);
        input.PointerPressed(true, 440, 225);
        LoginVerifiedSelectionRetention.Apply(current, Catalog(signer, 9), s_now, input, selection);
        input.PointerReleased(true, 440, 225);
        Assert.Equal("local-realm", selection.SelectedServer?.Id);
        Assert.Equal(LoginSelectionFocus.None, input.Focus);
    }

    private static (LoginServerSelection Selection, LoginSelectionInput Input) CreateInput()
    {
        LoginServerSelection selection = new();
        return (selection, new LoginSelectionInput(LoginVisualLayout.Create(new LogicalRenderSize(1024, 768)), selection));
    }

    private static void SelectFirst(LoginServerSelection selection)
    {
        Assert.True(selection.SelectRegionSlot(0));
        Assert.True(selection.SelectServerSlot(0));
    }

    private static SignedLoginCatalog Catalog(ECDsa signer, int sequence, int regionIndex = 0, int serverIndex = 0,
        string host = "127.0.0.1", int port = 9958, string protocol = "LocalRealm", string iconId = "g01s01",
        string regionName = "Local Development", string serverName = "OpenConquer Local", string regionId = "local", bool includeServer = true,
        string expiresAt = "2026-10-11T21:00:00Z")
    {
        var regions = Enumerable.Range(0, regionIndex + 1).Select(i => new
        {
            id = i == regionIndex ? regionId : $"filler-region-{i}",
            name = i == regionIndex ? regionName : $"Filler Region {i}",
            servers = Enumerable.Range(0, i == regionIndex && includeServer ? serverIndex + 1 : 0).Select(j => new
            {
                id = j == serverIndex ? "local-realm" : $"filler-server-{j}",
                name = j == serverIndex ? serverName : $"Filler Server {j}",
                protocolServerName = j == serverIndex ? protocol : $"Filler{j}",
                accountHost = host,
                accountPort = port,
                iconId = iconId,
            }).ToArray(),
        }).ToArray();

        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 2,
            productId = "OpenConquer",
            sequence,
            issuedAt = "2026-10-11T19:00:00Z",
            expiresAt,
            regions,
        });
        return SignedLoginCatalog.Read(payload, SignedLoginCatalogTests.Sign(signer, payload), SignedLoginCatalogTests.Authority(signer), s_now);
    }
}
