using System.Buffers;
using OpenConquer.Client.World;
using OpenConquer.Networking.Game.Framing;

namespace OpenConquer.Client.Tests.World;

public sealed class WorldFrameIngressTests
{
    [Fact]
    public async Task OneReaderAppliesCompletedFramesInOrderWithoutBlockingTheFrame()
    {
        TaskCompletionSource<GameSecuredInboundFrame?> pending = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int reads = 0;
        Task<GameSecuredInboundFrame?> Receive(CancellationToken token) => ++reads switch
        {
            1 => Task.FromResult<GameSecuredInboundFrame?>(Frame(1016)),
            2 => pending.Task,
            3 => Task.FromResult<GameSecuredInboundFrame?>(Frame(10014)),
            _ => WaitForCancellation(token)
        };

        await using WorldFrameIngress ingress = new(Receive);
        List<ushort> applied = [];
        Assert.Equal(1, ingress.Drain(packet => applied.Add(
            System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(packet.Span[2..]))));
        Assert.Equal([1016], applied);
        Assert.Equal(2, reads);
        Assert.Equal(0, ingress.Drain(_ => throw new InvalidOperationException("No frame is ready.")));

        pending.SetResult(Frame(10010));
        Assert.Equal(2, ingress.Drain(packet => applied.Add(
            System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(packet.Span[2..]))));
        Assert.Equal([1016, 10010, 10014], applied);
    }

    [Fact]
    public async Task PerTickBudgetLeavesRemainingFramesForTheNextFrame()
    {
        TaskCompletionSource<GameSecuredInboundFrame?> waiting = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int issued = 0;
        Task<GameSecuredInboundFrame?> Receive(CancellationToken token)
        {
            issued++;
            return issued <= 40 ? Task.FromResult<GameSecuredInboundFrame?>(Frame(10014)) : waiting.Task;
        }

        await using WorldFrameIngress ingress = new(Receive);
        int applied = 0;
        Assert.Equal(32, ingress.Drain(_ => applied++));
        Assert.Equal(8, ingress.Drain(_ => applied++));
        Assert.Equal(40, applied);
        waiting.SetResult(null);
    }

    [Fact]
    public async Task PendingReadCancellationIsObservedDuringDisposal()
    {
        CancellationToken observed = default;
        async Task<GameSecuredInboundFrame?> Receive(CancellationToken token)
        {
            observed = token;
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return null;
        }

        WorldFrameIngress ingress = new(Receive);
        Assert.Equal(0, ingress.Drain(_ => throw new InvalidOperationException("No packet.")));
        await ingress.DisposeAsync();
        Assert.True(observed.IsCancellationRequested);
    }

    [Fact]
    public async Task CompletedEndOfStreamEndsTheWorldSession()
    {
        await using WorldFrameIngress ingress = new(
            _ => Task.FromResult<GameSecuredInboundFrame?>(null));
        Assert.Throws<EndOfStreamException>(() => ingress.Drain(_ => { }));
    }

    private static async Task<GameSecuredInboundFrame?> WaitForCancellation(CancellationToken token)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, token);
        return null;
    }

    private static GameSecuredInboundFrame Frame(ushort type)
    {
        byte[] rented = ArrayPool<byte>.Shared.Rent(8);
        rented.AsSpan(0, 8).Clear();
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(rented, 8);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(rented.AsSpan(2), type);
        return new GameSecuredInboundFrame(rented, 8);
    }
}
