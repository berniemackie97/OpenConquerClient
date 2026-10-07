using System.ComponentModel;
using System.Globalization;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using OpenConquer.Launcher.Instances;

namespace OpenConquer.Launcher.InstanceProbe;

[SupportedOSPlatform("linux")]
internal static partial class LinuxCrossUidActor
{
    internal static int Run(string[] args)
    {
        if (args.Length < 2)
            return 64;
        VerifyIdentity(uint.Parse(args[0], CultureInfo.InvariantCulture));
        switch (args[1])
        {
            case "legacy-path" when args.Length == 2:
                // Exact obsolete identity algorithm from f33c7d5, before the private namespace.
                Console.WriteLine("/tmp/oc-launcher-" + Convert.ToHexStringLower(
                    SHA256.HashData(Encoding.UTF8.GetBytes(Environment.UserName))));
                return 0;
            case "occupy" when args.Length == 3:
                Occupy(args[2]);
                return 0;
            case "attack" when args.Length == 3:
                Attack(args[2]);
                Console.WriteLine("denied");
                return 0;
            case "probe" when args.Length == 5:
                return Program.Main(args[2..]);
            default:
                return 64;
        }
    }

    private static void VerifyIdentity(uint userId)
    {
        if (userId == 0 || UnixRuntimeNative.UserId != userId)
            throw new InvalidOperationException("The actor must run as the requested unprivileged UID.");

        Dictionary<string, string[]> status = File.ReadLines("/proc/self/status")
            .Select(line => line.Split(':', 2))
            .ToDictionary(parts => parts[0], parts => parts[1].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        string expected = userId.ToString(CultureInfo.InvariantCulture);
        foreach (string field in new[] { "Uid", "Gid" })
        {
            if (status[field].Length != 4 || status[field].Any(value => value != expected))
                throw new InvalidOperationException("Real, effective, saved and filesystem identities must agree.");
        }
        if (status["Groups"].Length != 0 || status["NoNewPrivs"] is not ["1"])
            throw new InvalidOperationException("Actor retained supplementary groups or privilege escalation rights.");
        foreach (string field in new[] { "CapInh", "CapPrm", "CapEff", "CapBnd", "CapAmb" })
        {
            if (status[field].Length != 1 || ulong.Parse(status[field][0], NumberStyles.HexNumber, CultureInfo.InvariantCulture) != 0)
                throw new InvalidOperationException("Actor retained Linux capabilities.");
        }
    }

    private static void Occupy(string path)
    {
        using Socket socket = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        // Never remove an existing endpoint to make setup succeed.
        socket.Bind(new UnixDomainSocketEndPoint(path));
        try
        {
            socket.Listen(1);
            Console.WriteLine("ready");
            Console.ReadLine();
        }
        finally
        {
            socket.Dispose();
            File.Delete(path);
        }
    }

    private static void Attack(string pipe)
    {
        string ownRoot = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR")
            ?? throw new InvalidOperationException("Missing actor runtime directory.");
        string directory = Path.GetDirectoryName(pipe)!;
        string source = Path.Combine(ownRoot, "replacement");
        string link = Path.Combine(ownRoot, "link");
        string moved = Path.Combine(ownRoot, "moved");

        // Positive controls: these operations must work in the attacker's own namespace.
        Directory.CreateDirectory(source, (UnixFileMode)0x1c0);
        NativeSuccess(Rename(source, moved));
        NativeSuccess(RemoveDirectory(moved));
        File.WriteAllText(source, "attacker");
        NativeSuccess(Rename(source, moved));
        File.Move(moved, source);
        File.CreateSymbolicLink(link, source);
        File.Delete(link);
        using (Socket listener = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified))
        using (Socket peer = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified))
        {
            listener.Bind(new UnixDomainSocketEndPoint(link));
            listener.Listen(1);
            peer.Connect(new UnixDomainSocketEndPoint(link));
        }
        File.Delete(link);

        Denied("create namespace", () => Directory.CreateDirectory(directory, (UnixFileMode)0x1c0));
        NativeDenied("remove namespace", () => RemoveDirectory(directory));
        NativeDenied("rename namespace", () => Rename(directory, moved));
        Denied("link namespace", () => Directory.CreateSymbolicLink(directory, ownRoot));
        Denied("create endpoint", () => File.WriteAllText(pipe, "attacker"));
        Denied("unlink endpoint", () => File.Delete(pipe));
        NativeDenied("replace endpoint", () => Rename(source, pipe));
        Denied("link endpoint", () => File.CreateSymbolicLink(pipe, source));
        using (Socket socket = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified))
        {
            SocketDenied("bind endpoint", () => socket.Bind(new UnixDomainSocketEndPoint(pipe)));
            SocketDenied("connect endpoint", () => socket.Connect(new UnixDomainSocketEndPoint(pipe)));
        }
        File.Delete(source);
    }

    private static void Denied(string operation, Action action)
    {
        try
        {
            action();
        }
        catch (UnauthorizedAccessException) { return; }
        throw new InvalidOperationException($"Cross-UID operation was not denied: {operation}.");
    }

    private static void SocketDenied(string operation, Action action)
    {
        try
        {
            action();
        }
        catch (SocketException exception) when (exception.SocketErrorCode == SocketError.AccessDenied) { return; }
        throw new InvalidOperationException($"Cross-UID socket operation was not denied: {operation}.");
    }

    private static void NativeSuccess(int result)
    {
        if (result != 0)
            throw new Win32Exception(Marshal.GetLastPInvokeError());
    }

    private static void NativeDenied(string operation, Func<int> action)
    {
        int result = action();
        int error = Marshal.GetLastPInvokeError();
        // Linux EACCES. Generic IOException, ENOENT, EEXIST and other failures are not proof.
        if (result != -1 || error != 13)
            throw new InvalidOperationException($"Expected EACCES for {operation}, received result {result}, errno {error}.");
    }

    [LibraryImport("libc", EntryPoint = "rmdir", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int RemoveDirectory(string path);

    [LibraryImport("libc", EntryPoint = "rename", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int Rename(string source, string destination);
}
