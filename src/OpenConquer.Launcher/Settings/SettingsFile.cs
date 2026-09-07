using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace OpenConquer.Launcher.Settings;

internal static partial class SettingsFile
{
    internal static FileStream OpenRead(string path)
    {
        FileAttributes attributes = File.GetAttributes(path);
        if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory | FileAttributes.Device)) != 0)
        {
            throw new IOException("Unsupported settings file.");
        }

        if (OperatingSystem.IsWindows())
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        }

        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS() || RuntimeInformation.ProcessArchitecture is not (Architecture.X64 or Architecture.Arm64))
        {
            throw new PlatformNotSupportedException();
        }

        int flags = OperatingSystem.IsMacOS() ? 0x4 | 0x100 | 0x1000000 : 0x800 | 0x80000 | 0x20000;
        int descriptor = Open(path, flags);
        if (descriptor < 0)
        {
            int error = Marshal.GetLastPInvokeError();
            if (error == 2)
            {
                throw new FileNotFoundException();
            }

            if (error == 13)
            {
                throw new UnauthorizedAccessException("Cannot read settings.");
            }

            throw new IOException("Cannot open settings.", new Win32Exception(error));
        }
        SafeFileHandle handle = new(descriptor, ownsHandle: true);
        try
        {
            return new FileStream(handle, FileAccess.Read);
        }
        catch { handle.Dispose(); throw; }
    }

    [LibraryImport("libc", EntryPoint = "open", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int Open(string path, int flags);
}
