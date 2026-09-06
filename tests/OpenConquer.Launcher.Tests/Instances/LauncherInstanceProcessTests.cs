using System.Diagnostics;
using OpenConquer.Launcher.Instances;

namespace OpenConquer.Launcher.Tests.Instances;

public sealed class LauncherInstanceProcessTests
{
    [Fact]
    public async Task SecondProcess_ActivatesOwnerAndExitsWithoutTakingOwnership()
    {
        string lease = NewLeaseName();
        string pipe = LauncherActivationServerTests.NewPipeName();
        using Probe owner = Probe.Start("listen", lease, pipe);
        Assert.Equal("ready", await owner.ReadLineAsync());
        using Probe secondary = Probe.Start("activate", lease, pipe);

        Assert.Equal("activated", await owner.ReadLineAsync());
        Assert.Equal(nameof(LauncherInstanceAdmission.ActivatedExisting), await secondary.ReadLineAsync());
        Assert.Equal(0, await secondary.WaitForExitAsync());
        Assert.False(owner.HasExited);
        await owner.ReleaseAsync();
        Assert.Equal(0, await owner.WaitForExitAsync());
    }

    [Fact]
    public async Task ConcurrentProcesses_GrantExactlyOneLease()
    {
        string lease = NewLeaseName();
        string pipe = LauncherActivationServerTests.NewPipeName();
        using Probe first = Probe.Start("hold", lease, pipe);
        using Probe second = Probe.Start("hold", lease, pipe);
        string?[] states = await Task.WhenAll(first.ReadLineAsync(), second.ReadLineAsync());
        Assert.Single(states, value => value == "ready");
        Assert.Single(states, value => value == "busy");
        Probe owner = states[0] == "ready" ? first : second;
        Probe rejected = states[0] == "busy" ? first : second;
        Assert.Equal(3, await rejected.WaitForExitAsync());
        await owner.ReleaseAsync();
        Assert.Equal(0, await owner.WaitForExitAsync());
    }

    [Fact]
    public async Task TerminatedOwner_ReleasesLeaseAndStaleSocketIsRecoverable()
    {
        string lease = NewLeaseName();
        string pipe = LauncherActivationServerTests.NewPipeName();
        using (Probe crashed = Probe.Start("listen", lease, pipe))
        {
            Assert.Equal("ready", await crashed.ReadLineAsync());
            crashed.Kill();
            await crashed.WaitForExitAsync();
        }

        using Probe replacement = Probe.Start("listen", lease, pipe);
        Assert.Equal("ready", await replacement.ReadLineAsync());
        using Probe secondary = Probe.Start("activate", lease, pipe);
        Assert.Equal("activated", await replacement.ReadLineAsync());
        Assert.Equal(nameof(LauncherInstanceAdmission.ActivatedExisting), await secondary.ReadLineAsync());
        Assert.Equal(0, await secondary.WaitForExitAsync());
        await replacement.ReleaseAsync();
        Assert.Equal(0, await replacement.WaitForExitAsync());
    }

    [Fact]
    public async Task UnresponsiveOwner_DoesNotGrantASecondLease()
    {
        string lease = NewLeaseName();
        string pipe = LauncherActivationServerTests.NewPipeName();
        using Probe owner = Probe.Start("hold", lease, pipe);
        Assert.Equal("ready", await owner.ReadLineAsync());
        using Probe secondary = Probe.Start("activate", lease, pipe);
        Assert.Equal(nameof(LauncherInstanceAdmission.ExistingUnavailable), await secondary.ReadLineAsync());
        Assert.Equal(2, await secondary.WaitForExitAsync());
        Assert.False(owner.HasExited);
        await owner.ReleaseAsync();
        Assert.Equal(0, await owner.WaitForExitAsync());
    }

    [Fact]
    public async Task ExitingOwner_AllowsWaitingInvocationToBecomePrimary()
    {
        string lease = NewLeaseName();
        string pipe = LauncherActivationServerTests.NewPipeName();
        using Probe owner = Probe.Start("hold", lease, pipe);
        Assert.Equal("ready", await owner.ReadLineAsync());
        using Probe secondary = Probe.Start("wait", lease, pipe);
        Assert.Equal("checking", await secondary.ReadLineAsync());
        await owner.ReleaseAsync();
        Assert.Equal(0, await owner.WaitForExitAsync());
        Assert.Equal(nameof(LauncherInstanceAdmission.Primary), await secondary.ReadLineAsync());
    }

    [Fact]
    public async Task TerminatedOwner_AllowsExistingMutexHandleToAcquireAbandonedLease()
    {
        string lease = NewLeaseName();
        string pipe = LauncherActivationServerTests.NewPipeName();
        using Probe owner = Probe.Start("hold", lease, pipe);
        Assert.Equal("ready", await owner.ReadLineAsync());
        using Probe secondary = Probe.Start("wait", lease, pipe);
        Assert.Equal("checking", await secondary.ReadLineAsync());
        owner.Kill();
        await owner.WaitForExitAsync();
        Assert.Equal(nameof(LauncherInstanceAdmission.Primary), await secondary.ReadLineAsync());
        Assert.Equal(2, await secondary.WaitForExitAsync());
    }

    [Fact]
    public void Lease_RejectsUseFromAnotherThreadWithoutLosingOwnership()
    {
        using LauncherProcessLease lease = new(NewLeaseName());
        Assert.True(lease.TryAcquire());
        Exception? failure = null;
        Thread other = new(() =>
        {
            try
            {
                lease.Dispose();
            }
            catch (Exception exception) { failure = exception; }
        });
        other.Start();
        other.Join();
        Assert.IsType<InvalidOperationException>(failure);
        Assert.True(lease.TryAcquire());
    }

    private static string NewLeaseName() => "OpenConquer.Test." + Guid.NewGuid().ToString("N");

    private sealed class Probe : IDisposable
    {
        private readonly Process _process;
        private readonly CancellationTokenSource _deadline;

        private Probe(Process process)
        {
            _process = process;
            _deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            _deadline.CancelAfter(TimeSpan.FromSeconds(20));
        }

        public bool HasExited => _process.HasExited;

        public static Probe Start(string mode, string lease, string pipe)
        {
            DirectoryInfo root = new(AppContext.BaseDirectory);
            while (!File.Exists(Path.Combine(root.FullName, "OpenConquer.Client.slnx")))
            {
                root = root.Parent ?? throw new DirectoryNotFoundException("Repository root not found.");
            }

            string configuration = Directory.GetParent(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory))!.Name;
            string probe = Path.Combine(root.FullName, "tests", "Fixtures", "OpenConquer.Launcher.InstanceProbe",
                "bin", configuration, "net10.0", "OpenConquer.Launcher.InstanceProbe.dll");
            ProcessStartInfo start = new("dotnet")
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
            };
            start.ArgumentList.Add(probe);
            start.ArgumentList.Add(mode);
            start.ArgumentList.Add(lease);
            start.ArgumentList.Add(pipe);
            return new Probe(Process.Start(start) ?? throw new InvalidOperationException("Probe did not start."));
        }

        public async Task<string?> ReadLineAsync() => await _process.StandardOutput.ReadLineAsync(_deadline.Token);

        public async Task ReleaseAsync()
        {
            await _process.StandardInput.WriteLineAsync("exit".AsMemory(), _deadline.Token);
            await _process.StandardInput.FlushAsync(_deadline.Token);
        }

        public async Task<int> WaitForExitAsync()
        {
            await _process.WaitForExitAsync(_deadline.Token);
            return _process.ExitCode;
        }

        public void Kill() => _process.Kill(entireProcessTree: true);

        public void Dispose()
        {
            try
            {
                if (!_process.HasExited)
                {
                    Kill();
                    if (!_process.WaitForExit(5000))
                    {
                        throw new TimeoutException("The launcher instance probe did not terminate.");
                    }
                }
            }
            finally
            {
                _process.Dispose();
                _deadline.Dispose();
            }
        }
    }
}
