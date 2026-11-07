using OpenConquer.Client.World;

namespace OpenConquer.Client.Tests.World;

public sealed class WorldContentLoadTests
{
    [Fact]
    public async Task PreparationDoesNotBlockCallerAndCompletesAfterWorker()
    {
        using ManualResetEventSlim release = new();
        using ManualResetEventSlim started = new();
        object content = new();
        await using WorldContentLoad<object> load = new(token =>
        {
            started.Set();
            release.Wait(token);
            return content;
        });

        try
        {
            Assert.True(await Task.Run(() => started.Wait(TimeSpan.FromSeconds(5)), TestContext.Current.CancellationToken));
            Assert.False(load.IsCompleted);
            release.Set();
            await Task.Run(() => SpinWait.SpinUntil(() => load.IsCompleted, TimeSpan.FromSeconds(5)), TestContext.Current.CancellationToken);
            Assert.True(load.IsCompleted);
            Assert.Same(content, load.Complete());
        }
        finally
        {
            release.Set();
        }
    }

    [Fact]
    public async Task CancellationInterruptsAWaitingWorkerAndIsObserved()
    {
        using ManualResetEventSlim started = new();
        await using WorldContentLoad<object> load = new(token =>
        {
            started.Set();
            token.WaitHandle.WaitOne();
            token.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Unreachable");
        });

        Assert.True(await Task.Run(() => started.Wait(TimeSpan.FromSeconds(5)), TestContext.Current.CancellationToken));
        await load.DisposeAsync();
        Assert.True(load.IsCompleted);
    }

    [Fact]
    public async Task PreparationFailuresAreReturnedToTheOwner()
    {
        await using WorldContentLoad<object> load = new(_ => throw new InvalidDataException("Invalid scene"));
        Assert.True(await Task.Run(() => SpinWait.SpinUntil(() => load.IsCompleted, TimeSpan.FromSeconds(5)), TestContext.Current.CancellationToken));
        Assert.Throws<InvalidDataException>(load.Complete);
    }
}
