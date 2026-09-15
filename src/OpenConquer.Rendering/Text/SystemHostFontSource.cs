namespace OpenConquer.Rendering.Text;

/// <summary>
/// Selects the native host-font discovery implementation for the current operating system.
/// </summary>
internal sealed class SystemHostFontSource : IHostFontSource
{
    public HostFontDiscovery Discover()
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsHostFontSource().Discover();
        }

        if (OperatingSystem.IsMacOS())
        {
            return new MacOSHostFontSource().Discover();
        }

        if (OperatingSystem.IsLinux())
        {
            return new LinuxHostFontSource().Discover();
        }

        throw new PlatformNotSupportedException(
            "Host font discovery is supported only on Windows, macOS, and Linux."
        );
    }
}
