using OpenConquer.Client.World;
using OpenConquer.Networking.Game.Login;

namespace OpenConquer.Client.Tests.World;

public sealed class WorldFrameEgressTests
{
    [Fact]
    public async Task ReplayedActionsAndCorrectionsPreserveOneSendOrder()
    {
        List<string> writes = [];
        TaskCompletionSource first = new(TaskCreationOptions.RunContinuationsAsynchronously);
        WorldFrameEgress egress = new(
            (_, _) => Task.CompletedTask,
            (_, _, _, _) => Task.CompletedTask,
            (correction, now, _) =>
            {
                writes.Add($"correction {correction.X} {now}");
                return Task.CompletedTask;
            },
            (reply, now, _) =>
            {
                writes.Add($"action {reply.Action} {now}");
                return reply.Action == GameWorldAction10010.GetFriends ? first.Task : Task.CompletedTask;
            });
        await using (egress)
        {
            egress.QueueWorldActionResponse(new GameWorldActionResponse(GameWorldAction10010.GetFriends, 1, 0));
            egress.QueuePositionCorrection(new WorldCorrectionReply(61, 109, 0));
            egress.QueueWorldActionResponse(new GameWorldActionResponse(GameWorldAction10010.SetPkMode, 1, 3));
            WorldHeartbeat heartbeat = new(0, 0, 0);

            try
            {
                egress.Pump(heartbeat, 100);
                egress.Pump(heartbeat, 101);
                Assert.Equal(["action 76 100"], writes);
            }
            finally
            {
                first.TrySetResult();
            }

            egress.Pump(heartbeat, 102);
            egress.Pump(heartbeat, 103);
            Assert.Equal(["action 76 100", "correction 61 102", "action 96 103"], writes);
        }
    }

    [Fact]
    public async Task WorldRepliesShareTheSameBoundedCapacity()
    {
        await using WorldFrameEgress egress = new(
            (_, _) => Task.CompletedTask,
            (_, _, _, _) => Task.CompletedTask,
            (_, _, _) => Task.CompletedTask,
            (_, _, _) => Task.CompletedTask);
        for (int index = 0; index < WorldFrameEgress.MaximumQueuedResponses - 1; index++)
        {
            egress.QueuePositionCorrection(new WorldCorrectionReply((ushort)index, 109, 0));
        }

        egress.QueueWorldActionResponse(new GameWorldActionResponse(GameWorldAction10010.GetFriends, 1, 0));
        Assert.Throws<WorldResponseQueueFullException>(() =>
            egress.QueueWorldActionResponse(new GameWorldActionResponse(GameWorldAction10010.SetPkMode, 1, 3)));
    }

    [Fact]
    public async Task ReplayWriteFailureIsPropagatedByTheOwnerPump()
    {
        IOException failure = new("connection lost");
        await using WorldFrameEgress egress = new(
            (_, _) => Task.CompletedTask,
            (_, _, _, _) => Task.CompletedTask,
            (_, _, _) => Task.CompletedTask,
            (_, _, _) => Task.FromException(failure));
        egress.QueueWorldActionResponse(new GameWorldActionResponse(GameWorldAction10010.GetFriends, 1, 0));
        egress.Pump(new WorldHeartbeat(0, 0, 0), 100);

        Assert.Same(failure, Assert.Throws<IOException>(() =>
            egress.Pump(new WorldHeartbeat(0, 0, 0), 101)));
    }

    [Fact]
    public async Task CorrectionsRetainReplyOrderWhileWriteIsActive()
    {
        List<string> writes = [];
        TaskCompletionSource firstCorrection = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int corrections = 0;
        await using WorldFrameEgress egress = new(
            (now, _) =>
            {
                writes.Add($"heartbeat {now}");
                return Task.CompletedTask;
            },
            (_, _, _, _) => Task.CompletedTask,
            (reply, now, _) =>
            {
                writes.Add($"correction {reply.X} {reply.Y} {reply.DirectionIndex} {now}");
                return ++corrections == 1 ? firstCorrection.Task : Task.CompletedTask;
            });
        WorldHeartbeat heartbeat = new(tickStartedAt: 0, clientStartedAt: 0, localAddress: 0);

        egress.QueuePositionCorrection(new WorldCorrectionReply(11, 22, 3));
        egress.QueuePositionCorrection(new WorldCorrectionReply(11, 22, 3));
        egress.QueuePositionCorrection(new WorldCorrectionReply(12, 23, 5));
        try
        {
            egress.Pump(heartbeat, 10_001);
            egress.Pump(heartbeat, 10_002);
            Assert.Equal(["correction 11 22 3 10001"], writes);
        }
        finally
        {
            firstCorrection.TrySetResult();
        }

        egress.Pump(heartbeat, 10_003);
        egress.Pump(heartbeat, 10_004);
        egress.Pump(heartbeat, 10_005);
        Assert.Equal(
        [
            "correction 11 22 3 10001",
            "correction 11 22 3 10003",
            "correction 12 23 5 10004",
        ], writes);
    }

    [Fact]
    public async Task HeartbeatDueDuringContinuousCorrectionsGetsTheOutboundLane()
    {
        List<string> writes = [];
        await using WorldFrameEgress egress = new(
            (now, _) =>
            {
                writes.Add($"heartbeat {now}");
                return Task.CompletedTask;
            },
            (_, _, _, _) => Task.CompletedTask,
            (reply, _, _) =>
            {
                writes.Add($"correction {reply.X}");
                return Task.CompletedTask;
            });
        for (ushort x = 0; x < 5; x++)
        {
            egress.QueuePositionCorrection(new WorldCorrectionReply(x, 1, 0));
        }

        WorldHeartbeat heartbeat = new(tickStartedAt: 1, clientStartedAt: 1, localAddress: 0);
        egress.Pump(heartbeat, 10_001);
        egress.Pump(heartbeat, 10_002);
        egress.Pump(heartbeat, 10_003);
        egress.Pump(heartbeat, 10_004);

        Assert.Equal(["correction 0", "heartbeat 10002", "correction 1", "correction 2"], writes);
    }

    [Fact]
    public async Task CorrectionBacklogIsBoundedWithoutDroppingAcceptedReplies()
    {
        List<ushort> delivered = [];
        await using WorldFrameEgress egress = new(
            (_, _) => Task.CompletedTask,
            (_, _, _, _) => Task.CompletedTask,
            (reply, _, _) =>
            {
                delivered.Add(reply.X);
                return Task.CompletedTask;
            });

        for (int i = 0; i < WorldFrameEgress.MaximumQueuedResponses; i++)
        {
            egress.QueuePositionCorrection(new WorldCorrectionReply((ushort)i, 1, 0));
        }

        Assert.Throws<WorldResponseQueueFullException>(() =>
            egress.QueuePositionCorrection(new WorldCorrectionReply(5000, 1, 0)));

        WorldHeartbeat heartbeat = new(0, 0, 0);
        egress.Pump(heartbeat, 1);
        egress.QueuePositionCorrection(new WorldCorrectionReply(5000, 1, 0));
        for (int i = 1; i <= WorldFrameEgress.MaximumQueuedResponses; i++)
        {
            egress.Pump(heartbeat, (uint)(i + 1));
        }

        Assert.Equal(WorldFrameEgress.MaximumQueuedResponses + 1, delivered.Count);
        for (int i = 0; i < WorldFrameEgress.MaximumQueuedResponses; i++)
        {
            Assert.Equal((ushort)i, delivered[i]);
        }
        Assert.Equal((ushort)5000, delivered[^1]);
    }

    [Fact]
    public async Task CorrectionWriteFailureIsObservedOnNextPump()
    {
        IOException writeFailure = new("disconnected");
        await using WorldFrameEgress egress = new(
            (_, _) => Task.CompletedTask,
            (_, _, _, _) => Task.CompletedTask,
            (_, _, _) => Task.FromException(writeFailure));
        egress.QueuePositionCorrection(new WorldCorrectionReply(11, 22, 3));
        egress.Pump(new WorldHeartbeat(0, 0, 0), 5);

        IOException caught = Assert.Throws<IOException>(() =>
            egress.Pump(new WorldHeartbeat(0, 0, 0), 6));
        Assert.Same(writeFailure, caught);
    }

    [Fact]
    public async Task CloseCancelsAndDrainsActiveCorrectionWrite()
    {
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken observed = default;
        WorldFrameEgress egress = new(
            (_, _) => Task.CompletedTask,
            (_, _, _, _) => Task.CompletedTask,
            async (_, _, token) =>
            {
                observed = token;
                started.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            });
        egress.QueuePositionCorrection(new WorldCorrectionReply(1, 2, 3));
        egress.Pump(new WorldHeartbeat(0, 0, 0), 100);
        await started.Task.WaitAsync(TestContext.Current.CancellationToken);

        await egress.DisposeAsync();
        Assert.True(observed.IsCancellationRequested);
        Assert.Throws<ObjectDisposedException>(() =>
            egress.QueuePositionCorrection(new WorldCorrectionReply(4, 5, 6)));
    }
}
