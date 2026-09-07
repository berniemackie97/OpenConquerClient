using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace OpenConquer.Launcher.Instances;

internal sealed record LauncherInstanceIdentity(string LeaseName, string PipeName)
{
    public static LauncherInstanceIdentity ForCurrentUser()
    {
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            return new LauncherInstanceIdentity("OpenConquer.Launcher", UnixActivationNamespace.ForCurrentUser());
        }
        string user;
        if (OperatingSystem.IsWindows())
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            user = identity.User?.Value ?? throw new InvalidOperationException("The current Windows user has no security identifier.");
        }
        else
        {
            throw new PlatformNotSupportedException("Unsupported launcher desktop platform.");
        }
        string suffix = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(user)));
        string pipeName = "OpenConquer.Launcher." + suffix;

        return new LauncherInstanceIdentity("OpenConquer.Launcher", pipeName);
    }
}
