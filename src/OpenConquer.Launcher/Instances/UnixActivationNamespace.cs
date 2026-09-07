using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace OpenConquer.Launcher.Instances;

internal static class UnixActivationNamespace
{
    private const string ApplicationDirectory = "openconquer";
    private const string SocketName = "activate";

    internal static string ForCurrentUser()
    {
        if (OperatingSystem.IsMacOS())
            return Create(UnixRuntimeNative.DarwinTemporaryDirectory());
        if (!OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException();

        string? configured = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        if (!string.IsNullOrEmpty(configured))
            return Create(configured);

        string standard = "/run/user/" + UnixRuntimeNative.UserId.ToString(CultureInfo.InvariantCulture);
        try
        {
            return Create(standard);
        }
        catch (DirectoryNotFoundException)
        {
            // Headless/non-systemd sessions may lack /run/user. The fallback is inside the
            // validated home namespace, never a predictable child of a shared temporary root.
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Create(home, requirePrivateRoot: false);
        }
    }

    internal static string Create(string runtimeRoot, bool requirePrivateRoot = true)
    {
        ValidatePlatform();
        string root = ValidatePath(runtimeRoot);
        string applicationName = requirePrivateRoot ? ApplicationDirectory : ".openconquer-runtime";
        string pipe = root + "/" + applicationName + "/" + SocketName;
        ValidateSocketLength(pipe);
        using SafeFileHandle parent = OpenValidatedDirectory(root, requirePrivateRoot);
        UnixRuntimeNative.CreateDirectory(parent, applicationName);
        using SafeFileHandle application = UnixRuntimeNative.OpenDirectory(parent, applicationName);
        ValidateOwnerAndMode(UnixRuntimeNative.ReadDirectory(application), requirePrivate: true);
        return pipe;
    }

    internal static void ValidateEndpoint(string pipeName)
    {
        ValidatePlatform();
        string path = ValidatePath(pipeName);
        ValidateSocketLength(path);
        string parentPath = Path.GetDirectoryName(path) ?? throw new ArgumentException("Missing runtime namespace.", nameof(pipeName));
        using SafeFileHandle parent = OpenValidatedDirectory(parentPath, requirePrivate: true);
        UnixRuntimeNative.Metadata? entry = UnixRuntimeNative.ReadEntry(parent, Path.GetFileName(path));
        if (entry is { } existing && (existing.UserId != UnixRuntimeNative.UserId || (existing.Mode & 0xf000) != 0xc000))
        {
            // Only a same-user socket may be replaced by .NET's stale-endpoint recovery.
            throw new UnauthorizedAccessException("The activation endpoint is not a socket owned by the current user.");
        }
    }

    private static SafeFileHandle OpenValidatedDirectory(string path, bool requirePrivate)
    {
        SafeFileHandle current = UnixRuntimeNative.OpenRoot();
        try
        {
            ValidateAncestor(UnixRuntimeNative.ReadDirectory(current));
            foreach (string segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                SafeFileHandle next = UnixRuntimeNative.OpenDirectory(current, segment);
                current.Dispose();
                current = next;
                ValidateAncestor(UnixRuntimeNative.ReadDirectory(current));
            }
            ValidateOwnerAndMode(UnixRuntimeNative.ReadDirectory(current), requirePrivate);
            return current;
        }
        catch { current.Dispose(); throw; }
    }

    private static void ValidateAncestor(UnixRuntimeNative.Metadata status)
    {
        bool trustedOwner = status.UserId == 0 || status.UserId == UnixRuntimeNative.UserId;
        bool writableByOthers = (status.Mode & 0x12) != 0; // group/other write
        bool protectedStickyRoot = status.UserId == 0 && (status.Mode & 0x200) != 0;
        if (!trustedOwner || (status.Mode & 0xf000) != 0x4000 || (writableByOthers && !protectedStickyRoot))
            throw new UnauthorizedAccessException("Runtime directory ancestry is writable or owned by another user.");
    }

    private static void ValidateOwnerAndMode(UnixRuntimeNative.Metadata status, bool requirePrivate)
    {
        if (status.UserId != UnixRuntimeNative.UserId || (requirePrivate && (status.Mode & 0xfff) != 0x1c0))
            throw new UnauthorizedAccessException("The runtime directory must be owned by the current user with private access.");
    }

    private static string ValidatePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Path.IsPathFullyQualified(path) || path.Contains('\0') || path.Split('/').Any(part => part is "." or ".."))
            throw new ArgumentException("The runtime path must be absolute and contain no traversal or NUL characters.", nameof(path));
        return Path.TrimEndingDirectorySeparator(path);
    }

    private static void ValidateSocketLength(string pipe)
    {
        // sun_path includes its NUL terminator: Darwin 104 bytes, Linux 108 bytes.
        int maximum = OperatingSystem.IsMacOS() ? 103 : 107;
        if (Encoding.UTF8.GetByteCount(pipe) > maximum)
            throw new PathTooLongException("The private activation endpoint exceeds the OS socket path limit.");
    }

    private static void ValidatePlatform()
    {
        if ((!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux()) ||
            RuntimeInformation.ProcessArchitecture is not (Architecture.X64 or Architecture.Arm64))
            throw new PlatformNotSupportedException("Unsupported Unix launcher platform.");
    }
}
