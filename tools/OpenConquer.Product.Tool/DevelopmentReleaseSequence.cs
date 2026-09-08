using System.Globalization;
using System.Text;

namespace OpenConquer.Product.Tool;

/// <summary>Durably reserves monotonically increasing release sequences for local development releases.</summary>
internal static class DevelopmentReleaseSequence
{
    public const string FileName = "release-sequence.txt";
    public const string LockFileName = "release-sequence.lock";

    private const int MaximumSequenceLength = 20;

    private const UnixFileMode StateFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    public static ulong Reserve(string stateRootPath)
    {
        return Reserve(stateRootPath, minimumExclusive: 0);
    }

    public static ulong Reserve(string stateRootPath, ulong minimumExclusive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateRootPath);

        string rootPath = ProductStagingPathGuard.RequireDirectory(stateRootPath, nameof(stateRootPath));
        string sequencePath = Path.Combine(rootPath, FileName);
        string lockPath = Path.Combine(rootPath, LockFileName);

        using FileStream sequenceLock = AcquireLock(lockPath);

        ulong current = ReadCurrent(sequencePath);
        ulong baseline = Math.Max(current, minimumExclusive);

        if (baseline == ulong.MaxValue)
        {
            throw new InvalidOperationException("The local-development release sequence has been exhausted.");
        }

        ulong next = baseline + 1;

        WriteReservedSequence(sequencePath, next);

        return next;
    }

    private static FileStream AcquireLock(string lockPath)
    {
        RejectUnsupportedEntry(lockPath, "development release-sequence lock");

        FileStreamOptions options = new()
        {
            Mode = FileMode.OpenOrCreate,
            Access = FileAccess.ReadWrite,
            Share = FileShare.None,
        };

        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = StateFileMode;
        }

        FileStream stream;

        try
        {
            stream = new FileStream(lockPath, options);
        }
        catch (IOException exception)
        {
            throw new InvalidOperationException("The local-development release sequence is already being reserved by another process.", exception);
        }

        try
        {
            RejectUnsupportedEntry(lockPath, "development release-sequence lock");
            RestrictUnixPermissions(lockPath, "development release-sequence lock");
            return stream;
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    private static ulong ReadCurrent(string sequencePath)
    {
        RejectUnsupportedEntry(sequencePath, "development release-sequence state");

        if (!File.Exists(sequencePath))
        {
            return 0;
        }

        string path = ProductStagingPathGuard.RequireRegularFile(sequencePath, "development release-sequence state");

        RestrictUnixPermissions(path, "development release-sequence state");

        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.None);

        if (stream.Length is <= 0 or > MaximumSequenceLength)
        {
            throw new InvalidDataException("The local-development release-sequence state is invalid.");
        }

        byte[] bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);

        if (stream.Length != bytes.Length)
        {
            throw new IOException("The local-development release-sequence state changed while it was being read.");
        }

        string text = Encoding.ASCII.GetString(bytes);

        if (!ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out ulong sequence) || sequence == 0
            || !string.Equals(sequence.ToString(CultureInfo.InvariantCulture), text, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The local-development release-sequence state is invalid.");
        }

        return sequence;
    }

    private static void WriteReservedSequence(string sequencePath, ulong sequence)
    {
        string temporaryPath = sequencePath + $".tmp-{Guid.NewGuid():N}";
        byte[] bytes = Encoding.ASCII.GetBytes(sequence.ToString(CultureInfo.InvariantCulture));
        bool completed = false;

        try
        {
            FileStreamOptions options = new()
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
            };

            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = StateFileMode;
            }

            using (FileStream stream = new(temporaryPath, options))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            RestrictUnixPermissions(temporaryPath, "development release-sequence state");

            File.Move(temporaryPath, sequencePath, overwrite: true);

            RestrictUnixPermissions(sequencePath, "development release-sequence state");

            completed = true;
        }
        finally
        {
            if (!completed)
            {
                TryDelete(temporaryPath);
            }
        }
    }

    private static void RejectUnsupportedEntry(string path, string description)
    {
        FileInfo file = new(path);
        file.Refresh();

        if (file.LinkTarget is not null || file.Exists && (file.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException($"Linked paths are not allowed for {description}: '{path}'.");
        }

        if (file.Exists)
        {
            return;
        }

        DirectoryInfo directory = new(path);
        directory.Refresh();

        if (directory.LinkTarget is not null || directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException($"Linked paths are not allowed for {description}: '{path}'.");
        }

        if (directory.Exists)
        {
            throw new InvalidDataException($"The {description} path must not be a directory: '{path}'.");
        }
    }

    private static void RestrictUnixPermissions(string path, string description)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        File.SetUnixFileMode(path, StateFileMode);

        if (File.GetUnixFileMode(path) != StateFileMode)
        {
            throw new UnauthorizedAccessException($"The {description} permissions could not be restricted to the current user.");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
