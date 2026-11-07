using System.Diagnostics.CodeAnalysis;
using OpenConquer.Client.World;
using OpenConquer.Content;
using OpenConquer.Content.Items;
using OpenConquer.Networking.Game.Framing;
using OpenConquer.Networking.Game.Login;
using OpenConquer.Rendering.Presentation;
using OpenConquer.Rendering.World;

namespace OpenConquer.Client.Tests.World;

public sealed class ClientWorldTests
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task ConnectionLoss_ClosesOnceAndReportsConnectionLost()
    {
        Harness harness = new(endOfStream: true);
        await using ClientWorld world = harness.Create();

        world.DrainIngress();

        Assert.False(world.IsActive);
        Assert.Equal(1, harness.ClosingCalls);
        await harness.SessionClosed.Task.WaitAsync(s_timeout, TestContext.Current.CancellationToken);
        Assert.True(world.TryObserveClose(out WorldCloseReason reason));
        Assert.Equal(WorldCloseReason.ConnectionLost, reason);
        Assert.False(world.TryObserveClose(out _));
        Assert.Equal(1, harness.SessionCloseCalls);
    }

    [Fact]
    public async Task CanceledMapLoad_KeepsTheWorldUntilTheLoadIsReaped()
    {
        Harness harness = new(endOfStream: true);
        await using ClientWorld world = harness.Create();

        world.DrainIngress();
        await harness.SessionClosed.Task.WaitAsync(s_timeout, TestContext.Current.CancellationToken);
        Assert.True(world.TryObserveClose(out _));
        Assert.False(world.IsClosed);

        // Task.Run may cancel before its delegate starts, so the delegate's finally is not a completion signal.
        await WaitUntilAsync(() => harness.Load!.IsCompleted);
        world.CompleteLoad();

        Assert.True(world.IsClosed);
        Assert.Equal(1, harness.ClosingCalls);
    }

    [Fact]
    public async Task BeginClose_IsIdempotentAndKeepsTheFirstReason()
    {
        Harness harness = new(endOfStream: false);
        await using ClientWorld world = harness.Create();

        world.BeginClose(WorldCloseReason.ContentUnavailable);
        world.BeginClose(WorldCloseReason.ConnectionLost);

        Assert.Equal(1, harness.ClosingCalls);
        Assert.False(world.IsPresentable);
    }

    [Fact]
    public async Task DisposeAsync_ClosesTheSessionAndCancelsTheMapLoad()
    {
        Harness harness = new(endOfStream: false);
        await using ClientWorld world = harness.Create();

        await world.DisposeAsync();
        world.BeginClose(WorldCloseReason.ConnectionLost);

        Assert.Equal(1, harness.SessionCloseCalls);
        Assert.True(harness.Load!.IsCompleted);
        Assert.Equal(0, harness.ClosingCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedMapLoad_DisposalStillStopsIngressAndClosesSession(bool failSession)
    {
        Harness harness = new(endOfStream: false, failLoad: true, failSession: failSession);
        ClientWorld world = harness.Create();
        await WaitUntilAsync(() => harness.Load!.IsCompleted);

        if (failSession)
        {
            AggregateException failure = await Assert.ThrowsAsync<AggregateException>(() => world.DisposeAsync().AsTask());
            Assert.Equal(2, failure.InnerExceptions.Count);
        }
        else
        {
            await Assert.ThrowsAsync<IOException>(() => world.DisposeAsync().AsTask());
        }

        Assert.True(harness.IngressStopped);
        Assert.Equal(1, harness.SessionCloseCalls);
        Assert.False(world.IsActive);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(s_timeout);
        while (!condition())
        {
            await Task.Delay(1, deadline.Token);
        }
    }

    private sealed class Harness(bool endOfStream, bool failLoad = false, bool failSession = false)
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
        public bool IngressStopped
        {
            get; private set;
        }

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
            return failSession ? ValueTask.FromException(new IOException("Session cleanup failed.")) : ValueTask.CompletedTask;
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
            if (failLoad)
            {
                throw new IOException("Map load failed.");
            }
            cancellationToken.WaitHandle.WaitOne();
            throw new OperationCanceledException(cancellationToken);
        }

        private async Task<GameSecuredInboundFrame?> WaitForeverAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            finally { IngressStopped = true; }
            return null;
        }

        private static GameWorldEntrySnapshot Entry() => new(
            new GameCharacterInfo1006 { EntityId = 0x11223344, Level = 1, CurrentProfession = 10 },
            1002, 1010, 0, 1, 61, 101, [], 3);
    }

    /// <summary>Serves only the item tables world construction needs; every map resource is missing.</summary>
    private sealed class MissingContentSource : IClientContentSource
    {
        public bool TryOpenRead(string contentPath, ContentLookupMode mode, [NotNullWhen(true)] out Stream? stream)
        {
            stream = contentPath switch
            {
                ItemStatCatalog.ItemTypePath => new MemoryStream(WorldHeroItemStatsTests.ItemTypeBytes()),
                ItemStatCatalog.ItemAddPath => new MemoryStream(WorldHeroItemStatsTests.ItemAddBytes()),
                _ => null,
            };
            return stream is not null;
        }

        public Stream OpenRequiredRead(string contentPath, ContentLookupMode mode) =>
            TryOpenRead(contentPath, mode, out Stream? stream)
                ? stream
                : throw new FileNotFoundException($"Content '{contentPath}' is unavailable.");
    }
}
