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
        WorldMapLoad load = new(_ =>
        {
            started.Set();
            release.Wait();
            return scene;
        });

        try
        {
            Assert.True(await Task.Run(() => started.Wait(TimeSpan.FromSeconds(5))));
            Assert.False(load.IsCompleted);
            release.Set();
            await Task.Run(() => SpinWait.SpinUntil(() => load.IsCompleted, TimeSpan.FromSeconds(5)));
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
        WorldMapLoad load = new(token =>
        {
            started.Set();
            token.WaitHandle.WaitOne();
            token.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Unreachable");
        });

        Assert.True(await Task.Run(() => started.Wait(TimeSpan.FromSeconds(5))));
        await Task.Run(load.CancelAndWait);
        Assert.True(load.IsCompleted);
    }

    [Fact]
    public async Task PreparationFailuresAreReturnedToTheOwner()
    {
        WorldMapLoad load = new(_ => throw new InvalidDataException("Invalid scene"));
        Assert.True(await Task.Run(() => SpinWait.SpinUntil(() => load.IsCompleted, TimeSpan.FromSeconds(5))));
        Assert.Throws<InvalidDataException>(load.Complete);
    }
}
