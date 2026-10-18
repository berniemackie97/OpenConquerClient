using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using OpenConquer.Launcher.Play;

namespace OpenConquer.Launcher.Tests.Play;

public sealed class PlayScreenInputTests
{
    private static readonly Lazy<HeadlessUnitTestSession> s_session = new(() => HeadlessUnitTestSession.StartNew(typeof(PlayScreenInputTests)));

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<Application>().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true });

    [Fact]
    public Task PressAndReleaseOnAStartButton_ClicksIt() => s_session.Value.Dispatch(() =>
    {
        (Window window, PlayScreen screen, List<PlayButton> clicks) = Show();
        Point high = PlayLayout.High + new Point(105, 40);

        window.MouseMove(high);
        window.MouseDown(high, MouseButton.Left);
        window.MouseUp(high, MouseButton.Left);

        Assert.Equal([PlayButton.High], clicks);
        Close(window, screen);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ReleaseAwayFromTheButton_DoesNotClick() => s_session.Value.Dispatch(() =>
    {
        (Window window, PlayScreen screen, List<PlayButton> clicks) = Show();
        Point standard = PlayLayout.Standard + new Point(105, 45);

        window.MouseDown(standard, MouseButton.Left);
        window.MouseMove(new Point(300, 300));
        window.MouseUp(new Point(300, 300), MouseButton.Left);

        Assert.Empty(clicks);
        Close(window, screen);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task TransparentPixelsOfAButton_AreNotPartOfIt() => s_session.Value.Dispatch(() =>
    {
        (Window window, PlayScreen screen, List<PlayButton> clicks) = Show();
        // The top-left corner of High's 210x80 art is the transparent colour key.
        Point corner = PlayLayout.High + new Point(1, 1);

        window.MouseDown(corner, MouseButton.Left);
        window.MouseUp(corner, MouseButton.Left);

        Assert.Empty(clicks);
        Close(window, screen);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task Buttons_ShowTheDialogCursor() => s_session.Value.Dispatch(() =>
    {
        (Window window, PlayScreen screen, _) = Show();
        using PlayCursor cursor = new(window);
        Point high = PlayLayout.High + new Point(105, 40);

        window.MouseMove(high);

        Assert.NotNull(window.Cursor);
        Assert.All(screen.Children.OfType<PlaySkinButton>(), button => Assert.Same(window.Cursor, button.Cursor));
        Close(window, screen);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task Hint_HidesTheWebPanelUntilItCloses() => s_session.Value.Dispatch(() =>
    {
        Border webPanel = new();
        (Window window, PlayScreen screen, _) = Show(webPanel);

        screen.ShowHint(PlayText.LowConfirmation, PlayButton.Continue, PlayButton.Return);
        Assert.False(webPanel.IsVisible);

        screen.HideHint();
        Assert.True(webPanel.IsVisible);
        Close(window, screen);
    }, TestContext.Current.CancellationToken);

    private static (Window Window, PlayScreen Screen, List<PlayButton> Clicks) Show(Control? webPanel = null)
    {
        PlayScreen screen = new(webPanel ?? new Border());
        List<PlayButton> clicks = [];
        screen.ButtonClicked += (_, button) => clicks.Add(button);
        Window window = new()
        {
            Width = 800,
            Height = 600,
            Content = screen
        };
        window.Show();
        return (window, screen, clicks);
    }

    private static void Close(Window window, PlayScreen screen)
    {
        window.Close();
        screen.Dispose();
    }
}
