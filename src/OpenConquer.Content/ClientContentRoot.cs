using System.Diagnostics.CodeAnalysis;

namespace OpenConquer.Content;

public sealed class ClientContentRoot
{
    private static readonly char[] s_pathSeparators = ['/', '\\'];

    public ClientContentRoot(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        string normalizedRootPath = Path.GetFullPath(rootPath);

        if (!Directory.Exists(normalizedRootPath))
        {
            throw new DirectoryNotFoundException($"Client content root '{normalizedRootPath}' does not exist.");
        }

        RootPath = Path.TrimEndingDirectorySeparator(normalizedRootPath);
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
                ? FindCaseInsensitiveFile(currentDirectoryPath, fileName: segments[index])
                : FindCaseInsensitiveDirectory(currentDirectoryPath, directoryName: segments[index]);

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

    private static string[] ParseRelativePath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        string normalizedPath = relativePath.Trim();

        if (IsLegacyRootedPath(normalizedPath))
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
                throw new ArgumentException($"Client content path '{relativePath}' is not a valid legacy relative path.", nameof(relativePath));
            }
        }

        return segments;
    }

    private static bool IsLegacyRootedPath(string path)
    {
        if (path[0] is '/' or '\\')
        {
            return true;
        }

        return path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':';
    }

    private static string? FindCaseInsensitiveDirectory(string parentDirectoryPath, string directoryName)
    {
        return FindUniqueCaseInsensitiveMatch(candidatePaths: Directory.EnumerateDirectories(parentDirectoryPath),
            directoryName, parentDirectoryPath, entryKind: "directory");
    }

    private static string? FindCaseInsensitiveFile(string parentDirectoryPath, string fileName)
    {
        return FindUniqueCaseInsensitiveMatch(candidatePaths: Directory.EnumerateFiles(parentDirectoryPath),
            fileName, parentDirectoryPath, entryKind: "file");
    }

    private static string? FindUniqueCaseInsensitiveMatch(IEnumerable<string> candidatePaths, string expectedName, string parentDirectoryPath, string entryKind)
    {
        string? matchedPath = null;

        foreach (string candidatePath in candidatePaths)
        {
            string candidateName = Path.GetFileName(candidatePath);

            if (!string.Equals(candidateName, expectedName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (matchedPath is not null)
            {
                throw new IOException($"Client content directory '{parentDirectoryPath}' contains multiple {entryKind} entries matching '{expectedName}' case-insensitively.");
            }

            matchedPath = candidatePath;
        }

        return matchedPath;
    }
}
