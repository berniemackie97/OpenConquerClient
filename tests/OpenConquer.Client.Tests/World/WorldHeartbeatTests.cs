using System.Net;
using System.Net.Sockets;
using OpenConquer.Client.Startup.LoginFlow;
using OpenConquer.Client.World;

namespace OpenConquer.Client.Tests.World;

// CTimerMS_ToNextTime 0x6B9D8B / CTimerMS_IsTimeOut 0x6B9C93 and CHero_TickHeartbeatAndPeriodicSessionSync 0x661ADD.
public sealed class WorldHeartbeatTests
{
    [Fact]
    public void Tick_FiresAtTenSecondsAndRestartsFromTheCheckTime()
    {
        WorldHeartbeat heartbeat = new(tickStartedAt: 1_000, clientStartedAt: 1_000, localAddress: 0);

        Assert.Equal(WorldHeartbeatSend.None, heartbeat.Tick(10_999));
        Assert.Equal(WorldHeartbeatSend.LatencyTick, heartbeat.Tick(11_500));
        Assert.Equal(WorldHeartbeatSend.None, heartbeat.Tick(21_499));
        Assert.Equal(WorldHeartbeatSend.LatencyTick, heartbeat.Tick(21_500));
    }

    [Fact]
    public void Tick_IsInactiveWhileTheStartIsZero() =>
        Assert.Equal(WorldHeartbeatSend.None, new WorldHeartbeat(0, 0, 0).Tick(uint.MaxValue));

    [Fact]
    public void Tick_ComparesAgainstTheWrappedDeadlineAsNativeDoes()
    {
        // start + interval wraps to 0x270F; native's unsigned compare fires as soon as now reaches it.
        WorldHeartbeat heartbeat = new(tickStartedAt: uint.MaxValue, clientStartedAt: uint.MaxValue, localAddress: 0);

        Assert.Equal(WorldHeartbeatSend.LatencyTick, heartbeat.Tick(uint.MaxValue));
    }

    [Fact]
    public void Tick_AddsTheReportEveryThirtyMinutesFromClientStart()
    {
        WorldHeartbeat heartbeat = new(tickStartedAt: 1_000, clientStartedAt: 500, localAddress: 0x0100007F);

        Assert.Equal(WorldHeartbeatSend.LatencyTick, heartbeat.Tick(1_799_999));
        Assert.Equal(WorldHeartbeatSend.LatencyTickAndReport, heartbeat.Tick(1_810_500));
        Assert.Equal(WorldHeartbeatSend.LatencyTick, heartbeat.Tick(1_820_500));
    }

    [Fact]
    public void Tick_RestartsTheReportWindowEvenWithoutAnAddress()
    {
        WorldHeartbeat heartbeat = new(tickStartedAt: 1_000, clientStartedAt: 0, localAddress: 0);

        Assert.Equal(WorldHeartbeatSend.LatencyTick, heartbeat.Tick(1_800_000));
        Assert.Equal(WorldHeartbeatSend.LatencyTick, heartbeat.Tick(1_810_000));
    }

    [Fact]
    public void ObserveLatencyTick_StoresNowMinusTheServerReference()
    {
        WorldHeartbeat heartbeat = new(1, 1, 0);
        heartbeat.ObserveLatencyTick(now: 5, serverReference: 10);

        Assert.Equal(unchecked(5u - 10u), heartbeat.Latency);
    }

    [Fact]
    public async Task Egress_SendsOneWriteAtATimeAndRethrowsAFailure()
    {
        List<string> sent = [];
        // Inline continuations keep the write chain synchronous for the assertions.
        TaskCompletionSource firstWrite = new();
        int writes = 0;
        await using WorldFrameEgress egress = new(
            (now, _) =>
            {
                sent.Add($"tick {now}");
                return Interlocked.Increment(ref writes) == 1 ? firstWrite.Task : Task.FromException(new IOException("lost"));
            },
            (address, latency, now, _) =>
            {
                sent.Add($"report {address:X8} {latency} {now}");
                return Task.CompletedTask;
            });
        WorldHeartbeat heartbeat = new(tickStartedAt: 1, clientStartedAt: 0, localAddress: 0x0100007F);

        egress.Pump(heartbeat, 1_800_001);
        egress.Pump(heartbeat, 1_900_000);
        Assert.Equal(["tick 1800001"], sent);

        firstWrite.SetResult();
        egress.Pump(heartbeat, 1_900_000);
        Assert.Equal(["tick 1800001", "report 0100007F 0 1800001", "tick 1900000"], sent);

        Assert.Throws<IOException>(() => egress.Pump(heartbeat, 2_000_000));
    }

    [Fact]
    public async Task LocalAddress_KeepsTheLastIpv4AsTheRawInAddrAndSurvivesALookupFailure()
    {
        bool fail = false;
        LocalAddressResolver resolver = new(_ => fail
            ? Task.FromException<IPAddress[]>(new SocketException())
            : Task.FromResult(new[] { IPAddress.Parse("192.168.1.5"), IPAddress.IPv6Loopback, IPAddress.Parse("10.211.55.3") }));

        Assert.Equal(0x0337D30Au, await resolver.RefreshAsync(TestContext.Current.CancellationToken));
        fail = true;
        Assert.Equal(0x0337D30Au, await resolver.RefreshAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void SilentInfoFiles_ReadTheNativeFilesOrTheDefaults()
    {
        string root = Directory.CreateTempSubdirectory().FullName;
        try
        {
            Assert.Equal(new OpenConquer.Networking.Game.Login.GameSilentInfoReport(0, 1), SilentInfoFiles.Read(root));

            Directory.CreateDirectory(Path.Combine(root, "ini"));
            File.WriteAllBytes(Path.Combine(root, "ini", "Slient.dat"), "OpenConquer"u8.ToArray());
            File.WriteAllText(Path.Combine(root, "ini", "SlientVersion.ini"), " 12\r\n");

            Assert.Equal(new OpenConquer.Networking.Game.Login.GameSilentInfoReport(0xB29F7EC2, 12), SilentInfoFiles.Read(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
