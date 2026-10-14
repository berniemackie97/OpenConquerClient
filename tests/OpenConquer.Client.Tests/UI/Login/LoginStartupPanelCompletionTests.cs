using OpenConquer.Client.UI.Login;
using OpenConquer.Platform.Windowing.Desktop;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Login;

public sealed class LoginStartupPanelCompletionTests
{
    [Fact]
    public void AccountHistoryUsesVerifiedTenEntryBoundAndMostRecentFirst()
    {
        LoginLocalPreferences preferences = new();
        for (int index = 0; index < 15; index++)
            Assert.True(preferences.SaveAccount($"Player_{index}"));
        Assert.Equal(10, preferences.Accounts.Count);
        Assert.Equal("Player_14", preferences.Accounts[0]);
        Assert.Equal("Player_5", preferences.Accounts[9]);
        Assert.True(preferences.SaveAccount("Player_10"));
        Assert.Equal("Player_10", preferences.Accounts[0]);
        Assert.Equal(10, preferences.Accounts.Distinct(StringComparer.Ordinal).Count());
        Assert.True(preferences.RemoveAccount("Player_10"));
        Assert.False(preferences.RemoveAccount("Player_10"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("with spaces")]
    [InlineData("a\nb")]
    [InlineData("!injection")]
    [InlineData("漢字")]
    public void InvalidAccountNamesCannotEnterHistory(string account)
    {
        LoginLocalPreferences preferences = new();
        Assert.False(preferences.SaveAccount(account));
        Assert.Empty(preferences.Accounts);
    }

    [Fact]
    public void PreferencesRoundTripWithoutAnyPasswordOrRemoteAddress()
    {
        LoginLocalPreferences preferences = new();
        Assert.True(preferences.SaveAccount("Player_1"));
        preferences.SetMusicEnabled(false);
        byte[] encoded = preferences.Serialize();
        Assert.True(LoginLocalPreferences.TryParse(encoded, out LoginLocalPreferences loaded));
        Assert.False(loaded.MusicEnabled);
        Assert.Equal("Player_1", Assert.Single(loaded.Accounts));
        Assert.False(System.Text.Encoding.UTF8.GetString(encoded).Contains("password", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"schemaVersion\":2,\"accounts\":[],\"musicEnabled\":true}")]
    [InlineData("{\"schemaVersion\":1,\"accounts\":[],\"musicEnabled\":true,\"extra\":4}")]
    [InlineData("{\"schemaVersion\":1,\"accounts\":[\"A\",\"A\"],\"musicEnabled\":true}")]
    [InlineData("{\"schemaVersion\":1,\"accounts\":[\"漢\"],\"musicEnabled\":true}")]
    [InlineData("{\"schemaVersion\":1,\"accounts\":[],\"musicEnabled\":true,\"accounts\":[]}")]
    public void PreferenceParserFailsClosedForUnsupportedData(string json)
    {
        Assert.False(LoginLocalPreferences.TryParse(System.Text.Encoding.UTF8.GetBytes(json), out _));
    }

    [Fact]
    public void StoreRefusesToOverwriteInvalidUserFile()
    {
        string dir = Directory.CreateTempSubdirectory("oc-login-preferences-").FullName;
        try
        {
            string path = Path.Combine(dir, "login-preferences.json");
            File.WriteAllText(path, "{invalid}");
            LoginLocalPreferencesStore store = new(dir);
            LoginLocalPreferences fallback = store.Load();
            Assert.True(fallback.MusicEnabled);
            Assert.False(store.TrySave(fallback));
            Assert.Equal("{invalid}", File.ReadAllText(path));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void StorePersistsOnlyExplicitAccountNameAndBgmChoices()
    {
        string dir = Directory.CreateTempSubdirectory("oc-login-preferences-").FullName;
        try
        {
            LoginLocalPreferencesStore store = new(dir);
            LoginLocalPreferences preferences = store.Load();
            Assert.True(preferences.SaveAccount("Native_5517"));
            preferences.SetMusicEnabled(false);
            Assert.True(store.TrySave(preferences));
            LoginLocalPreferences loaded = new LoginLocalPreferencesStore(dir).Load();
            Assert.Equal("Native_5517", Assert.Single(loaded.Accounts));
            Assert.False(loaded.MusicEnabled);
            Assert.Single(Directory.EnumerateFiles(dir));
            if (!OperatingSystem.IsWindows())
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(Path.Combine(dir, "login-preferences.json")));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void SelectingAccountFromHistoryWipesPasswordAndResetsCaret()
    {
        LoginServerSelection selection = new();
        selection.ReplaceCatalog(new LoginServerCatalog([new LoginRegion("reg", "Region", [new LoginServer("realm", "Realm", "Native")])]));
        Assert.True(selection.SelectRegionSlot(0));
        Assert.True(selection.SelectServerSlot(0));
        LoginCredentialLayout layout = LoginCredentialLayout.Create(LoginVisualLayout.Create(new LogicalRenderSize(1024, 768)));
        using LoginCredentialInput editor = new(selection, layout);
        editor.PointerPressed(true, layout.AccountField.X + 1, layout.AccountField.Y + 1);
        editor.TextEntered('A');
        editor.KeyPressed(new DesktopKeyEvent(DesktopKey.Tab, DesktopKeyModifiers.None, false));
        editor.TextEntered('S');
        Assert.Equal(1, editor.PasswordLength);
        Assert.True(editor.ReplaceAccount("Recall_1"));
        Assert.Equal("Recall_1", editor.AccountDisplay);
        Assert.Equal(0, editor.PasswordLength);
        Assert.Equal(LoginCredentialFocus.Account, editor.Focus);
        Assert.Equal(8, editor.CaretIndex);
        Assert.False(editor.ReplaceAccount("invalid account"));
        Assert.Equal("Recall_1", editor.AccountDisplay);
    }

    [Fact]
    public void PanelButtonsAndSavedNamePopupCommitOnlyCompletedGestures()
    {
        string dir = Directory.CreateTempSubdirectory("oc-login-panel-").FullName;
        try
        {
            LoginServerSelection selection = new();
            selection.ReplaceCatalog(new LoginServerCatalog([new LoginRegion("reg", "Region", [new LoginServer("realm", "Realm", "Native")])]));
            Assert.True(selection.SelectRegionSlot(0));
            Assert.True(selection.SelectServerSlot(0));
            LoginVisualLayout visual = LoginVisualLayout.Create(new LogicalRenderSize(1024, 768));
            LoginCredentialLayout fields = LoginCredentialLayout.Create(visual);
            using LoginCredentialInput editor = new(selection, fields);
            LoginPanelControls controls = new(visual, new LoginLocalPreferencesStore(dir));
            Assert.True(editor.PointerPressed(true, fields.AccountField.X + 2, fields.AccountField.Y + 2));
            foreach (char c in "Native_01")
                editor.TextEntered(c);
            LoginSpritePlacement save = controls.Bounds(LoginPanelControl.Save);
            Assert.True(controls.PointerPressed(true, save.X + 2, save.Y + 2, editor));
            Assert.Equal(LoginPanelControl.None, controls.PointerReleased(false, -1, -1, editor));
            Assert.Empty(controls.SavedAccounts);
            Assert.True(controls.PointerPressed(true, save.X + 2, save.Y + 2, editor));
            Assert.Equal(LoginPanelControl.Save, controls.PointerReleased(true, save.X + 2, save.Y + 2, editor));
            Assert.Equal("Native_01", Assert.Single(controls.SavedAccounts));

            LoginSpritePlacement music = controls.Bounds(LoginPanelControl.Music);
            Assert.True(controls.PointerPressed(true, music.X + 2, music.Y + 2, editor));
            Assert.Equal(LoginPanelControl.Music, controls.PointerReleased(true, music.X + 2, music.Y + 2, editor));
            Assert.False(controls.MusicEnabled);

            Assert.True(editor.ReplaceAccount("Other_02"));
            editor.KeyPressed(new DesktopKeyEvent(DesktopKey.Tab, DesktopKeyModifiers.None, false));
            editor.TextEntered('S');
            Assert.Equal(1, editor.PasswordLength);
            LoginSpritePlacement list = controls.Bounds(LoginPanelControl.Accounts);
            Assert.True(controls.PointerPressed(true, list.X + 2, list.Y + 2, editor));
            Assert.Equal(LoginPanelControl.Accounts, controls.PointerReleased(true, list.X + 2, list.Y + 2, editor));
            Assert.True(controls.ListExpanded);
            LoginSpritePlacement row = controls.Bounds(LoginPanelControl.Account0);
            Assert.True(controls.PointerPressed(true, row.X + 3, row.Y + 3, editor));
            Assert.Equal(LoginPanelControl.Account0, controls.PointerReleased(true, row.X + 3, row.Y + 3, editor));
            Assert.Equal("Native_01", editor.AccountDisplay);
            Assert.Equal(0, editor.PasswordLength);
            Assert.False(controls.ListExpanded);

            LoginSpritePlacement clear = controls.Bounds(LoginPanelControl.Clear);
            Assert.True(controls.PointerPressed(true, clear.X + 3, clear.Y + 3, editor));
            Assert.Equal(LoginPanelControl.Clear, controls.PointerReleased(true, clear.X + 3, clear.Y + 3, editor));
            Assert.Empty(controls.SavedAccounts);
            Assert.Empty(editor.AccountDisplay);
            Assert.False(controls.IsEnabled(LoginPanelControl.Save, editor));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Theory]
    [InlineData(1024, 768)]
    [InlineData(800, 600)]
    public void NativeAccountButtonFootprintsDoNotCoverCredentialFields(int width, int height)
    {
        LoginVisualLayout visual = LoginVisualLayout.Create(new LogicalRenderSize(width, height));
        LoginCredentialLayout fields = LoginCredentialLayout.Create(visual);
        LoginPanelControls controls = new(visual, new LoginLocalPreferencesStore(null));
        foreach (LoginPanelControl control in new[] { LoginPanelControl.Save, LoginPanelControl.Clear, LoginPanelControl.Accounts, LoginPanelControl.Music })
        {
            LoginSpritePlacement rect = controls.Bounds(control);
            Assert.True(rect.X >= visual.AccountPanel.X);
            Assert.True(rect.Y >= visual.AccountPanel.Y);
            Assert.True(rect.X + rect.Width <= visual.AccountPanel.X + visual.AccountPanel.Width);
            Assert.True(rect.Y + rect.Height <= visual.AccountPanel.Y + visual.AccountPanel.Height);
            Assert.True(rect.X >= fields.AccountField.X + fields.AccountField.Width);
            Assert.True(rect.X >= fields.PasswordField.X + fields.PasswordField.Width);
        }
    }

    [Theory]
    [InlineData(1024, 768, 1366, 768)]
    [InlineData(800, 600, 1280, 720)]
    [InlineData(800, 600, 800, 600)]
    public void DragXClampsThroughLetterboxAndBeyondViewport(int width, int height, int hostWidth, int hostHeight)
    {
        PresentationViewport viewport = PresentationViewport.Compute(new LogicalRenderSize(width, height), hostWidth, hostHeight);
        Assert.Equal(0, viewport.MapPointerXClamped(int.MinValue));
        Assert.Equal(width - 1, viewport.MapPointerXClamped(int.MaxValue));
        Assert.Equal(0, viewport.MapPointerXClamped(viewport.OffsetX));
        Assert.Equal(width - 1, viewport.MapPointerXClamped(viewport.OffsetX + viewport.Width - 1));
    }
}
