using OpenConquer.Client.UI.Login.Credentials;
using OpenConquer.Client.UI.Login.Registration;
using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Content.Login;
using OpenConquer.Platform.Windowing.Desktop;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Login.Registration;

public sealed class LoginRegistrationFormTests
{
    private static readonly LoginRegistrationLayout s_layout = new(LoginStageLayout.Create(new LogicalRenderSize(1024, 768)));

    [Fact]
    public void OpeningFadesInAndHoldsWithTheAccountFieldFocused()
    {
        using LoginRegistrationForm form = new(s_layout);

        form.Open("guidance");
        Assert.Equal(2, form.Frame);
        Assert.False(form.AcceptsInput);
        form.TextEntered('a');

        form.Advance(10);

        Assert.Equal(LoginAccountArtwork.DialogHeldFrame, form.Frame);
        Assert.True(form.AcceptsInput);
        Assert.Equal(LoginRegistrationField.Account, form.Focus);
        Assert.Equal(("guidance", false), (form.Message, form.MessageIsError));
        Assert.Equal(0, form.Field(LoginRegistrationField.Account).Length);
    }

    [Fact]
    public void TabAndShiftTabCycleTheFieldsAndSelectTheirText()
    {
        using LoginRegistrationForm form = Open();
        Type(form, "hero");

        Assert.Equal(LoginRegistrationCommand.None, form.KeyPressed(Key(DesktopKey.Tab)));
        Assert.Equal(LoginRegistrationField.Password, form.Focus);
        form.KeyPressed(Key(DesktopKey.Tab));
        Assert.Equal(LoginRegistrationField.Confirm, form.Focus);
        form.KeyPressed(Key(DesktopKey.Tab, repeat: true));
        Assert.Equal(LoginRegistrationField.Confirm, form.Focus);
        form.KeyPressed(Key(DesktopKey.Tab));
        Assert.Equal(LoginRegistrationField.Account, form.Focus);
        Assert.Equal((0, 4), (form.Field(LoginRegistrationField.Account).SelectionStart, form.Field(LoginRegistrationField.Account).SelectionEnd));

        form.KeyPressed(Key(DesktopKey.Tab, DesktopKeyModifiers.Shift));
        Assert.Equal(LoginRegistrationField.Confirm, form.Focus);
    }

    [Fact]
    public void EnterSubmitsAndEscapeCancels()
    {
        using LoginRegistrationForm form = Open();

        Assert.Equal(LoginRegistrationCommand.Submit, form.KeyPressed(Key(DesktopKey.Enter)));
        Assert.Equal(LoginRegistrationCommand.None, form.KeyPressed(Key(DesktopKey.Enter, repeat: true)));
        Assert.Equal(LoginRegistrationCommand.None, form.KeyPressed(Key(DesktopKey.Enter, DesktopKeyModifiers.Alt)));
        Assert.Equal(LoginRegistrationCommand.Cancel, form.KeyPressed(Key(DesktopKey.Escape)));
    }

    [Fact]
    public void FieldsStopAtThePolicyLimits()
    {
        using LoginRegistrationForm form = Open();

        Type(form, new string('a', 20));
        form.KeyPressed(Key(DesktopKey.Tab));
        Type(form, new string('b', 20));

        Assert.Equal(LoginRegistrationPolicy.MaximumAccountLength, form.Field(LoginRegistrationField.Account).Length);
        Assert.Equal(LoginRegistrationPolicy.MaximumPasswordLength, form.Field(LoginRegistrationField.Password).Length);
    }

    [Fact]
    public void PasswordFieldsAreMaskedAndNeverReachTheClipboard()
    {
        using LoginRegistrationForm form = Open();
        ILoginTextEditor editor = form;
        Type(form, "hero");
        form.KeyPressed(Key(DesktopKey.Tab));
        Type(form, "secret1");
        form.KeyPressed(Key(DesktopKey.A, DesktopKeyModifiers.Control));

        Assert.Equal("*******", form.Field(LoginRegistrationField.Password).Display);
        Assert.Null(editor.SelectedText());
        Assert.False(editor.CutSelection());
        Assert.Equal(7, form.Field(LoginRegistrationField.Password).Length);

        form.KeyPressed(Key(DesktopKey.Tab, DesktopKeyModifiers.Shift));
        Assert.Equal("hero", editor.SelectedText());
    }

    [Theory]
    [InlineData("secret1", "secret1", true)]
    [InlineData("secret1", "secret2", false)]
    [InlineData("secret1", "secret12", false)]
    [InlineData("", "", true)]
    public void PasswordsMatchComparesBothEntries(string password, string confirmation, bool expected)
    {
        using LoginRegistrationForm form = Open();
        Fill(form, "hero", password, confirmation);

        Assert.Equal(expected, form.PasswordsMatch());
    }

    [Fact]
    public void TakingCredentialsWipesBothPasswordsAndKeepsTheName()
    {
        using LoginRegistrationForm form = Open();
        Fill(form, "hero", "secret1", "secret1");

        Assert.True(form.TryTakeCredentials(out byte[] name, out char[] password));

        Assert.Equal("hero"u8.ToArray(), name);
        Assert.Equal("secret1", new string(password));
        Assert.Equal(0, form.Field(LoginRegistrationField.Password).Length);
        Assert.Equal(0, form.Field(LoginRegistrationField.Confirm).Length);
        Assert.Equal("hero", form.AccountName);
        Assert.False(form.TryTakeCredentials(out _, out _));
    }

    [Fact]
    public void PasswordProblemClearsBothEntriesAndFocusesThePassword()
    {
        using LoginRegistrationForm form = Open();
        Fill(form, "hero", "secret1", "secret2");

        form.ShowProblem("mismatch", LoginRegistrationField.Confirm);

        Assert.Equal(("mismatch", true), (form.Message, form.MessageIsError));
        Assert.Equal(LoginRegistrationField.Password, form.Focus);
        Assert.Equal(0, form.Field(LoginRegistrationField.Password).Length);
        Assert.Equal(0, form.Field(LoginRegistrationField.Confirm).Length);
        Assert.Equal("hero", form.AccountName);
    }

    [Fact]
    public void AccountProblemSelectsTheNameSoTypingReplacesIt()
    {
        using LoginRegistrationForm form = Open();
        Fill(form, "hero", "secret1", "secret1");

        form.ShowProblem("taken", LoginRegistrationField.Account);
        Type(form, "knight");

        Assert.Equal("knight", form.AccountName);
        Assert.Equal(7, form.Field(LoginRegistrationField.Password).Length);
    }

    [Fact]
    public void SubmittingLocksTheDialog()
    {
        using LoginRegistrationForm form = Open();
        Type(form, "hero");
        LoginRect ok = s_layout.Ok;
        form.PointerMoved(true, ok.X + 4, ok.Y + 4);

        form.BeginSubmitting("working");
        Type(form, "x");

        Assert.False(form.AcceptsInput);
        Assert.Null(form.Focus);
        Assert.Equal("hero", form.AccountName);
        Assert.Equal(LoginRegistrationCommand.None, form.KeyPressed(Key(DesktopKey.Enter)));
        Assert.Equal(LoginButtonState.Up, form.ButtonState(LoginDialogButton.Ok));
        Assert.Null(form.PointerPressed(true, ok.X + 4, ok.Y + 4, null, extendSelection: false));
    }

    [Fact]
    public void ClosingWipesPasswordsAtOnceAndEverythingOnceHidden()
    {
        using LoginRegistrationForm form = Open();
        Fill(form, "hero", "secret1", "secret1");

        form.Close();

        Assert.Equal(LoginAccountArtwork.DialogHeldFrame + 1, form.Frame);
        Assert.Equal(0, form.Field(LoginRegistrationField.Password).Length);
        Assert.Equal("hero", form.AccountName);
        Assert.False(form.AcceptsInput);

        form.Advance(10);

        Assert.False(form.IsOpen);
        Assert.Equal(string.Empty, form.AccountName);
        Assert.Equal(string.Empty, form.Message);
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(3, 9)]
    [InlineData(5, 7)]
    public void ClosingWhileFadingInFadesOutFromTheSameMask(int framesShown, int expected)
    {
        using LoginRegistrationForm form = new(s_layout);
        form.Open("guidance");
        form.Advance(framesShown - 2);

        form.Close();

        Assert.Equal(expected, form.Frame);
        int fadingOut = expected == 1 ? LoginAccountArtwork.DialogFrameCount : expected;
        Assert.Equal(LoginAccountArtwork.MaskAlpha(framesShown), LoginAccountArtwork.MaskAlpha(fadingOut));
    }

    [Fact]
    public void PointerFocusesFieldsAndClicksTheButtons()
    {
        using LoginRegistrationForm form = Open();
        LoginRect confirm = s_layout.Box(LoginRegistrationField.Confirm);

        Assert.Null(form.PointerPressed(true, confirm.X + 3, confirm.Y + 3, null, extendSelection: false));
        Assert.Equal(LoginRegistrationField.Confirm, form.Focus);
        Assert.Equal(LoginRegistrationCommand.None, form.PointerReleased(true, confirm.X + 3, confirm.Y + 3));

        Assert.Equal(LoginRegistrationCommand.Submit, Click(form, s_layout.Ok));
        Assert.Equal(LoginRegistrationCommand.Cancel, Click(form, s_layout.Cancel));

        // A press that leaves its button before release clicks nothing.
        LoginRect ok = s_layout.Ok;
        Assert.Equal(LoginDialogButton.Ok, form.PointerPressed(true, ok.X + 2, ok.Y + 2, null, extendSelection: false));
        Assert.Equal(LoginRegistrationCommand.None, form.PointerReleased(true, 0, 0));
    }

    [Fact]
    public void DisposeWipesEveryField()
    {
        LoginRegistrationForm form = Open();
        Fill(form, "hero", "secret1", "secret1");

        form.Dispose();
        form.Dispose();

        Assert.False(form.IsOpen);
        Assert.All(Enum.GetValues<LoginRegistrationField>(), field => Assert.Equal(0, form.Field(field).Length));
        Assert.Throws<ObjectDisposedException>(() => form.Open("guidance"));
    }

    internal static LoginRegistrationForm Open(LoginRegistrationLayout? layout = null)
    {
        LoginRegistrationForm form = new(layout ?? s_layout);
        form.Open("guidance");
        form.Advance(LoginAccountArtwork.DialogFrameCount);
        Assert.True(form.AcceptsInput);
        return form;
    }

    /// <summary>Types the three entries in field order, starting from the focused account field.</summary>
    internal static void Fill(LoginRegistrationForm form, string account, string password, string confirmation)
    {
        Type(form, account);
        form.KeyPressed(Key(DesktopKey.Tab));
        Type(form, password);
        form.KeyPressed(Key(DesktopKey.Tab));
        Type(form, confirmation);
    }

    private static void Type(LoginRegistrationForm form, string text)
    {
        foreach (char character in text)
        {
            form.TextEntered(character);
        }
    }

    private static LoginRegistrationCommand Click(LoginRegistrationForm form, LoginRect button)
    {
        form.PointerMoved(true, button.X + 2, button.Y + 2);
        form.PointerPressed(true, button.X + 2, button.Y + 2, null, extendSelection: false);
        return form.PointerReleased(true, button.X + 2, button.Y + 2);
    }

    private static DesktopKeyEvent Key(DesktopKey key, DesktopKeyModifiers modifiers = default, bool repeat = false) =>
        new(key, modifiers, repeat);
}
