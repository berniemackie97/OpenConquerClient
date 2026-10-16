using OpenConquer.Client.UI.Login;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Login;

public sealed class LoginAccountHistoryTests
{
    [Fact]
    public void AccountNamesOnlyPersistAfterExplicitConfirmationAndNeverIncludePasswords()
    {
        string folder = Directory.CreateTempSubdirectory("oc-retail-account-").FullName;
        try
        {
            LoginAccountHistory history = new(folder);
            LoginRetailPanelControls controls = Controls(history);
            using LoginCredentialInput editor = Editor(out LoginCredentialLayout layout);
            Assert.True(editor.PointerPressed(true, layout.AccountField.X + 2, layout.AccountField.Y + 2));
            foreach (char c in "Player_01")
                editor.TextEntered(c);
            editor.PointerReleased();
            Assert.True(editor.PointerPressed(true, layout.PasswordField.X + 2, layout.PasswordField.Y + 2));
            foreach (char c in "Secret")
                editor.TextEntered(c);
            editor.PointerReleased();

            Click(controls, editor, 626, 652);
            Assert.Equal(LoginRetailDialog.Save, controls.Dialog);
            Assert.Empty(history.Accounts);
            controls.Advance(TimeSpan.FromSeconds(.2));
            Assert.Equal(6, controls.DialogFrame);
            Click(controls, editor, 579, 426); // Cancel
            Assert.Empty(history.Accounts);
            controls.Advance(TimeSpan.FromSeconds(.2));
            Assert.Equal(LoginRetailDialog.None, controls.Dialog);

            Click(controls, editor, 626, 652);
            controls.Advance(TimeSpan.FromSeconds(.2));
            Click(controls, editor, 410, 428); // Save -> OK
            Assert.Equal("Player_01", Assert.Single(history.Accounts));
            string persisted = File.ReadAllText(Path.Combine(folder, "login-accounts.json"));
            Assert.Contains("Player_01", persisted, StringComparison.Ordinal);
            Assert.DoesNotContain("Secret", persisted, StringComparison.Ordinal);
            Assert.Equal(6, editor.PasswordLength);
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    [Fact]
    public void HistoryIsBoundedToTenEntriesAndDeduplicatedInSourceListOrder()
    {
        string folder = Directory.CreateTempSubdirectory("oc-retail-history-").FullName;
        try
        {
            LoginAccountHistory history = new(folder);
            for (int i = 0; i < 15; i++)
                Assert.True(history.TryRecord($"Player_{i}"));
            Assert.Equal(10, history.Accounts.Count);
            Assert.Equal("Player_5", history.Accounts[0]);
            Assert.Equal("Player_14", history.Accounts[^1]);
            Assert.True(history.TryRecord("Player_10"));
            Assert.Equal("Player_5", history.Accounts[0]);
            Assert.Equal(10, history.Accounts.Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(history.Accounts.ToArray(), new LoginAccountHistory(folder).Accounts.ToArray());
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    [Theory]
    [InlineData("{invalid")]
    [InlineData("{\"schemaVersion\":999,\"accounts\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"accounts\":[\"A\",\"A\"]}")]
    [InlineData("{\"schemaVersion\":1,\"accounts\":[\"bad name\"]}")]
    public void MalformedOrUnknownHistoryCannotBeOverwritten(string contents)
    {
        string folder = Directory.CreateTempSubdirectory("oc-retail-invalid-").FullName;
        try
        {
            string path = Path.Combine(folder, "login-accounts.json");
            File.WriteAllText(path, contents);
            LoginAccountHistory history = new(folder);
            Assert.False(history.Writable);
            Assert.Empty(history.Accounts);
            Assert.False(history.TryRecord("Player"));
            Assert.Equal(contents, File.ReadAllText(path));
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    [Fact]
    public void ListMotionUsesSourceTwentyMillisecondStepsAndClicksRecallAccount()
    {
        string folder = Directory.CreateTempSubdirectory("oc-retail-list-").FullName;
        try
        {
            LoginAccountHistory history = new(folder);
            Assert.True(history.TryRecord("First"));
            Assert.True(history.TryRecord("Second"));
            LoginRetailPanelControls controls = Controls(history);
            using LoginCredentialInput editor = Editor(out LoginCredentialLayout field);
            Assert.True(editor.PointerPressed(true, field.PasswordField.X + 2, field.PasswordField.Y + 2));
            editor.TextEntered('S');
            editor.PointerReleased();
            Click(controls, editor, 601, 650);
            Assert.Equal(LoginAccountPopupState.Opening, controls.Popup);
            Assert.Equal(0, controls.PopupOffset);
            controls.Advance(TimeSpan.FromMilliseconds(20));
            Assert.Equal(-14, controls.PopupOffset); // 10+2*N
            controls.Advance(TimeSpan.FromMilliseconds(100));
            Assert.Equal(LoginAccountPopupState.Open, controls.Popup);
            LoginSpritePlacement row = controls.RowBounds(0);
            Assert.True(controls.PointerPressed(true, row.X + 5, row.Y + 5, editor));
            Assert.Equal("Second", editor.AccountDisplay);
            Assert.Equal(0, editor.PasswordLength);
            Assert.Equal(LoginAccountPopupState.Closing, controls.Popup);
            controls.Advance(TimeSpan.FromMilliseconds(100));
            Assert.Equal(LoginAccountPopupState.Closed, controls.Popup);
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    [Fact]
    public void EmptyOriginalHistoryCanOpenItsSingleBlankRowWithoutSelectingAnAccount()
    {
        string folder = Directory.CreateTempSubdirectory("oc-retail-empty-row-").FullName;
        try
        {
            LoginRetailPanelControls controls = Controls(new LoginAccountHistory(folder));
            using LoginCredentialInput editor = Editor(out _);
            Click(controls, editor, 601, 650);
            Assert.Equal(1, controls.VisibleRowCount);
            controls.Advance(TimeSpan.FromMilliseconds(100));
            Assert.Equal(LoginAccountPopupState.Open, controls.Popup);
            LoginSpritePlacement blank = controls.RowBounds(0);
            Assert.True(controls.PointerPressed(true, blank.X + 5, blank.Y + 5, editor));
            Assert.Equal(LoginAccountPopupState.Closing, controls.Popup);
            Assert.Empty(editor.AccountDisplay);
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    [Fact]
    public void ClearAccountRequiresConfirmAndDoesNotEraseCurrentTypedAccount()
    {
        string folder = Directory.CreateTempSubdirectory("oc-retail-clear-").FullName;
        try
        {
            LoginAccountHistory history = new(folder);
            Assert.True(history.TryRecord("Recorded"));
            LoginRetailPanelControls controls = Controls(history);
            using LoginCredentialInput editor = Editor(out _);
            Assert.True(editor.ReplaceAccount("Recorded"));
            Click(controls, editor, 659, 650);
            Assert.Equal(LoginRetailDialog.Clear, controls.Dialog);
            Assert.Equal("Recorded", Assert.Single(history.Accounts));
            controls.Advance(TimeSpan.FromSeconds(.2));
            Click(controls, editor, 410, 428);
            Assert.Empty(history.Accounts);
            Assert.Equal("Recorded", editor.AccountDisplay);
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    private static LoginRetailPanelControls Controls(LoginAccountHistory history)
    {
        LoginVisualLayout layout = LoginVisualLayout.Create(new LogicalRenderSize(1024, 768));
        return new LoginRetailPanelControls(LoginRetailFlashLayout.Create(layout), history, layout.Width);
    }

    private static LoginCredentialInput Editor(out LoginCredentialLayout fields)
    {
        LoginServerSelection selection = new();
        fields = LoginCredentialLayout.Create(LoginVisualLayout.Create(new LogicalRenderSize(1024, 768)));
        return new LoginCredentialInput(selection, fields);
    }

    private static void Click(LoginRetailPanelControls controls, LoginCredentialInput editor, int x, int y)
    {
        Assert.True(controls.PointerPressed(true, x, y, editor));
        controls.PointerReleased(true, x, y, editor);
    }
}
