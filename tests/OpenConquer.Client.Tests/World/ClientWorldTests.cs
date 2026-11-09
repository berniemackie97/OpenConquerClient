using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using OpenConquer.Client.Diagnostics;
using OpenConquer.Client.Tests.Diagnostics;
using OpenConquer.Client.World;
using OpenConquer.Content;
using OpenConquer.Content.Items;
using OpenConquer.Content.Progression;
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

        world.DrainIngress(frameMilliseconds: 0);

        Assert.False(world.IsActive);
        Assert.Equal(1, harness.ClosingCalls);
        await harness.SessionClosed.Task.WaitAsync(s_timeout, TestContext.Current.CancellationToken);
        WorldCloseReason reason = default;
        await WaitUntilAsync(() => world.TryObserveClose(out reason));
        Assert.Equal(WorldCloseReason.ConnectionLost, reason);
        Assert.False(world.TryObserveClose(out _));
        Assert.Equal(1, harness.SessionCloseCalls);
    }

    [Fact]
    public async Task IngressFailureReportsOnlyTheNumericCodeAndStillCloses()
    {
        const string PrivateDetail = "/private/synthetic-account/ticket-secret";
        RecordingClientLogger logger = new();
        Harness harness = new(endOfStream: false, ingressFailure: new IOException(PrivateDetail));
        await using ClientWorld world = harness.Create(diagnostics: new ClientDiagnostics(logger));

        world.DrainIngress(frameMilliseconds: 0);

        WorldCloseReason reason = default;
        await WaitUntilAsync(() => world.TryObserveClose(out reason));
        Assert.Equal(WorldCloseReason.ConnectionLost, reason);
        Assert.Equal(1, harness.SessionCloseCalls);
        RecordingClientLogger.RecordedEvent logged = Assert.Single(logger.Events);
        Assert.Equal((2300, LogLevel.Warning), (logged.Id, logged.Level));
        Assert.Null(logged.Exception);
        Assert.DoesNotContain(PrivateDetail, logged.Message, StringComparison.Ordinal);
        KeyValuePair<string, object?> failure = Assert.Single(logged.Fields, field => field.Key == "FailureCode");
        Assert.Equal((int)ClientFailureCode.Io, failure.Value);
    }

    [Fact]
    public async Task ReentrantCloseAndThrowingNotificationStillCloseAndAggregate()
    {
        Harness harness = new(endOfStream: false, failSession: true);
        InvalidOperationException notification = new("World close notification failed.");
        ClientWorld? world = null;
        world = harness.Create(onClosing: () =>
        {
            ClientWorld closing = Assert.IsType<ClientWorld>(world);
            Assert.False(closing.IsActive);
            closing.BeginClose(WorldCloseReason.ContentUnavailable);
            throw notification;
        });

        world.BeginClose(WorldCloseReason.ConnectionLost);
        Task first = world.DisposeAsync().AsTask();
        Assert.Same(first, world.DisposeAsync().AsTask());

        AggregateException failures = await Assert.ThrowsAsync<AggregateException>(
            () => first.WaitAsync(s_timeout, TestContext.Current.CancellationToken));
        Assert.Equal(2, failures.InnerExceptions.Count);
        Assert.Contains(notification, failures.InnerExceptions);
        Assert.Contains(failures.InnerExceptions, failure => failure is IOException { Message: "Session cleanup failed." });
        Assert.Equal(1, harness.ClosingCalls);
        Assert.Equal(1, harness.SessionCloseCalls);
    }

    [Fact]
    public async Task CanceledMapLoad_KeepsTheWorldUntilTheLoadIsReaped()
    {
        Harness harness = new(endOfStream: true);
        await using ClientWorld world = harness.Create();

        world.DrainIngress(frameMilliseconds: 0);
        await harness.SessionClosed.Task.WaitAsync(s_timeout, TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => world.TryObserveClose(out _));
        Assert.False(world.IsClosed);

        // Task.Run may cancel before its delegate starts, so the delegate's finally is not a completion signal.
        await WaitUntilAsync(() => harness.Load!.IsCompleted);
        await WaitUntilAsync(() =>
        {
            world.CompleteLoad();
            return world.IsClosed;
        });
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

    [Fact]
    public async Task CatalogGate_KeepsMapUnpublishedAndReplaysHydrationBeforePrefetchedFrame()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TaskCompletionSource reading = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        CatalogContentSource content = new(() =>
        {
            reading.SetResult();
            release.Task.WaitAsync(s_timeout, token).GetAwaiter().GetResult();
        });
        using WorldMapFixture fixture = new();
        WorldMapScene scene = new(fixture.Map, fixture.Puzzles, [], fixture.Scenery, 61, 101, 100, 1000);
        GameWorldEntrySnapshot entry = Entry() with
        {
            HydrationPackets = [Correction(62, 102)],
        };
        int receives = 0;
        Task<GameSecuredInboundFrame?> Receive(CancellationToken stopping)
        {
            if (++receives == 1)
            {
                byte[] packet = Correction(63, 103);
                byte[] rented = ArrayPool<byte>.Shared.Rent(packet.Length);
                packet.CopyTo(rented, 0);
                return Task.FromResult<GameSecuredInboundFrame?>(new GameSecuredInboundFrame(rented, packet.Length));
            }

            return WaitForDisconnectAsync(stopping);
        }

        await using ClientWorld world = new(entry, content, new LogicalRenderSize(1024, 768), static () => { },
            static () => ValueTask.CompletedTask, () => new WorldFrameIngress(Receive),
            () => new WorldContentLoad<WorldMapScene>(_ => scene));

        try
        {
            await reading.Task.WaitAsync(s_timeout, token);
            world.CompleteLoad();
            world.DrainIngress(frameMilliseconds: 0);
            Assert.False(world.HasScene);
            Assert.Equal(1, receives);

            release.SetResult();
            await WaitUntilAsync(() =>
            {
                world.CompleteLoad();
                return world.HasScene;
            });
            AssertCameraAt(62, 102);
            world.DrainIngress(frameMilliseconds: 0);
            Assert.Equal(2, receives);
            AssertCameraAt(63, 103);
        }
        finally
        {
            release.TrySetResult();
        }

        void AssertCameraAt(int x, int y)
        {
            WorldMapScene expected = new(fixture.Map, fixture.Puzzles, [], fixture.Scenery, x, y, 100, 1000);
            expected.SetHeroDirection(entry.DirectionIndex);
            _ = scene.BuildFrame(640, 480, 1000);
            _ = expected.BuildFrame(640, 480, 1000);
            Assert.Equal(expected.HeroMatrices, scene.HeroMatrices);
        }
    }

    [Fact]
    public async Task AdmittedManagedPeerFramesUpdateSceneWithoutGraphicsPreparation()
    {
        byte[][] packets = ManagedPeerFrames.Load();
        TaskCompletionSource<GameSecuredInboundFrame?>[] deliveries =
        [
            .. packets.Select(_ => new TaskCompletionSource<GameSecuredInboundFrame?>(
                TaskCreationOptions.RunContinuationsAsynchronously))
        ];
        int nextRead = 0;
        Task<GameSecuredInboundFrame?> Receive(CancellationToken stopping) =>
            nextRead < deliveries.Length ? deliveries[nextRead++].Task : WaitForDisconnectAsync(stopping);

        using WorldMapFixture fixture = new();
        WorldMapScene scene = new(fixture.Map, fixture.Puzzles, [], fixture.Scenery, 60, 108, 100, 1000);
        GameWorldEntrySnapshot entry = Entry() with
        {
            Character = new GameCharacterInfo1006 { EntityId = 1_000_000, Level = 1, CurrentProfession = 10 },
            X = 60,
            Y = 108
        };
        await using ClientWorld world = new(entry, new CatalogContentSource(), new LogicalRenderSize(1024, 768),
            static () => { }, static () => ValueTask.CompletedTask,
            () => new WorldFrameIngress(Receive),
            () => new WorldContentLoad<WorldMapScene>(_ => scene));

        await WaitUntilAsync(() =>
        {
            world.CompleteLoad();
            return world.HasScene;
        });

        Assert.Empty(scene.CommittedRoleOrder);
        (int X, int Y)[] expectedPositions = [(60, 108), (61, 108), (60, 108)];
        for (int index = 0; index < packets.Length; index++)
        {
            byte[] packet = packets[index];
            byte[] rented = ArrayPool<byte>.Shared.Rent(packet.Length);
            packet.CopyTo(rented, 0);
            deliveries[index].SetResult(new GameSecuredInboundFrame(rented, packet.Length));

            uint now = (uint)(1000 + index * 34);
            world.DrainIngress(now);
            scene.BuildFrame(800, 600, now);

            if (index == 3)
            {
                Assert.DoesNotContain(scene.CommittedRoleOrder,
                    static slot => slot.Role.EntityId == 1_000_001);
            }
            else
            {
                WorldSceneRole peer = Assert.Single(scene.CommittedRoleOrder,
                    static slot => slot.Role.EntityId == 1_000_001).Role;
                Assert.Equal(expectedPositions[index], (peer.TileX, peer.TileY));
                if (index > 0)
                {
                    Assert.Equal(index == 1 ? (byte)6 : (byte)2, peer.Direction);
                }
            }

            Assert.True(world.IsActive);
        }

        world.DrainIngress(1200);
        scene.BuildFrame(800, 600, 1200);
        Assert.DoesNotContain(scene.CommittedRoleOrder, static slot => slot.Role.EntityId == 1_000_001);
        Assert.Equal(4, nextRead);
    }

    [Fact]
    public async Task ActiveWorldRoutesReplayedAcknowledgementsThroughTheOutboundLane()
    {
        using WorldMapFixture fixture = new();
        WorldMapScene scene = new(fixture.Map, fixture.Puzzles, [], fixture.Scenery, 61, 101, 100, 1000);
        GameWorldEntrySnapshot entry = Entry();
        byte[] full = new byte[GameWorldAction10010.PacketLength];
        GameWorldAction10010.Write(full, GameWorldAction10010.GetItems, entry.Character.EntityId);
        byte[] shortFrame = full[..22];
        BinaryPrimitives.WriteUInt16LittleEndian(shortFrame, checked((ushort)shortFrame.Length));
        byte[] next = new byte[GameWorldAction10010.PacketLength];
        GameWorldAction10010.Write(next, GameWorldAction10010.GetFriends, entry.Character.EntityId);
        List<(GameWorldActionResponse Reply, uint Now)> sent = [];
        int reads = 0;

        Task<GameSecuredInboundFrame?> Receive(CancellationToken stopping)
        {
            byte[]? message = ++reads switch
            {
                1 => shortFrame,
                2 => next,
                _ => null
            };
            if (message is null)
            {
                return WaitForDisconnectAsync(stopping);
            }

            byte[] rented = ArrayPool<byte>.Shared.Rent(message.Length);
            message.CopyTo(rented, 0);
            return Task.FromResult<GameSecuredInboundFrame?>(new GameSecuredInboundFrame(rented, message.Length));
        }

        await using ClientWorld world = new(entry, new CatalogContentSource(), new LogicalRenderSize(1024, 768),
            static () => { }, static () => ValueTask.CompletedTask,
            () => new WorldFrameIngress(Receive), () => new WorldContentLoad<WorldMapScene>(_ => scene),
            () => new WorldFrameEgress(
                (_, _) => Task.CompletedTask,
                (_, _, _, _) => Task.CompletedTask,
                (_, _, _) => Task.CompletedTask,
                (reply, now, _) =>
                {
                    sent.Add((reply, now));
                    return Task.CompletedTask;
                }));

        await WaitUntilAsync(() =>
        {
            world.CompleteLoad();
            return world.HasScene;
        });
        world.DrainIngress(500);
        world.DrainIngress(501);
        world.DrainIngress(502);

        Assert.Equal(
        [
            (new GameWorldActionResponse(GameWorldAction10010.GetFriends, entry.Character.EntityId, 0), 500u),
            (new GameWorldActionResponse(GameWorldAction10010.SetPkMode, entry.Character.EntityId, 3), 501u),
            (new GameWorldActionResponse(GameWorldAction10010.GetWeaponSkills, entry.Character.EntityId, 0), 502u)
        ], sent);
    }

    [Fact]
    public async Task HydrationAndLiveHeroCorrectionsShareTheOutboundLaneInOrder()
    {
        using WorldMapFixture fixture = new();
        WorldMapScene scene = new(fixture.Map, fixture.Puzzles, [], fixture.Scenery, 61, 101, 100, 1000);
        GameWorldEntrySnapshot entry = Entry() with
        {
            HydrationPackets = [Correction(61, 101), Correction(61, 101)],
        };
        List<(WorldCorrectionReply Reply, uint Tick)> sent = [];
        int receives = 0;
        Task<GameSecuredInboundFrame?> Receive(CancellationToken stopping)
        {
            receives++;
            if (receives > 3)
            {
                return WaitForDisconnectAsync(stopping);
            }

            byte[] packet = receives switch
            {
                1 => Correction(63, 103),
                2 => Correction(62, 102),
                _ => Correction(62, 102),
            };
            if (receives == 1)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), entry.Character.EntityId + 1);
            }

            byte[] rented = ArrayPool<byte>.Shared.Rent(packet.Length);
            packet.CopyTo(rented, 0);
            return Task.FromResult<GameSecuredInboundFrame?>(new GameSecuredInboundFrame(rented, packet.Length));
        }

        await using ClientWorld world = new(entry, new CatalogContentSource(), new LogicalRenderSize(1024, 768),
            static () => { }, static () => ValueTask.CompletedTask,
            () => new WorldFrameIngress(Receive), () => new WorldContentLoad<WorldMapScene>(_ => scene),
            () => new WorldFrameEgress(
                (_, _) => Task.CompletedTask,
                (_, _, _, _) => Task.CompletedTask,
                (reply, tick, _) =>
                {
                    sent.Add((reply, tick));
                    return Task.CompletedTask;
                }));

        await WaitUntilAsync(() =>
        {
            world.CompleteLoad();
            return world.HasScene;
        });

        world.DrainIngress(500);
        world.DrainIngress(501);
        world.DrainIngress(502);
        world.DrainIngress(503);

        Assert.Equal(
        [
            (new WorldCorrectionReply(61, 101, 3), 500u),
            (new WorldCorrectionReply(61, 101, 3), 501u),
            (new WorldCorrectionReply(62, 102, 3), 502u),
            (new WorldCorrectionReply(62, 102, 3), 503u),
        ], sent);
        Assert.Equal(4, sent.Count);
    }

    [Fact]
    public async Task HydrationReplyOverflowIsClassifiedAsConnectionLoss()
    {
        using WorldMapFixture fixture = new();
        WorldMapScene scene = new(fixture.Map, fixture.Puzzles, [], fixture.Scenery, 61, 101, 100, 1000);
        byte[][] excessCorrections = Enumerable.Range(0, WorldFrameEgress.MaximumQueuedResponses + 1)
            .Select(_ => Correction(61, 101)).ToArray();
        GameWorldEntrySnapshot entry = Entry() with
        {
            HydrationPackets = excessCorrections,
        };

        await using ClientWorld world = new(entry, new CatalogContentSource(), new LogicalRenderSize(1024, 768),
            static () => { }, static () => ValueTask.CompletedTask,
            () => new WorldFrameIngress(WaitForDisconnectAsync),
            () => new WorldContentLoad<WorldMapScene>(_ => scene),
            () => new WorldFrameEgress(
                (_, _) => Task.CompletedTask,
                (_, _, _, _) => Task.CompletedTask,
                (_, _, _) => Task.CompletedTask));

        await WaitUntilAsync(() =>
        {
            world.CompleteLoad();
            return !world.IsActive;
        });

        WorldCloseReason reason = default;
        await WaitUntilAsync(() => world.TryObserveClose(out reason));
        Assert.Equal(WorldCloseReason.ConnectionLost, reason);
    }

    [Fact]
    public async Task MissingExperienceTable_ClosesWithContentFailure()
    {
        Harness harness = new(endOfStream: false);
        await using ClientWorld world = harness.Create(new CatalogContentSource(hasExperience: false));
        await WaitUntilAsync(() =>
        {
            world.CompleteLoad();
            return !world.IsActive;
        });
        Assert.False(world.HasScene);
        WorldCloseReason reason = default;
        await WaitUntilAsync(() => world.TryObserveClose(out reason));
        Assert.Equal(WorldCloseReason.ContentUnavailable, reason);
        Assert.Equal(1, harness.SessionCloseCalls);
    }

    [Fact]
    public async Task Shutdown_ClosesSocketBeforeWaitingForCatalogReadToReturn()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TaskCompletionSource reading = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        CatalogContentSource content = new(() =>
        {
            reading.SetResult();
            release.Task.WaitAsync(s_timeout, token).GetAwaiter().GetResult();
        });
        Harness harness = new(endOfStream: false);
        await using ClientWorld world = harness.Create(content);
        try
        {
            await reading.Task.WaitAsync(s_timeout, token);
            Task shutdown = world.DisposeAsync().AsTask();
            await harness.SessionClosed.Task.WaitAsync(s_timeout, token);
            Assert.False(shutdown.IsCompleted);
            release.SetResult();
            await shutdown.WaitAsync(s_timeout, token);
            Assert.True(harness.IngressStopped);
            Assert.Equal(1, harness.SessionCloseCalls);
        }
        finally
        {
            release.TrySetResult();
        }
    }

    private static GameWorldEntrySnapshot Entry() => new(
        new GameCharacterInfo1006 { EntityId = 0x11223344, Level = 1, CurrentProfession = 10 },
        1002, 1010, 0, 1, 61, 101, [], 3);

    private static byte[] Correction(ushort x, ushort y)
    {
        byte[] frame = new byte[GameWorldAction10010.PacketLength];
        GameWorldAction10010.Write(frame, GameWorldAction10010.PositionCorrection, 0x11223344);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(24), x);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(26), y);
        return frame;
    }

    private static async Task<GameSecuredInboundFrame?> WaitForDisconnectAsync(CancellationToken token)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, token);
        return null;
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

    private sealed class Harness(bool endOfStream, bool failLoad = false, bool failSession = false, Exception? ingressFailure = null)
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

        public WorldContentLoad<WorldMapScene>? Load
        {
            get; private set;
        }

        public ClientWorld Create(IClientContentSource? content = null, ClientDiagnostics? diagnostics = null, Action? onClosing = null) =>
            new(Entry(), content ?? new CatalogContentSource(), new LogicalRenderSize(1024, 768),
                () =>
                {
                    ClosingCalls++;
                    onClosing?.Invoke();
                }, CloseSessionAsync, CreateIngress, CreateLoad, diagnostics: diagnostics);

        private ValueTask CloseSessionAsync()
        {
            SessionCloseCalls++;
            SessionClosed.TrySetResult();
            return failSession ? ValueTask.FromException(new IOException("Session cleanup failed.")) : ValueTask.CompletedTask;
        }

        private WorldFrameIngress CreateIngress()
        {
            if (ingressFailure is not null)
            {
                return new WorldFrameIngress(_ => Task.FromException<GameSecuredInboundFrame?>(ingressFailure));
            }

            return endOfStream
                ? new WorldFrameIngress(_ => Task.FromResult<GameSecuredInboundFrame?>(null))
                : new WorldFrameIngress(WaitForeverAsync);
        }

        private WorldContentLoad<WorldMapScene> CreateLoad()
        {
            Load = new WorldContentLoad<WorldMapScene>(LoadUntilCanceled);
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

    }

    /// <summary>Serves the required world entry catalogs without reading the host filesystem.</summary>
    private sealed class CatalogContentSource(Action? beforeItemTable = null, bool hasExperience = true) : IClientContentSource
    {
        public bool TryOpenRead(string contentPath, ContentLookupMode mode, [NotNullWhen(true)] out Stream? stream)
        {
            if (contentPath == ItemStatCatalog.ItemTypePath)
            {
                beforeItemTable?.Invoke();
            }

            stream = contentPath switch
            {
                ItemStatCatalog.ItemTypePath => new MemoryStream(WorldHeroItemStatsTests.ItemTypeBytes()),
                ItemStatCatalog.ItemAddPath => new MemoryStream(WorldHeroItemStatsTests.ItemAddBytes()),
                LevelExperienceTable.RelativePath when hasExperience => new MemoryStream(Convert.FromHexString("d4951d370363a6ace8b8297e63")),
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
