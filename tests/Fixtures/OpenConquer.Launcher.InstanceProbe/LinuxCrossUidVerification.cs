using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using OpenConquer.Launcher.Instances;

namespace OpenConquer.Launcher.InstanceProbe;

/// <summary>Opt-in integration verification for an isolated Linux runner; never a product entry point.</summary>
[SupportedOSPlatform("linux")]
internal static class LinuxCrossUidVerification
{
    // Existing unprivileged Linux identities; no account or machine configuration is changed.
    private const uint OwnerUid = 65534;
    private const uint AttackerUid = 1;

    internal static async Task RunAsync()
    {
        if (UnixRuntimeNative.UserId != 0)
            throw new InvalidOperationException("Cross-UID verification requires root in an isolated Linux runner.");

        string root = Directory.CreateTempSubdirectory("oc-uid-").FullName;
        string? ownedLegacyPath = null;
        try
        {
            File.SetUnixFileMode(root, (UnixFileMode)0x1ed);
            string ownerRoot = await CreateRuntimeAsync(root, "owner", OwnerUid);
            string attackerRoot = await CreateRuntimeAsync(root, "attacker", AttackerUid);
            string lease = "OpenConquer.CrossUid." + Guid.NewGuid().ToString("N");
            string pipe = Path.Combine(ownerRoot, "openconquer", "activate");

            // Obtain the historical path from the real owner identity, without creating a namespace.
            string legacyPath;
            using (Child resolve = StartActor(OwnerUid, ownerRoot, "legacy-path"))
            {
                legacyPath = await resolve.ReadLineAsync();
                await resolve.ExpectExitAsync(0);
            }
            using Child obsolete = StartActor(AttackerUid, attackerRoot, "occupy", legacyPath);
            await obsolete.ExpectLineAsync("ready");
            ownedLegacyPath = legacyPath;

            await AttackAsync(attackerRoot, pipe, "before namespace creation");
            Require(!Directory.Exists(Path.GetDirectoryName(pipe)), "Attacker created the victim namespace.");
            await RejectForeignRootAsync(attackerRoot, lease);

            using (Child owner = StartProbe(OwnerUid, ownerRoot, "listen", lease))
            using (Child attackerOwner = StartProbe(AttackerUid, attackerRoot, "listen", lease))
            {
                await owner.ExpectLineAsync("ready");
                await attackerOwner.ExpectLineAsync("ready");
                Require(File.GetUnixFileMode(Path.GetDirectoryName(pipe)!) == (UnixFileMode)0x1c0,
                    "Application namespace is not private.");
                Require(File.GetUnixFileMode(pipe) == (UnixFileMode)0x180, "Activation socket is not private.");
                await AttackAsync(attackerRoot, pipe, "while owner is listening");
                await ActivateAsync(owner, OwnerUid, ownerRoot, lease);
                await ActivateAsync(attackerOwner, AttackerUid, attackerRoot, lease);
                Require(File.Exists(legacyPath) && !obsolete.HasExited, "Obsolete endpoint was disturbed.");
                await attackerOwner.ReleaseAsync();
                await owner.KillAsync();
            }

            Require(File.Exists(pipe), "Forced termination did not leave a stale socket.");
            await AttackAsync(attackerRoot, pipe, "after forced owner termination");
            Require(File.Exists(pipe), "Attacker removed the stale endpoint.");
            using (Child replacement = StartProbe(OwnerUid, ownerRoot, "listen", lease))
            {
                await replacement.ExpectLineAsync("ready");
                await ActivateAsync(replacement, OwnerUid, ownerRoot, lease);
                await replacement.ReleaseAsync();
            }
            Require(!File.Exists(pipe), "Normal shutdown did not remove the endpoint.");
            Require(File.Exists(legacyPath) && !obsolete.HasExited, "Recovery disturbed the obsolete endpoint.");
            await obsolete.ReleaseAsync();
            Require(!File.Exists(legacyPath), "Attacker did not clean up its obsolete endpoint.");
            Console.WriteLine("PASS: cross-UID isolation, obsolete endpoint independence, activation and stale-socket recovery.");
        }
        finally
        {
            try
            {
                // Children have exited before cleanup, including failures. Only remove the legacy
                // socket after a successful bind acknowledgement; preserve pre-existing objects.
                if (ownedLegacyPath is not null)
                    File.Delete(ownedLegacyPath);
            }
            finally { Directory.Delete(root, recursive: true); }
        }
    }

    private static async Task<string> CreateRuntimeAsync(string root, string name, uint userId)
    {
        string path = Path.Combine(root, name);
        Directory.CreateDirectory(path, (UnixFileMode)0x1c0);
        string identity = userId.ToString(CultureInfo.InvariantCulture);
        using Child chown = Child.Start("/usr/bin/chown", [$"{identity}:{identity}", "--", path]);
        await chown.ExpectExitAsync(0);
        return path;
    }

    private static async Task RejectForeignRootAsync(string attackerRoot, string lease)
    {
        using Child invalid = StartProbe(OwnerUid, attackerRoot, "resolve", lease);
        await invalid.ExpectExitAsync(65);
        Require(!Directory.Exists(Path.Combine(attackerRoot, "openconquer")), "Invalid foreign root was modified.");
        Require(File.GetUnixFileMode(attackerRoot) == (UnixFileMode)0x1c0, "Invalid foreign root was chmod-repaired.");
        Console.WriteLine("PASS: foreign-owned runtime root rejected without modification.");
    }

    private static async Task AttackAsync(string attackerRoot, string pipe, string phase)
    {
        using Child attacker = StartActor(AttackerUid, attackerRoot, "attack", pipe);
        await attacker.ExpectLineAsync("denied");
        await attacker.ExpectExitAsync(0);
        Console.WriteLine($"PASS: namespace and endpoint attacks denied {phase}.");
    }

    private static async Task ActivateAsync(Child owner, uint userId, string runtimeRoot, string lease)
    {
        using Child secondary = StartProbe(userId, runtimeRoot, "activate", lease);
        await secondary.ExpectLineAsync(nameof(LauncherInstanceAdmission.ActivatedExisting));
        await secondary.ExpectExitAsync(0);
        await owner.ExpectLineAsync("activated");
        Require(!owner.HasExited, "Activation displaced the owner.");
    }

    private static Child StartProbe(uint userId, string runtimeRoot, string mode, string lease) =>
        StartActor(userId, runtimeRoot, "probe", mode, lease, "@current-user");

    private static Child StartActor(uint userId, string runtimeRoot, params string[] arguments)
    {
        string identity = userId.ToString(CultureInfo.InvariantCulture);
        string host = Environment.ProcessPath ?? throw new InvalidOperationException("Missing .NET host path.");
        string assembly = typeof(Program).Assembly.Location;
        string[] managedArguments = Path.GetFileNameWithoutExtension(host) == "dotnet" ? [assembly] : [];
        return Child.Start("/usr/bin/setpriv",
            ["--reuid", identity, "--regid", identity, "--clear-groups", "--bounding-set=-all",
                "--inh-caps=-all", "--ambient-caps=-all", "--no-new-privs", "--",
                host, .. managedArguments, "--cross-uid-actor", identity, .. arguments], runtimeRoot);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class Child : IDisposable
    {
        private readonly Process _process;
        private readonly CancellationTokenSource _deadline = new(TimeSpan.FromSeconds(60));

        private Child(Process process) => _process = process;

        internal bool HasExited => _process.HasExited;

        internal static Child Start(string executable, string[] arguments, string? runtimeRoot = null)
        {
            ProcessStartInfo start = new(executable)
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                // Stderr is inherited: failures remain visible and cannot fill an unread pipe.
                WorkingDirectory = "/",
            };
            start.Environment.Clear();
            start.Environment["PATH"] = "/usr/bin:/bin";
            start.Environment["DOTNET_EnableDiagnostics"] = "0";
            if (runtimeRoot is not null)
            {
                start.Environment["HOME"] = runtimeRoot;
                start.Environment["XDG_RUNTIME_DIR"] = runtimeRoot;
            }
            foreach (string argument in arguments)
                start.ArgumentList.Add(argument);
            return new Child(Process.Start(start) ?? throw new InvalidOperationException("Verification process did not start."));
        }

        internal async Task<string> ReadLineAsync() =>
            await _process.StandardOutput.ReadLineAsync(_deadline.Token)
            ?? throw new InvalidOperationException("Verification process exited before reporting its state.");

        internal async Task ExpectLineAsync(string expected)
        {
            string actual = await ReadLineAsync();
            Require(actual == expected, $"Expected '{expected}', received '{actual}'.");
        }

        internal async Task ExpectExitAsync(int expected)
        {
            await _process.WaitForExitAsync(_deadline.Token);
            Require(_process.ExitCode == expected, $"Expected exit {expected}, received {_process.ExitCode}.");
        }

        internal async Task ReleaseAsync()
        {
            await _process.StandardInput.WriteLineAsync("exit".AsMemory(), _deadline.Token);
            await _process.StandardInput.FlushAsync(_deadline.Token);
            await ExpectExitAsync(0);
        }

        internal async Task KillAsync()
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync(_deadline.Token);
            Require(_process.ExitCode != 0, "Forced termination reported successful exit.");
        }

        public void Dispose()
        {
            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                    if (!_process.WaitForExit(5000))
                        throw new TimeoutException("Verification process did not terminate during cleanup.");
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
