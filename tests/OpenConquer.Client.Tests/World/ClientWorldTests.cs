using System.Diagnostics.CodeAnalysis;
using OpenConquer.Client.World;
using OpenConquer.Content;
using OpenConquer.Networking.Game.Framing;
using OpenConquer.Networking.Game.Login;
using OpenConquer.Rendering.Presentation;
using OpenConquer.Rendering.World;

namespace OpenConquer.Client.Tests.World;

public sealed class ClientWorldTests
{
    [Fact]
    public async Task ConnectionLoss_ClosesOnceAndReportsConnectionLost()
    {
        Harness harness = new(endOfStream: true);
        ClientWorld world = harness.Create();

        world.DrainIngress();

        Assert.False(world.IsActive);
        Assert.Equal(1, harness.ClosingCalls);
        await harness.SessionClosed.Task.WaitAsync(TestContext.Current.CancellationToken);
        Assert.True(world.TryObserveClose(out WorldCloseReason reason));
        Assert.Equal(WorldCloseReason.ConnectionLost, reason);
        Assert.False(world.TryObserveClose(out _));
        Assert.Equal(1, harness.SessionCloseCalls);
    }

    [Fact]
    public async Task CanceledMapLoad_KeepsTheWorldUntilTheLoadIsReaped()
    {
        Harness harness = new(endOfStream: true);
        ClientWorld world = harness.Create();

        world.DrainIngress();
        await harness.SessionClosed.Task.WaitAsync(TestContext.Current.CancellationToken);
        Assert.True(world.TryObserveClose(out _));
        Assert.False(world.IsClosed);

        await harness.LoadFinished.Task.WaitAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => harness.Load!.IsCompleted);
        world.CompleteLoad(graphicsDevice: null);

        Assert.True(world.IsClosed);
        Assert.Equal(1, harness.ClosingCalls);
    }

    [Fact]
    public void BeginClose_IsIdempotentAndKeepsTheFirstReason()
    {
        Harness harness = new(endOfStream: false);
        ClientWorld world = harness.Create();

        world.BeginClose(WorldCloseReason.ContentUnavailable);
        world.BeginClose(WorldCloseReason.ConnectionLost);

        Assert.Equal(1, harness.ClosingCalls);
        Assert.False(world.IsPresentable);
    }

    [Fact]
    public async Task DisposeAsync_ClosesTheSessionAndCancelsTheMapLoad()
    {
        Harness harness = new(endOfStream: false);
        ClientWorld world = harness.Create();

        await world.DisposeAsync();

        Assert.Equal(1, harness.SessionCloseCalls);
        Assert.True(harness.Load!.IsCompleted);
        Assert.Equal(0, harness.ClosingCalls);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        while (!condition())
        {
            await Task.Delay(1, TestContext.Current.CancellationToken);
        }
    }

    private sealed class Harness(bool endOfStream)
    {
        public int ClosingCalls
        {
            get; private set;
        }

        public int SessionCloseCalls
        {
            get; private set;
        }

        public TaskCompletionSource SessionClosed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource LoadFinished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public WorldMapLoad? Load
        {
            get; private set;
        }

        public ClientWorld Create() => new(Entry(), new MissingContentSource(), new LogicalRenderSize(1024, 768),
            () => ClosingCalls++, CloseSessionAsync, CreateIngress, CreateLoad);

        private ValueTask CloseSessionAsync()
        {
            SessionCloseCalls++;
            SessionClosed.TrySetResult();
            return ValueTask.CompletedTask;
        }

        private WorldFrameIngress CreateIngress() => endOfStream
            ? new WorldFrameIngress(_ => Task.FromResult<GameSecuredInboundFrame?>(null))
            : new WorldFrameIngress(WaitForeverAsync);

        private WorldMapLoad CreateLoad()
        {
            Load = new WorldMapLoad(LoadUntilCanceled);
            return Load;
        }

        private WorldMapScene LoadUntilCanceled(CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.WaitHandle.WaitOne();
                throw new OperationCanceledException(cancellationToken);
            }
            finally
            {
                LoadFinished.TrySetResult();
            }
        }

        private static async Task<GameSecuredInboundFrame?> WaitForeverAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return null;
        }

        private static GameWorldEntrySnapshot Entry() => new(
            new GameCharacterInfo1006 { EntityId = 0x11223344, Level = 1, CurrentProfession = 10 },
            1002, 1010, 0, 1, 61, 101, [], 3);
    }

    private sealed class MissingContentSource : IClientContentSource
    {
        public bool TryOpenRead(string contentPath, ContentLookupMode mode, [NotNullWhen(true)] out Stream? stream)
        {
            stream = null;
            return false;
        }

        public Stream OpenRequiredRead(string contentPath, ContentLookupMode mode) =>
            throw new FileNotFoundException($"Content '{contentPath}' is unavailable.");
    }
}
