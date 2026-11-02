using OpenConquer.Client.World;

namespace OpenConquer.Client.Tests.World;

public sealed class WorldMapLoadTests
{
    [Fact]
    public async Task PreparationDoesNotBlockCallerAndCompletesAfterWorker()
    {
        using ManualResetEventSlim release = new();
        using ManualResetEventSlim started = new();
        using WorldMapFixture fixture = new();
        WorldMapScene scene = new(fixture.Map, fixture.Puzzles, [], 60, 108, 100, 1000);
        using WorldMapLoad load = new(token =>
        {
            started.Set();
            release.Wait(token);
            return scene;
        });

        try
        {
            Assert.True(await Task.Run(() => started.Wait(TimeSpan.FromSeconds(5)), TestContext.Current.CancellationToken));
            Assert.False(load.IsCompleted);
            release.Set();
            await Task.Run(() => SpinWait.SpinUntil(() => load.IsCompleted, TimeSpan.FromSeconds(5)), TestContext.Current.CancellationToken);
            Assert.True(load.IsCompleted);
            Assert.Same(scene, load.Complete());
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
        using WorldMapLoad load = new(token =>
        {
            started.Set();
            token.WaitHandle.WaitOne();
            token.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Unreachable");
        });

        Assert.True(await Task.Run(() => started.Wait(TimeSpan.FromSeconds(5)), TestContext.Current.CancellationToken));
        await Task.Run(load.Dispose, TestContext.Current.CancellationToken);
        Assert.True(load.IsCompleted);
    }

    [Fact]
    public async Task PreparationFailuresAreReturnedToTheOwner()
    {
        using WorldMapLoad load = new(_ => throw new InvalidDataException("Invalid scene"));
        Assert.True(await Task.Run(() => SpinWait.SpinUntil(() => load.IsCompleted, TimeSpan.FromSeconds(5)), TestContext.Current.CancellationToken));
        Assert.Throws<InvalidDataException>(load.Complete);
    }
}
