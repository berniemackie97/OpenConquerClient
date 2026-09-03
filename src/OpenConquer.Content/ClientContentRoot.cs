using System.Diagnostics.CodeAnalysis;

namespace OpenConquer.Content;

public sealed class ClientContentRoot : IClientContentSource
{
    private static readonly char[] s_pathSeparators = ['/', '\\'];

    public ClientContentRoot(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        string normalizedRootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));

        ValidateContentRoot(normalizedRootPath);

        RootPath = normalizedRootPath;
    }

    public string RootPath
    {
        get;
    }

    public bool TryResolveFile(string relativePath, [NotNullWhen(true)] out string? absolutePath)
    {
        string[] segments = ParseRelativePath(relativePath);
        string currentDirectoryPath = RootPath;

        for (int index = 0; index < segments.Length; index++)
        {
            bool isFileSegment = index == segments.Length - 1;

            string? resolvedPath = isFileSegment
                ? FindCaseInsensitiveFile(currentDirectoryPath, segments[index])
                : FindCaseInsensitiveDirectory(currentDirectoryPath, segments[index]);

            if (resolvedPath is null)
            {
                absolutePath = null;
                return false;
            }

            if (isFileSegment)
            {
                absolutePath = resolvedPath;
                return true;
            }

            currentDirectoryPath = resolvedPath;
        }

        absolutePath = null;
        return false;
    }

    public string ResolveRequiredFile(string relativePath)
    {
        if (TryResolveFile(relativePath, out string? absolutePath))
        {
            return absolutePath;
        }

        throw new FileNotFoundException($"Client content file '{relativePath}' was not found under '{RootPath}'.");
    }

    /// <summary>
    /// Opens a loose file under the content root.
    /// </summary>
    /// <remarks>
    /// A directory root owns no packages, so <see cref="ContentLookupMode.PackageOnly"/> can never
    /// be satisfied here and is reported as a miss instead of being widened to a loose read.
    /// </remarks>
    public bool TryOpenRead(string contentPath, ContentLookupMode mode, [NotNullWhen(true)] out Stream? stream)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown content lookup mode.");
        }

        if (mode == ContentLookupMode.PackageOnly || !TryResolveFile(contentPath, out string? absolutePath))
        {
            stream = null;
            return false;
        }

        stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, FileOptions.SequentialScan);

        return true;
    }

    /// <inheritdoc />
    public Stream OpenRequiredRead(string contentPath, ContentLookupMode mode)
    {
        if (TryOpenRead(contentPath, mode, out Stream? stream))
        {
            return stream;
        }

        throw new FileNotFoundException($"Client content file '{contentPath}' was not found under the configured content root using {mode} lookup.");
    }

    private static string[] ParseRelativePath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        string normalizedPath = relativePath.Trim();

        if (IsRootedPath(normalizedPath))
        {
            throw new ArgumentException("Client content paths must be relative to the client content root.", nameof(relativePath));
        }

        string[] segments = normalizedPath.Split(s_pathSeparators, StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0)
        {
            throw new ArgumentException("Client content path must contain at least one path segment.", nameof(relativePath));
        }

        foreach (string segment in segments)
        {
            if (segment is "." or ".." || segment.Contains(':', StringComparison.Ordinal))
            {
                throw new ArgumentException($"Client content path '{relativePath}' is not a valid relative content path.", nameof(relativePath));
            }
        }

        return segments;
    }

    private static bool IsRootedPath(string path)
    {
        if (path[0] is '/' or '\\')
        {
            return true;
        }

        return path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':';
    }

    private static string? FindCaseInsensitiveDirectory(string parentDirectoryPath, string directoryName)
    {
        ValidateSearchDirectory(parentDirectoryPath);

        return FindUniqueCaseInsensitiveMatch(Directory.EnumerateDirectories(parentDirectoryPath), directoryName, parentDirectoryPath, entryKind: "directory", expectedDirectory: true);
    }

    private static string? FindCaseInsensitiveFile(string parentDirectoryPath, string fileName)
    {
        ValidateSearchDirectory(parentDirectoryPath);

        return FindUniqueCaseInsensitiveMatch(Directory.EnumerateFiles(parentDirectoryPath), fileName, parentDirectoryPath, entryKind: "file", expectedDirectory: false);
    }

    private static string? FindUniqueCaseInsensitiveMatch(IEnumerable<string> candidatePaths, string expectedName, string parentDirectoryPath, string entryKind, bool expectedDirectory)
    {
        string? matchedPath = null;

        foreach (string candidatePath in candidatePaths)
        {
            string candidateName = Path.GetFileName(candidatePath);

            if (!string.Equals(candidateName, expectedName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            FileAttributes attributes = File.GetAttributes(candidatePath);

            RejectLinkedEntry(candidatePath, attributes, entryKind);

            bool isDirectory = (attributes & FileAttributes.Directory) != 0;

            if (isDirectory != expectedDirectory)
            {
                throw new IOException($"Client content {entryKind} '{candidatePath}' changed type during path resolution.");
            }

            if (matchedPath is not null)
            {
                throw new IOException($"Client content directory '{parentDirectoryPath}' contains multiple {entryKind} entries matching '{expectedName}' case-insensitively.");
            }

            matchedPath = candidatePath;
        }

        return matchedPath;
    }

    private static void ValidateContentRoot(string rootPath)
    {
        FileAttributes attributes;

        try
        {
            attributes = File.GetAttributes(rootPath);
        }
        catch (FileNotFoundException exception)
        {
            throw new DirectoryNotFoundException($"Client content root '{rootPath}' does not exist.", exception);
        }

        RejectLinkedEntry(rootPath, attributes, entryKind: "root");

        if ((attributes & FileAttributes.Directory) == 0)
        {
            throw new IOException($"Client content root '{rootPath}' is not a directory.");
        }

        using IEnumerator<string> entries = Directory.EnumerateFileSystemEntries(rootPath).GetEnumerator();

        _ = entries.MoveNext();
    }

    private static void ValidateSearchDirectory(string directoryPath)
    {
        FileAttributes attributes = File.GetAttributes(directoryPath);

        RejectLinkedEntry(directoryPath, attributes, entryKind: "directory");

        if ((attributes & FileAttributes.Directory) == 0)
        {
            throw new IOException($"Client content path '{directoryPath}' is not a directory.");
        }
    }

    private static void RejectLinkedEntry(string path, FileAttributes attributes, string entryKind)
    {
        bool isDirectory = (attributes & FileAttributes.Directory) != 0;

        FileSystemInfo fileSystemInfo = isDirectory ? new DirectoryInfo(path) : new FileInfo(path);

        bool isLinked = (attributes & FileAttributes.ReparsePoint) != 0 || fileSystemInfo.LinkTarget is not null;

        if (!isLinked)
        {
            return;
        }

        throw new IOException($"Client content {entryKind} '{path}' is a symbolic link or reparse point. Linked content entries are not permitted.");
    }
}
