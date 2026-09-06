using OpenConquer.Launcher.Installation;

namespace OpenConquer.Launcher.Tests;

public sealed class LauncherApplicationTests
{
    [Fact]
    public async Task StopAsync_DrainsReentrantCallbacksBeforeCompleting()
    {
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseEvaluation = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<Task> reentered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using ManualResetEventSlim releaseCallback = new();
        CancellationToken testToken = TestContext.Current.CancellationToken;
        CancellationTokenRegistration registration = default;
        LauncherApplication? application = null;
        application = new LauncherApplication(new StubResolver(async token =>
        {
            registration = token.Register(() =>
            {
                reentered.SetResult(application!.StopAsync());
                Assert.True(releaseCallback.Wait(TimeSpan.FromSeconds(10), testToken));
            });
            entered.SetResult();
            await releaseEvaluation.Task;
            return new ManagedInstallationResolution.Rejected(ManagedInstallationIssue.ReadFailure);
        }));

        Task evaluation = application.StartAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), testToken);
        Task stopping = application.StopAsync();

        try
        {
            Task callbackShutdown = await reentered.Task.WaitAsync(TimeSpan.FromSeconds(10), testToken);
            releaseEvaluation.SetResult();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => evaluation);

            Assert.Same(stopping, callbackShutdown);
            Assert.False(stopping.IsCompleted);
            Assert.IsType<LauncherState.Stopping>(application.State);
        }
        finally
        {
            releaseEvaluation.TrySetResult();
            releaseCallback.Set();
            await stopping;
            await registration.DisposeAsync();
        }

        Assert.IsType<LauncherState.Stopped>(application.State);
    }

    [Fact]
    public async Task StopAsync_PreservesCallbackAndEvaluationFailures()
    {
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        InvalidOperationException callbackFailure = new("callback failure");
        IOException evaluationFailure = new("unexpected resolver failure");
        CancellationTokenRegistration registration = default;
        LauncherApplication application = new(new StubResolver(async token =>
        {
            registration = token.Register(() => throw callbackFailure);
            entered.SetResult();
            await release.Task;
            throw evaluationFailure;
        }));

        Task evaluation = application.StartAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Task stopping = application.StopAsync();
        release.SetResult();

        try
        {
            AggregateException failure = await Assert.ThrowsAsync<AggregateException>(() => stopping);
            Assert.Equal(new Exception[] { callbackFailure, evaluationFailure }.ToHashSet(),
                failure.Flatten().InnerExceptions.ToHashSet());
            Assert.Same(evaluationFailure, await Assert.ThrowsAsync<IOException>(() => evaluation));
            Assert.IsType<LauncherState.Stopped>(application.State);
        }
        finally
        {
            await registration.DisposeAsync();
        }
    }

    [Fact]
    public async Task RetryInstallationAsync_RecoversWithoutRestartingTheLauncher()
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "managed-installation"));
        ManagedInstallation installation = ManagedInstallation.Create(
            root, Path.Combine(root, "client"), Path.Combine(root, ManagedInstallationManifest.FileName));
        int calls = 0;
        await using LauncherApplication application = new(new StubResolver(_ =>
            Task.FromResult<ManagedInstallationResolution>(++calls == 1
                ? new ManagedInstallationResolution.Rejected(ManagedInstallationIssue.ClientComponentMissing)
                : new ManagedInstallationResolution.Resolved(installation))));

        await application.StartAsync();
        Assert.IsType<LauncherState.InstallationUnavailable>(application.State);
        await application.RetryInstallationAsync();

        Assert.Same(installation, Assert.IsType<LauncherState.InstallationResolved>(application.State).Installation);
        Assert.Equal(2, calls);
        await Assert.ThrowsAsync<InvalidOperationException>(application.RetryInstallationAsync);
    }

    [Fact]
    public async Task RetryInstallationAsync_CoalescesRequestsAndRemainsRetryableAfterExpectedFailure()
    {
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        await using LauncherApplication application = new(new StubResolver(async _ =>
        {
            if (++calls == 2)
            {
                entered.SetResult();
                await release.Task;
            }

            return new ManagedInstallationResolution.Rejected(ManagedInstallationIssue.ReadFailure);
        }));

        await application.StartAsync();
        Task first = application.RetryInstallationAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Task second = application.RetryInstallationAsync();
        release.SetResult();
        await Task.WhenAll(first, second);

        Assert.Same(first, second);
        Assert.Equal(2, calls);
        Assert.IsType<LauncherState.InstallationUnavailable>(application.State);
        await application.RetryInstallationAsync();
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task RetryInstallationAsync_ShutdownCancelsRetryAndPreventsFurtherEvaluation()
    {
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        LauncherApplication application = new(new StubResolver(async token =>
        {
            if (++calls == 2)
            {
                entered.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }

            return new ManagedInstallationResolution.Rejected(ManagedInstallationIssue.ReadFailure);
        }));

        await application.StartAsync();
        Task retry = application.RetryInstallationAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await application.StopAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => retry);
        await Assert.ThrowsAsync<InvalidOperationException>(application.RetryInstallationAsync);
        Assert.IsType<LauncherState.Stopped>(application.State);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task RetryInstallationAsync_UnexpectedFaultIsNotExposedAsRecoverable()
    {
        InvalidOperationException failure = new("unexpected resolver failure");
        int calls = 0;
        LauncherApplication application = new(new StubResolver(_ => ++calls == 1
            ? Task.FromResult<ManagedInstallationResolution>(new ManagedInstallationResolution.Rejected(ManagedInstallationIssue.ReadFailure))
            : Task.FromException<ManagedInstallationResolution>(failure)));

        await application.StartAsync();
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(application.RetryInstallationAsync));
        Assert.IsType<LauncherState.Faulted>(application.State);
        await Assert.ThrowsAsync<InvalidOperationException>(application.RetryInstallationAsync);
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(application.StopAsync));
        Assert.IsType<LauncherState.Stopped>(application.State);
    }

    [Fact]
    public async Task StopAsync_BeforeStartupIsTerminalAndIdempotent()
    {
        LauncherApplication application = new(new StubResolver(_ =>
            throw new InvalidOperationException("Resolver must not run.")));

        await Assert.ThrowsAsync<InvalidOperationException>(application.RetryInstallationAsync);
        Task stopping = application.StopAsync();
        await stopping;
        await application.DisposeAsync();

        Assert.Same(stopping, application.StopAsync());
        Assert.IsType<LauncherState.Stopped>(application.State);
        await Assert.ThrowsAsync<InvalidOperationException>(application.StartAsync);
        await Assert.ThrowsAsync<InvalidOperationException>(application.RetryInstallationAsync);
    }

    [Fact]
    public async Task StopAsync_UnrelatedCancellationRemainsAFault()
    {
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        OperationCanceledException failure = new(CancellationToken.None);
        LauncherApplication application = new(new StubResolver(async _ =>
        {
            entered.SetResult();
            await release.Task;
            throw failure;
        }));

        Task evaluation = application.StartAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Task stopping = application.StopAsync();
        release.SetResult();

        InvalidOperationException observed = await Assert.ThrowsAsync<InvalidOperationException>(() => evaluation);
        Assert.Same(failure, observed.InnerException);
        Assert.Same(observed, await Assert.ThrowsAsync<InvalidOperationException>(() => stopping));
        Assert.IsType<LauncherState.Stopped>(application.State);
    }

    [Fact]
    public async Task StopAsync_DoesNotReclassifyAnEarlierUnsolicitedLifetimeCancellation()
    {
        LauncherApplication application = new(new StubResolver(token =>
            Task.FromException<ManagedInstallationResolution>(new OperationCanceledException(token))));

        InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(application.StartAsync);
        Assert.IsType<OperationCanceledException>(failure.InnerException);
        Assert.IsType<LauncherState.Faulted>(application.State);

        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(application.StopAsync));
        Assert.IsType<LauncherState.Stopped>(application.State);
    }

    [Fact]
    public async Task StopAsync_CallbackFailureStillDrainsEvaluationAndTerminates()
    {
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        InvalidOperationException failure = new("cancellation callback failed");
        CancellationTokenRegistration registration = default;
        LauncherApplication application = new(new StubResolver(async token =>
        {
            registration = token.Register(() => throw failure);
            entered.SetResult();
            await release.Task;
            token.ThrowIfCancellationRequested();
            return new ManagedInstallationResolution.Rejected(ManagedInstallationIssue.ReadFailure);
        }));

        Task evaluation = application.StartAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Task stopping = application.StopAsync();
        release.SetResult();

        try
        {
            AggregateException actual = await Assert.ThrowsAsync<AggregateException>(() => stopping);
            Assert.Contains(failure, actual.InnerExceptions);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => evaluation);
            Assert.IsType<LauncherState.Stopped>(application.State);
            Assert.Same(stopping, application.StopAsync());
        }
        finally
        {
            await registration.DisposeAsync();
        }
    }

    [Fact]
    public async Task StopAsync_ConcurrentCallersShareTheCompleteShutdownOperation()
    {
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        LauncherApplication application = new(new StubResolver(async token =>
        {
            entered.SetResult();
            await release.Task;
            token.ThrowIfCancellationRequested();
            return new ManagedInstallationResolution.Rejected(ManagedInstallationIssue.ReadFailure);
        }));

        Task evaluation = application.StartAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Task first = application.StopAsync();
        Task second = application.StopAsync();
        release.SetResult();
        await Task.WhenAll(first, second);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => evaluation);

        Assert.Same(first, second);
        Assert.Same(first, application.StopAsync());
        Assert.IsType<LauncherState.Stopped>(application.State);
    }

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
        LauncherApplication application = new(
            new StubResolver(new ManagedInstallationResolution.Resolved(installation))
        );

        await application.StartAsync();

        LauncherState.InstallationResolved state =
            Assert.IsType<LauncherState.InstallationResolved>(application.State);

        Assert.Equal(installation, state.Installation);

        await application.StopAsync();

        Assert.IsType<LauncherState.Stopped>(application.State);
    }

    [Fact]
    public async Task StartAsyncRejectsASecondStartup()
    {
        LauncherApplication application = new(
            new StubResolver(
                new ManagedInstallationResolution.Rejected(ManagedInstallationIssue.ManifestMissing)
            )
        );

        await application.StartAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(application.StartAsync);

        await application.StopAsync();
    }

    [Fact]
    public async Task StopAsyncCancelsAnInFlightInstallationEvaluation()
    {
        TaskCompletionSource resolverEntered = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        StubResolver resolver = new(async cancellationToken =>
        {
            resolverEntered.SetResult();

            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

            return new ManagedInstallationResolution.Rejected(ManagedInstallationIssue.ReadFailure);
        });

        LauncherApplication application = new(resolver);

        Task evaluation = application.StartAsync();

        await resolverEntered.Task;

        await application.StopAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => evaluation);

        Assert.IsType<LauncherState.Stopped>(application.State);
    }

    [Fact]
    public async Task StopAsyncOwnsStateAfterShutdownBegins()
    {
        TaskCompletionSource resolverEntered = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        TaskCompletionSource<ManagedInstallationResolution> resolverCompletion = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        StubResolver resolver = new(async _ =>
        {
            resolverEntered.SetResult();

            return await resolverCompletion.Task;
        });

        LauncherApplication application = new(resolver);

        Task evaluation = application.StartAsync();

        await resolverEntered.Task;

        Task stopping = application.StopAsync();

        Assert.IsType<LauncherState.Stopping>(application.State);

        resolverCompletion.SetResult(
            new ManagedInstallationResolution.Rejected(
                ManagedInstallationIssue.ClientComponentMissing
            )
        );

        await stopping;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => evaluation);

        Assert.IsType<LauncherState.Stopped>(application.State);
    }

    [Fact]
    public async Task ConcurrentStopAsyncCallsShareShutdownCancellationSemantics()
    {
        TaskCompletionSource resolverEntered = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        StubResolver resolver = new(async cancellationToken =>
        {
            resolverEntered.SetResult();

            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

            return new ManagedInstallationResolution.Rejected(ManagedInstallationIssue.ReadFailure);
        });

        LauncherApplication application = new(resolver);

        _ = application.StartAsync();

        await resolverEntered.Task;

        Task firstStop = application.StopAsync();
        Task secondStop = application.StopAsync();

        await Task.WhenAll(firstStop, secondStop);

        Assert.IsType<LauncherState.Stopped>(application.State);
    }

    [Fact]
    public async Task StopAsyncReleasesLifetimeAfterAnUnexpectedEvaluationFailure()
    {
        InvalidOperationException failure = new("resolver failure");
        LauncherApplication application = new(
            new StubResolver(_ => Task.FromException<ManagedInstallationResolution>(failure))
        );

        Task evaluation = application.StartAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => evaluation);

        InvalidOperationException stopFailure = await Assert.ThrowsAsync<InvalidOperationException>(
            application.StopAsync
        );

        Assert.Same(failure, stopFailure);
        Assert.IsType<LauncherState.Stopped>(application.State);
    }

    [Fact]
    public async Task EvaluationFailureCannotReplaceStoppingState()
    {
        TaskCompletionSource resolverEntered = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        TaskCompletionSource<ManagedInstallationResolution> resolverCompletion = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        StubResolver resolver = new(async _ =>
        {
            resolverEntered.SetResult();

            return await resolverCompletion.Task;
        });

        LauncherApplication application = new(resolver);

        Task evaluation = application.StartAsync();

        await resolverEntered.Task;

        Task stopping = application.StopAsync();

        Assert.IsType<LauncherState.Stopping>(application.State);

        InvalidOperationException failure = new("resolver failure");

        resolverCompletion.SetException(failure);

        InvalidOperationException stopFailure = await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                stopping
        );

        InvalidOperationException evaluationFailure =
            await Assert.ThrowsAsync<InvalidOperationException>(() => evaluation);

        Assert.Same(failure, evaluationFailure);
        Assert.Same(failure, stopFailure);
        Assert.IsType<LauncherState.Stopped>(application.State);
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
            ArgumentNullException.ThrowIfNull(resolve);

            _resolve = resolve;
        }

        public Task<ManagedInstallationResolution> ResolveAsync(CancellationToken cancellationToken)
        {
            return _resolve(cancellationToken);
        }
    }
}
