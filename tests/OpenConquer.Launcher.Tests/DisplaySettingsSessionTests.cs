using OpenConquer.Launcher.Settings;

namespace OpenConquer.Launcher.Tests;

public sealed class DisplaySettingsSessionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Shutdown_CancelsAndDrainsPendingIoBeforeCompleting(bool saving)
    {
        ControlledStore store = new();
        await using DisplaySettingsSession session = new(store);
        Task operation = saving ? session.SaveAsync(DisplayPreferences.Default, null, false) : session.LoadAsync();
        Task stop = session.StopAsync();
        Assert.True(store.Token.IsCancellationRequested);
        Assert.False(stop.IsCompleted);
        Assert.Same(stop, session.StopAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.LoadAsync());
        store.Release.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
        await stop.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await session.StopAsync();
    }

    [Fact]
    public async Task CancellationCallback_CanReenterShutdownWithoutStartingAnotherDrain()
    {
        ControlledStore store = new();
        await using DisplaySettingsSession session = new(store);
        Task operation = session.LoadAsync();
        TaskCompletionSource<Task> reentered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration registration = store.Token.Register(() => reentered.SetResult(session.StopAsync()));
        Task stop = session.StopAsync();
        Assert.Same(stop, await reentered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        store.Release.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
        await stop;
    }

    [Fact]
    public async Task PendingOperation_PreventsOverlappingSave()
    {
        ControlledStore store = new();
        await using DisplaySettingsSession session = new(store);
        Task operation = session.LoadAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.SaveAsync(DisplayPreferences.Default, null, false));
        store.Release.SetResult();
        await operation;
    }

    [Fact]
    public async Task Shutdown_PreservesUnexpectedOperationFailures()
    {
        ControlledStore store = new();
        DisplaySettingsSession session = new(store);
        Task operation = session.LoadAsync();
        Task stop = session.StopAsync();
        IOException failure = new("Test failure");
        store.Release.SetException(failure);
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => operation));
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => stop));
        Assert.Same(stop, session.StopAsync());
        await Assert.ThrowsAsync<IOException>(() => session.DisposeAsync().AsTask());
    }

    [Fact]
    public async Task Shutdown_DoesNotHideCancellationFromAnUnrelatedToken()
    {
        ControlledStore store = new();
        DisplaySettingsSession session = new(store);
        Task operation = session.LoadAsync();
        using CancellationTokenSource unrelated = new();
        await unrelated.CancelAsync();
        Task stop = session.StopAsync();
        store.Release.SetCanceled(unrelated.Token);
        OperationCanceledException operationFailure = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
        OperationCanceledException shutdownFailure = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stop);
        Assert.Equal(unrelated.Token, operationFailure.CancellationToken);
        Assert.Equal(unrelated.Token, shutdownFailure.CancellationToken);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.DisposeAsync().AsTask());
    }

    [Fact]
    public async Task FailingCancellationCallback_StillDrainsIoAndReportsFailure()
    {
        ControlledStore store = new();
        DisplaySettingsSession session = new(store);
        Task operation = session.LoadAsync();
        TaskCompletionSource callback = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration registration = store.Token.Register(() =>
        {
            callback.SetResult();
            throw new InvalidOperationException("Test callback failure");
        });
        Task stop = session.StopAsync();
        await callback.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.False(stop.IsCompleted);
        store.Release.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
        AggregateException failure = await Assert.ThrowsAsync<AggregateException>(() => stop);
        Assert.IsType<InvalidOperationException>(Assert.Single(failure.InnerExceptions));
        await Assert.ThrowsAsync<AggregateException>(() => session.DisposeAsync().AsTask());
    }

    [Fact]
    public async Task IdleShutdown_IsRepeatableAndRejectsFurtherWork()
    {
        await using DisplaySettingsSession session = new(new ControlledStore());
        Task first = session.StopAsync();
        await first;
        Assert.Same(first, session.StopAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.LoadAsync());
    }

    private sealed class ControlledStore : IDisplaySettingsStore
    {
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal CancellationToken Token
        {
            get; private set;
        }

        public async Task<SettingsReadResult> LoadAsync(CancellationToken cancellationToken)
        {
            await WaitAsync(cancellationToken);
            return new SettingsReadResult.Loaded(DisplayPreferences.Default, null);
        }

        public async Task<SettingsIssue?> SaveAsync(DisplayPreferences preferences, string? expectedRevision,
            bool resetInvalid, CancellationToken cancellationToken)
        {
            await WaitAsync(cancellationToken);
            return null;
        }

        private async Task WaitAsync(CancellationToken cancellationToken)
        {
            Token = cancellationToken;
            await Release.Task;
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
