using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace OpenConquer.Launcher.Instances;

internal sealed record LauncherInstanceIdentity(string LeaseName, string PipeName)
{
    public static LauncherInstanceIdentity ForCurrentUser()
    {
        string user;
        if (OperatingSystem.IsWindows())
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            user = identity.User?.Value ?? throw new InvalidOperationException("The current Windows user has no security identifier.");
        }
        else
        {
            user = Environment.UserName;
        }
        string suffix = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(user)));
        string pipeName = "OpenConquer.Launcher." + suffix;

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            // A stable, short Unix socket path also works across Finder/shell sessions with
            // different TMPDIR values. Peer identity is enforced by the pipe, not this hash.
            pipeName = "/tmp/oc-launcher-" + suffix;
        }
        else if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Unsupported launcher desktop platform.");
        }

        return new LauncherInstanceIdentity("OpenConquer.Launcher", pipeName);
    }
}
