using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;

namespace OpenConquer.Launcher.Tests;

// Tests that access Avalonia's global dispatcher share this session.
[CollectionDefinition(nameof(LauncherHeadlessUi))]
public sealed class LauncherHeadlessUi : ICollectionFixture<LauncherHeadlessFixture>;

public sealed class LauncherHeadlessFixture : IAsyncDisposable
{
    // Keep the dispatcher alive while prior windows' callbacks drain. Each test owns a fresh window.
    private readonly HeadlessUnitTestSession _session =
        HeadlessUnitTestSession.StartNew(typeof(LauncherHeadlessFixture), AvaloniaTestIsolationLevel.PerAssembly);

    public static AppBuilder BuildAvaloniaApp()
    {
        // AppBuilder invokes this on the session thread, before the headless compositor is created.
        Dispatcher.UIThread.VerifyAccess();
        return AppBuilder.Configure<Application>().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true });
    }

    internal Task Dispatch(Action test, CancellationToken token) => _session.Dispatch(test, token);

    internal Task Dispatch(Func<Task> test, CancellationToken token) => _session.Dispatch(async () =>
    {
        await test();
        return true;
    }, token);

    public ValueTask DisposeAsync() => _session.DisposeAsync();
}
