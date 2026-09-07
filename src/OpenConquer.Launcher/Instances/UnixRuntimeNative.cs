using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace OpenConquer.Launcher.Instances;

// Public OS ABIs only. Linux statx has a fixed layout across architectures; Darwin uses stat64.
internal static partial class UnixRuntimeNative
{
    internal readonly record struct Metadata(uint UserId, uint Mode);

    internal static uint UserId => GetEffectiveUserId();

    // O_DIRECTORY | O_NOFOLLOW | O_CLOEXEC. Linux arm64 differs from Linux x64.
    private static int DirectoryFlags => OperatingSystem.IsMacOS()
        ? 0x00100000 | 0x00000100 | 0x01000000
        : RuntimeInformation.ProcessArchitecture == Architecture.Arm64
            ? 0x00004000 | 0x00008000 | 0x00080000
            : 0x00010000 | 0x00020000 | 0x00080000;

    internal static SafeFileHandle OpenRoot() => OwnDirectory(Open("/", DirectoryFlags));

    internal static SafeFileHandle OpenDirectory(SafeFileHandle parent, string name) =>
        OwnDirectory(OpenAt(parent, name, DirectoryFlags));

    private static SafeFileHandle OwnDirectory(int descriptor)
    {
        if (descriptor < 0)
            ThrowLastError();
        return new SafeFileHandle(descriptor, ownsHandle: true);
    }

    internal static void CreateDirectory(SafeFileHandle parent, string name)
    {
        // mkdirat applies 0700 atomically. Never chmod or repair a pre-existing directory.
        if (MkdirAt(parent, name, 0x1c0) != 0 && Marshal.GetLastPInvokeError() != 17)
            ThrowLastError();
    }

    internal static Metadata ReadDirectory(SafeFileHandle descriptor)
    {
        if (OperatingSystem.IsLinux())
        {
            if (Statx(descriptor, "", 0x1000, 0xb, out LinuxStat status) != 0)
                ThrowLastError();
            return FromLinux(status);
        }

        int result = RuntimeInformation.ProcessArchitecture == Architecture.X64
            ? DarwinFStat64(descriptor, out DarwinStat darwin)
            : DarwinFStat(descriptor, out darwin);
        if (result != 0)
            ThrowLastError();
        RejectDarwinAccessGrants(descriptor);
        return new(darwin.UserId, darwin.Mode);
    }

    internal static Metadata? ReadEntry(SafeFileHandle parent, string name)
    {
        if (OperatingSystem.IsLinux())
        {
            if (Statx(parent, name, 0x100, 0xb, out LinuxStat status) == 0)
                return FromLinux(status);
        }
        else
        {
            int result = RuntimeInformation.ProcessArchitecture == Architecture.X64
                ? DarwinFStatAt64(parent, name, out DarwinStat darwin, 0x20)
                : DarwinFStatAt(parent, name, out darwin, 0x20);
            if (result == 0)
                return new(darwin.UserId, darwin.Mode);
        }

        if (Marshal.GetLastPInvokeError() == 2)
            return null;
        ThrowLastError();
        return null;
    }

    private static Metadata FromLinux(LinuxStat status)
    {
        if ((status.Mask & 0xb) != 0xb)
            throw new IOException("Runtime filesystem did not provide ownership and mode metadata.");
        return new(status.UserId, status.Mode);
    }

    internal static string DarwinTemporaryDirectory()
    {
        // confstr selects/creates the OS-managed per-user directory without trusting TMPDIR.
        byte[] buffer = new byte[1024];
        nuint length = Confstr(65537, buffer, (nuint)buffer.Length); // _CS_DARWIN_USER_TEMP_DIR
        if (length == 0 || length > (nuint)buffer.Length)
            throw new IOException("The OS did not provide a bounded user runtime directory.");
        string path = Encoding.UTF8.GetString(buffer, 0, checked((int)length - 1));
        // Apple's system /var alias is canonicalized explicitly; user-supplied links are rejected.
        return path.StartsWith("/var/", StringComparison.Ordinal) ? "/private" + path : path;
    }

    private static void RejectDarwinAccessGrants(SafeFileHandle descriptor)
    {
        nint acl = AclGetFd(descriptor, 0x100); // ACL_TYPE_EXTENDED
        // For an already-open descriptor, Darwin FILESEC_ACL reports ENOENT when no ACL is set.
        if (acl == 0 && Marshal.GetLastPInvokeError() == 2)
            return;
        if (acl == 0)
            ThrowLastError();
        try
        {
            // Darwin ACL grants can bypass mode bits. Deny entries (e.g. home deny-delete) are safe.
            int position = 0; // ACL_FIRST_ENTRY
            while (AclGetEntry(acl, position, out nint entry) == 0)
            {
                if (AclGetTag(entry, out int tag) != 0)
                    ThrowLastError();
                if (tag != 2)
                    throw new UnauthorizedAccessException("Runtime directory ancestry contains an extended access grant.");
                position = -1; // ACL_NEXT_ENTRY
            }
            if (Marshal.GetLastPInvokeError() != 22)
                ThrowLastError(); // Darwin signals end with EINVAL.
        }
        finally { _ = AclFree(acl); }
    }

    private static void ThrowLastError()
    {
        int error = Marshal.GetLastPInvokeError();
        if (error == 2)
            throw new DirectoryNotFoundException("The runtime directory is unavailable.");
        throw new IOException("Cannot securely access the runtime namespace.", new Win32Exception(error));
    }

    [StructLayout(LayoutKind.Explicit, Size = 256)]
    private struct LinuxStat
    {
        [FieldOffset(0)] public uint Mask;
        [FieldOffset(20)] public uint UserId;
        [FieldOffset(28)] public ushort Mode;
    }

    [StructLayout(LayoutKind.Explicit, Size = 144)]
    private struct DarwinStat
    {
        [FieldOffset(4)] public ushort Mode;
        [FieldOffset(16)] public uint UserId;
    }

    [LibraryImport("libc", EntryPoint = "geteuid")]
    private static partial uint GetEffectiveUserId();
    [LibraryImport("libc", EntryPoint = "open", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int Open(string path, int flags);
    [LibraryImport("libc", EntryPoint = "openat", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int OpenAt(SafeFileHandle parent, string name, int flags);
    [LibraryImport("libc", EntryPoint = "mkdirat", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int MkdirAt(SafeFileHandle parent, string name, uint mode);
    [LibraryImport("libc", EntryPoint = "statx", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int Statx(SafeFileHandle parent, string name, int flags, uint mask, out LinuxStat status);
    [LibraryImport("libc", EntryPoint = "fstat", SetLastError = true)]
    private static partial int DarwinFStat(SafeFileHandle descriptor, out DarwinStat status);
    [LibraryImport("libc", EntryPoint = "fstat$INODE64", SetLastError = true)]
    private static partial int DarwinFStat64(SafeFileHandle descriptor, out DarwinStat status);
    [LibraryImport("libc", EntryPoint = "fstatat", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int DarwinFStatAt(SafeFileHandle parent, string name, out DarwinStat status, int flags);
    [LibraryImport("libc", EntryPoint = "fstatat$INODE64", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int DarwinFStatAt64(SafeFileHandle parent, string name, out DarwinStat status, int flags);
    [LibraryImport("libc", EntryPoint = "confstr", SetLastError = true)]
    private static partial nuint Confstr(int name, [Out] byte[] buffer, nuint length);
    [LibraryImport("libc", EntryPoint = "acl_get_fd_np", SetLastError = true)]
    private static partial nint AclGetFd(SafeFileHandle descriptor, int type);
    [LibraryImport("libc", EntryPoint = "acl_get_entry", SetLastError = true)]
    private static partial int AclGetEntry(nint acl, int position, out nint entry);
    [LibraryImport("libc", EntryPoint = "acl_get_tag_type", SetLastError = true)]
    private static partial int AclGetTag(nint entry, out int tag);
    [LibraryImport("libc", EntryPoint = "acl_free")]
    private static partial int AclFree(nint acl);
}
