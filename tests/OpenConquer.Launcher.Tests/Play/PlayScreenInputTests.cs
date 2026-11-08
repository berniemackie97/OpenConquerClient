using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using OpenConquer.Launcher.Play;

namespace OpenConquer.Launcher.Tests.Play;

public sealed class PlayScreenInputTests(PlayScreenHeadlessFixture fixture) : IClassFixture<PlayScreenHeadlessFixture>
{
    [Fact]
    public Task PressAndReleaseOnAStartButton_ClicksIt() => TestOnScreen((window, _, clicks, _) =>
    {
        Point high = PlayLayout.High + new Point(105, 40);

        window.MouseMove(high);
        window.MouseDown(high, MouseButton.Left);
        window.MouseUp(high, MouseButton.Left);

        Assert.Equal([PlayButton.High], clicks);
    });

    [Fact]
    public Task ReleaseAwayFromTheButton_DoesNotClick() => TestOnScreen((window, _, clicks, _) =>
    {
        Point standard = PlayLayout.Standard + new Point(105, 45);

        window.MouseDown(standard, MouseButton.Left);
        window.MouseMove(new Point(300, 300));
        window.MouseUp(new Point(300, 300), MouseButton.Left);

        Assert.Empty(clicks);
    });

    [Fact]
    public Task TransparentPixelsOfAButton_AreNotPartOfIt() => TestOnScreen((window, _, clicks, _) =>
    {
        // The top-left corner of High's 210x80 art is the transparent colour key.
        Point corner = PlayLayout.High + new Point(1, 1);

        window.MouseDown(corner, MouseButton.Left);
        window.MouseUp(corner, MouseButton.Left);

        Assert.Empty(clicks);
    });

    [Fact]
    public Task Buttons_ShowTheDialogCursor() => TestOnScreen((window, screen, _, _) =>
    {
        using PlayCursor cursor = new(window);
        Point high = PlayLayout.High + new Point(105, 40);

        window.MouseMove(high);

        Assert.NotNull(window.Cursor);
        Assert.All(screen.Children.OfType<PlaySkinButton>(), button => Assert.Same(window.Cursor, button.Cursor));
    });

    [Fact]
    public Task Hint_HidesTheWebPanelUntilItCloses() => TestOnScreen((_, screen, _, webPanel) =>
    {
        screen.ShowHint(PlayText.LowConfirmation, PlayButton.Continue, PlayButton.Return);
        Assert.False(webPanel.IsVisible);
        screen.HideHint();
        Assert.True(webPanel.IsVisible);
    });

    private Task TestOnScreen(Action<Window, PlayScreen, List<PlayButton>, Border> test) => fixture.Dispatch(() =>
    {
        Border webPanel = new();
        using PlayScreen screen = new(webPanel);
        List<PlayButton> clicks = [];
        screen.ButtonClicked += (_, button) => clicks.Add(button);
        Window window = new()
        {
            Width = 800,
            Height = 600,
            Content = screen
        };
        try
        {
            window.Show();
            test(window, screen, clicks, webPanel);
        }
        finally
        {
            window.Close();
        }
    }, TestContext.Current.CancellationToken);
}

public sealed class PlayScreenHeadlessFixture : IAsyncDisposable
{
    // Keep the dispatcher alive while prior windows' callbacks drain. Each test owns a fresh window.
    private readonly HeadlessUnitTestSession _session =
        HeadlessUnitTestSession.StartNew(typeof(PlayScreenHeadlessFixture), AvaloniaTestIsolationLevel.PerAssembly);

    public static AppBuilder BuildAvaloniaApp()
    {
        // AppBuilder invokes this on the session thread, before the headless compositor is created.
        Dispatcher.UIThread.VerifyAccess();
        return AppBuilder.Configure<Application>().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true });
    }

    internal Task Dispatch(Action test, CancellationToken token) => _session.Dispatch(test, token);

    public ValueTask DisposeAsync() => _session.DisposeAsync();
}
