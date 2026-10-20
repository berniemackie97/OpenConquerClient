using OpenConquer.Client.UI.Login;
using OpenConquer.Client.UI.Login.Account;
using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Client.UI.Login.Status;
using OpenConquer.Content.Login;
using OpenConquer.Content.Text;
using OpenConquer.Platform.Windowing.Desktop;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Login;

public sealed class LoginErrorTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("oc-error-ui-").FullName;
    private static LoginStageLayout Layout => LoginStageLayout.Create(new LogicalRenderSize(1024, 768));

    internal static LoginErrorMessages Messages() => new(
        ClientStringResources.Parse("49995=Please login later!\n11400=Your password must be 10-14 characters. Please retrieve your password on the website.\n"u8),
        ClientKeyedStringResources.Parse("STR_LOGIN_FAIL=Failed to login: %s(%d)\nSTR_CONNECT_ERROR_MSG_TYPE_001=Invalid Account ID or Password\nSTR_CONNECT_ERROR_MSG_TYPE_024=This account has been banned.\nSTR_CONNECT_ERROR_MSG_TYPE_UNKNOWN=Unknown Error\n"u8),
        LoginErrorEncoding.ForCodePage(1252));

    [Theory]
    [InlineData(1u, "Failed to login: Invalid Account ID or Password(1)")]
    [InlineData(24u, "Failed to login: This account has been banned.(24)")]
    [InlineData(123456u, "Failed to login: Unknown Error(123456)")]
    public void AccountRefusalKeepsNumericCodeAndRetailFormatting(uint code, string expected)
    {
        LoginFailure failure = LoginFailure.Account(code);
        Assert.Equal(code, failure.Code);
        Assert.Equal(expected, Messages().Format(failure));
        Assert.Equal(code != 24, failure.OpensErrorMovie);
    }

    [Fact]
    public void GameRefusalOwnsItsBytesAndUsesNativeNulAndCommandDelimiterBoundaries()
    {
        byte[] source = "Server refused|ignored\0tail"u8.ToArray();
        LoginFailure failure = LoginFailure.Game(source);
        source.AsSpan().Fill((byte)'X');
        Assert.Equal("Server refused", Messages().Format(failure));
        Assert.DoesNotContain("Server refused", failure.ToString(), StringComparison.Ordinal);
        Assert.Throws<ArgumentOutOfRangeException>(() => LoginFailure.Game(new byte[256]));
    }

    [Fact]
    public void PeerMarkupRemainsPlainTextAndIsNotAFormatString()
    {
        Assert.Equal("<b>%s%d</b>", Messages().Format(LoginFailure.Game("<b>%s%d</b>"u8)));
    }

    [Fact]
    public void EncodingIsSelectedAtPresentationAndDoesNotRejectCp1252ControlBytes()
    {
        Assert.Equal("\u0081\u008D\u008F\u0090\u009D", Messages().Format(LoginFailure.Game([0x81, 0x8D, 0x8F, 0x90, 0x9D])));
        LoginErrorMessages chinese = new(ClientStringResources.Parse([]), ClientKeyedStringResources.Parse([]),
            LoginErrorEncoding.ForCodePage(936));
        Assert.Equal("中", chinese.Format(LoginFailure.Game([0xD6, 0xD0])));
        Assert.NotEqual("中", Messages().Format(LoginFailure.Game([0xD6, 0xD0])));
    }

    [Fact]
    public void EmptyFieldsAndRetryUseDistinctRetailMessages()
    {
        LoginErrorMessages messages = Messages();
        Assert.Equal("Please Enter Your User Name.", messages.Format(LoginFailure.EmptyAccount()));
        Assert.Equal("Please Enter Your Password.", messages.Format(LoginFailure.EmptyPassword()));
        Assert.Equal("Please login later!", messages.Format(LoginFailure.Resource(49995)));
        Assert.StartsWith("Your password must be 10-14 characters.", messages.Format(LoginFailure.Resource(11400)), StringComparison.Ordinal);
    }

    [Fact]
    public void TimelineStopsAtSixAndBlocksInputThroughoutBothTransitions()
    {
        LoginErrorDialog dialog = new(Layout);
        dialog.Show("error");
        Assert.Equal(2, dialog.Frame);
        Assert.Null(dialog.PointerPressed(true, 500, 428));
        Assert.False(dialog.PointerReleased(true, 500, 428));
        Assert.False(dialog.Advance(int.MaxValue));
        Assert.Equal(6, dialog.Frame);
        Assert.Equal(new LoginRect(461, 415, 96, 26), dialog.Bounds(LoginDialogButton.Ok));
        Assert.False(dialog.PointerReleased(true, 500, 428));
        Assert.Equal(LoginDialogButton.Ok, dialog.PointerPressed(true, 500, 428));
        Assert.True(dialog.PointerReleased(true, 500, 428));
        Assert.Equal(7, dialog.Frame);
        Assert.True(dialog.IsOpen);
        dialog.Advance(2);
        Assert.Equal(9, dialog.Frame);
        dialog.Advance(1);
        Assert.False(dialog.IsOpen);
        Assert.Empty(dialog.Text);
    }

    [Fact]
    public void DragOutsideAndFocusLossCancelThePressWithoutDismissing()
    {
        LoginErrorDialog dialog = new(Layout);
        dialog.Show("error");
        dialog.Advance(4);
        dialog.PointerPressed(true, 500, 428);
        dialog.PointerMoved(false, 0, 0);
        Assert.Equal(LoginButtonState.Up, dialog.State(LoginDialogButton.Ok));
        Assert.False(dialog.PointerReleased(false, 0, 0));
        dialog.PointerPressed(true, 500, 428);
        dialog.FocusLost();
        Assert.False(dialog.PointerReleased(true, 500, 428));
        Assert.Equal(6, dialog.Frame);
    }

    [Fact]
    public void RepeatedFeedbackDoesNotReplayButANewIdenticalFailureDoes()
    {
        List<LoginAudioCue> sounds = [];
        using LoginScreen screen = Screen(sounds);
        LoginAttemptFeedback feedback = new(LoginAttemptStatus.RetryLater, LoginFailure.Resource(49995));
        screen.ApplyFeedback(feedback, Messages());
        screen.Error.Advance(4);
        screen.ApplyFeedback(feedback, Messages());
        Assert.Equal(6, screen.Error.Frame);
        screen.Error.PointerPressed(true, 500, 428);
        screen.Error.PointerReleased(true, 500, 428);
        screen.Error.Advance(3);
        screen.ApplyFeedback(feedback, Messages());
        Assert.False(screen.Error.IsOpen);
        screen.ApplyFeedback(new(LoginAttemptStatus.RetryLater, LoginFailure.Resource(49995)), Messages());
        Assert.Equal(2, screen.Error.Frame);
        Assert.Equal([LoginAudioCue.Error, LoginAudioCue.Error], sounds);
    }

    [Fact]
    public void ModalBlocksCredentialsClipboardEnterAndUnderlyingButtons()
    {
        using LoginScreen screen = Screen([]);
        screen.ApplyFeedback(new(LoginAttemptStatus.RetryLater, LoginFailure.Resource(49995)), Messages());
        int submitted = 0;
        int exited = 0;
        screen.LoginRequested += () => submitted++;
        screen.ExitRequested += () => exited++;
        long revision = screen.Selection.Revision;
        screen.KeyPressed(new DesktopKeyEvent(DesktopKey.Enter, DesktopKeyModifiers.None, false),
            () => throw new InvalidOperationException(), _ => throw new InvalidOperationException());
        screen.KeyPressed(new DesktopKeyEvent(DesktopKey.V, DesktopKeyModifiers.Control, false),
            () => throw new InvalidOperationException(), _ => throw new InvalidOperationException());
        screen.TextEntered('x');
        screen.PointerPressed(true, 500, 700, false);
        screen.PointerReleased(true, 500, 700);
        Assert.Equal(0, submitted);
        Assert.Equal(0, exited);
        Assert.Equal(0, screen.Credentials.AccountLength);
        Assert.Equal(revision, screen.Selection.Revision);
        Assert.Equal(2, screen.Error.Frame);
    }

    [Fact]
    public void EscapeOpensQuitAndCancelReturnsToTheHeldError()
    {
        using LoginScreen screen = Screen([]);
        screen.ApplyFeedback(new(LoginAttemptStatus.RetryLater, LoginFailure.Resource(49995)), Messages());
        screen.Error.Advance(4);
        screen.KeyPressed(new DesktopKeyEvent(DesktopKey.Escape, DesktopKeyModifiers.None, false), () => null, _ => { });
        Assert.True(screen.Quit.IsOpen);
        Assert.Equal(6, screen.Error.Frame);
        screen.Quit.Advance(4);
        screen.PointerPressed(true, 580, 428, false);
        screen.PointerReleased(true, 580, 428);
        Assert.False(screen.Quit.Advance(3));
        Assert.False(screen.Quit.IsOpen);
        Assert.True(screen.Error.IsOpen);
    }

    [Fact]
    public void QuitConfirmationWaitsForFrameTen()
    {
        LoginErrorDialog dialog = new(Layout, quit: true);
        dialog.Show(string.Empty);
        dialog.Advance(4);
        dialog.PointerPressed(true, 420, 428);
        Assert.True(dialog.PointerReleased(true, 420, 428));
        Assert.False(dialog.Advance(2));
        Assert.True(dialog.Advance(1));
        Assert.False(dialog.Advance(1));
    }

    [Fact]
    public void Code24RetainsRefusalWithoutInventingAnErrorMovieHandler()
    {
        List<LoginAudioCue> sounds = [];
        using LoginScreen screen = Screen(sounds);
        screen.ApplyFeedback(new(LoginAttemptStatus.AccountRefused, LoginFailure.Account(24)), Messages());
        Assert.False(screen.Error.IsOpen);
        Assert.Empty(sounds);
    }

    [Theory]
    [InlineData("abc def", 4, "abc |def")]
    [InlineData("abcdef", 3, "abc|def")]
    [InlineData("a\r\nb\rc\nd", 8, "a|b|c|d")]
    [InlineData("", 8, "")]
    public void WrappingKeepsWordsExplicitLineBreaksAndLongTokens(string text, int width, string expected)
    {
        Assert.Equal(expected, string.Join('|', LoginErrorTextLayout.Wrap(text, width, value => value.Length)));
    }

    [Fact]
    public void WrappingNeverSplitsASurrogatePair()
    {
        Assert.Equal(["😀", "😀"], LoginErrorTextLayout.Wrap("😀😀", 1, value => value.EnumerateRunes().Count()));
    }

    private LoginScreen Screen(List<LoginAudioCue> sounds)
    {
        LoginScreen screen = new(Layout, new LoginAccountHistory(_folder), "5517", sounds.Add);
        screen.Advance(TimeSpan.Zero);
        while (screen.IsOpeningPlaying)
        {
            screen.Advance(TimeSpan.FromMilliseconds(250));
        }

        return screen;
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);
}
