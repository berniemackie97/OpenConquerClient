using OpenConquer.Launcher.Installation;

namespace OpenConquer.Launcher.Tests;

public sealed class LauncherApplicationTests
{
    [Fact]
    public void ManagedInstallationRejectsPathsOutsideItsProductRoot()
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "managed-installation"));

        Assert.Throws<ArgumentException>(() =>
            ManagedInstallation.Create(
                root,
                Path.Combine(Path.GetDirectoryName(root)!, "outside-client"),
                Path.Combine(root, ManagedInstallationManifest.FileName)
            )
        );
    }

    [Fact]
    public void ManagedInstallationHandlesAFilesystemRootWithoutChangingContainmentRules()
    {
        string root = Path.GetPathRoot(Path.GetFullPath(Path.GetTempPath()))!;

        ManagedInstallation installation = ManagedInstallation.Create(
            root,
            Path.Combine(root, "client"),
            Path.Combine(root, ManagedInstallationManifest.FileName)
        );

        Assert.Equal(Path.GetFullPath(root), installation.RootPath);
    }

    [Fact]
    public async Task StartAsyncAutomaticallyEvaluatesManagedInstallation()
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "managed-installation"));
        ManagedInstallation installation = ManagedInstallation.Create(
            root,
            Path.Combine(root, "client"),
            Path.Combine(root, ManagedInstallationManifest.FileName)
        );
        LauncherApplication application = new(new StubResolver(
            new ManagedInstallationResolution.Resolved(installation)));

        await application.StartAsync();

        LauncherState.InstallationResolved state = Assert.IsType<LauncherState.InstallationResolved>(application.State);
        Assert.Equal(installation, state.Installation);

        await application.StopAsync();
        Assert.IsType<LauncherState.Stopped>(application.State);
    }

    [Fact]
    public async Task StartAsyncRejectsASecondStartup()
    {
        LauncherApplication application = new(new StubResolver(
            new ManagedInstallationResolution.Rejected(ManagedInstallationIssue.ManifestMissing)));

        await application.StartAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(application.StartAsync);
        await application.StopAsync();
    }

    [Fact]
    public async Task StopAsyncCancelsAnInFlightInstallationEvaluation()
    {
        using CancellationTokenSource resolverEntered = new();
        StubResolver resolver = new(async cancellationToken =>
        {
            resolverEntered.Cancel();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new ManagedInstallationResolution.Rejected(ManagedInstallationIssue.ReadFailure);
        });
        LauncherApplication application = new(resolver);

        Task evaluation = application.StartAsync();
        await SpinWaitAsync(() => resolverEntered.IsCancellationRequested);

        await application.StopAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => evaluation);
        Assert.IsType<LauncherState.Stopped>(application.State);
    }

    [Fact]
    public async Task ConcurrentStopAsyncCallsShareShutdownCancellationSemantics()
    {
        using CancellationTokenSource resolverEntered = new();
        StubResolver resolver = new(async cancellationToken =>
        {
            resolverEntered.Cancel();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new ManagedInstallationResolution.Rejected(ManagedInstallationIssue.ReadFailure);
        });
        LauncherApplication application = new(resolver);

        _ = application.StartAsync();
        await SpinWaitAsync(() => resolverEntered.IsCancellationRequested);

        Task firstStop = application.StopAsync();
        Task secondStop = application.StopAsync();

        await Task.WhenAll(firstStop, secondStop);
        Assert.IsType<LauncherState.Stopped>(application.State);
    }

    [Fact]
    public async Task StopAsyncReleasesLifetimeAfterAnUnexpectedEvaluationFailure()
    {
        InvalidOperationException failure = new("resolver failure");
        LauncherApplication application = new(new StubResolver(_ => Task.FromException<ManagedInstallationResolution>(failure)));

        Task evaluation = application.StartAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => evaluation);

        InvalidOperationException stopFailure = await Assert.ThrowsAsync<InvalidOperationException>(application.StopAsync);

        Assert.Same(failure, stopFailure);
        Assert.IsType<LauncherState.Stopped>(application.State);
    }

    private static async Task SpinWaitAsync(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 100 && !condition(); attempt++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    private sealed class StubResolver : IManagedInstallationResolver
    {
        private readonly Func<CancellationToken, Task<ManagedInstallationResolution>> _resolve;

        public StubResolver(ManagedInstallationResolution resolution)
        {
            _resolve = _ => Task.FromResult(resolution);
        }

        public StubResolver(Func<CancellationToken, Task<ManagedInstallationResolution>> resolve)
        {
            _resolve = resolve;
        }

        public Task<ManagedInstallationResolution> ResolveAsync(CancellationToken cancellationToken)
        {
            return _resolve(cancellationToken);
        }
    }
}
