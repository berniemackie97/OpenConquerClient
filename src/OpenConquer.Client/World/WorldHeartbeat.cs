namespace OpenConquer.Client.World;

/// <summary>What <see cref="WorldHeartbeat.Tick"/> asks the session to send.</summary>
internal enum WorldHeartbeatSend
{
    None,
    LatencyTick,
    LatencyTickAndReport,
}

/// <summary>
/// The native hero heartbeat (<c>CHero_TickHeartbeatAndPeriodicSessionSync</c> 0x661ADD), checked every frame. Times
/// are the client millisecond counter and wrap as native <c>timeGetTime</c> does. Evidence: 20_notes
/// Network/bootstrap-tolerance-cp24d.md, slice 2.
/// </summary>
internal sealed class WorldHeartbeat
{
    internal const uint TickInterval = 10_000;
    internal const uint ReportInterval = 1_800_000;

    private uint _tickStartedAt;
    private uint _reportedAt;

    /// <param name="tickStartedAt">When the 74 reply started the 10 s timer (0x6FF587).</param>
    /// <param name="clientStartedAt">When the hero was constructed (<c>hero+0x2F74</c>, 0x655BAD).</param>
    /// <param name="localAddress">The raw in_addr dword resolved at account connect, or 0.</param>
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

    /// <summary><c>hero+0x2CF4</c>: set from each server 1009/27 as now - its +16.</summary>
    internal uint Latency
    {
        get;
        private set;
    }

    internal void ObserveLatencyTick(uint now, uint serverReference) => Latency = unchecked(now - serverReference);

    /// <summary>
    /// <c>CTimerMS_ToNextTime</c> (0x6B9D8B): an active timer (start != 0) times out when <paramref name="now"/> reaches
    /// start + interval, compared unsigned against the wrapped sum (0x6B9CAA), and restarts from now. On a timeout
    /// the heartbeat is sent; after 30 minutes since the last report window a 1037 is added when the address is
    /// known, and the window restarts either way (0x661B4F..0x661BB7).
    /// </summary>
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
