namespace OpenConquer.Client.World;

internal enum WorldHeartbeatSend
{
    None,
    LatencyTick,
    LatencyTickAndReport,
}

internal sealed class WorldHeartbeat
{
    internal const uint TickInterval = 10_000;
    internal const uint ReportInterval = 1_800_000;

    private uint _tickStartedAt;
    private uint _reportedAt;

    internal WorldHeartbeat(uint tickStartedAt, uint clientStartedAt, uint localAddress)
    {
        _tickStartedAt = tickStartedAt;
        _reportedAt = clientStartedAt;
        LocalAddress = localAddress;
    }

    internal uint LocalAddress
    {
        get;
    }

    internal uint Latency
    {
        get;
        private set;
    }

    internal void ObserveLatencyTick(uint now, uint serverReference) => Latency = unchecked(now - serverReference);

    internal WorldHeartbeatSend Tick(uint now)
    {
        if (_tickStartedAt == 0 || now < unchecked(_tickStartedAt + TickInterval))
        {
            return WorldHeartbeatSend.None;
        }

        _tickStartedAt = now;
        if (unchecked(now - _reportedAt) < ReportInterval)
        {
            return WorldHeartbeatSend.LatencyTick;
        }

        _reportedAt = now;
        return LocalAddress == 0 ? WorldHeartbeatSend.LatencyTick : WorldHeartbeatSend.LatencyTickAndReport;
    }
}
