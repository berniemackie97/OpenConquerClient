namespace OpenConquer.Product.Tool;

/// <summary>Serializes local product composition for the shared development publisher state.</summary>
internal static class DevelopmentProductBuildLock
{
    public const string FileName = "local-product-build.lock";

    private const UnixFileMode LockFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    public static FileStream Acquire(string stateRootPath)
    {
        string rootPath = DevelopmentStateDirectory.Prepare(stateRootPath);
        string lockPath = Path.Combine(rootPath, FileName);

        RejectUnsupportedEntry(lockPath);

        FileStreamOptions options = new()
        {
            Mode = FileMode.OpenOrCreate,
            Access = FileAccess.ReadWrite,
            Share = FileShare.None,
        };

        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = LockFileMode;
        }

        FileStream stream;

        try
        {
            stream = new FileStream(lockPath, options);
        }
        catch (IOException exception)
        {
            throw new InvalidOperationException("Another local OpenConquer product build is already in progress.", exception);
        }

        try
        {
            RejectUnsupportedEntry(lockPath);
            RestrictUnixPermissions(lockPath);

            return stream;
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    private static void RejectUnsupportedEntry(string path)
    {
        FileInfo file = new(path);
        file.Refresh();

        if (file.LinkTarget is not null || file.Exists && (file.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException($"Linked paths are not allowed for the local-product build lock: '{path}'.");
        }

        if (file.Exists)
        {
            return;
        }

        DirectoryInfo directory = new(path);
        directory.Refresh();

        if (directory.LinkTarget is not null || directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException($"Linked paths are not allowed for the local-product build lock: '{path}'.");
        }

        if (directory.Exists)
        {
            throw new InvalidDataException($"The local-product build-lock path must not be a directory: '{path}'.");
        }
    }

    private static void RestrictUnixPermissions(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        File.SetUnixFileMode(path, LockFileMode);

        if (File.GetUnixFileMode(path) != LockFileMode)
        {
            throw new UnauthorizedAccessException("The local-product build-lock permissions could not be restricted to the current user.");
        }
    }
}
