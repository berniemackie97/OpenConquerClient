using System.Text;

namespace OpenConquer.Launcher.Installation;

internal static class ManagedInstallationPathGuard
{
    private const StringComparison PortablePathComparison = StringComparison.OrdinalIgnoreCase;
    private const int MaximumLinkResolutionDepth = 64;

    public static bool PathsOverlap(string left, string right)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(left);
        ArgumentException.ThrowIfNullOrWhiteSpace(right);

        return AreRelated(ResolveIdentity(left), ResolveIdentity(right));
    }

    private static string ResolveIdentity(string path)
    {
        string normalizedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        return ResolveIdentityCore(normalizedPath, resolutionDepth: 0);
    }

    private static string ResolveIdentityCore(string path, int resolutionDepth)
    {
        if (resolutionDepth > MaximumLinkResolutionDepth)
        {
            throw new IOException("Filesystem link resolution exceeded the supported safety limit.");
        }

        string rootPath = Path.GetPathRoot(path) ?? throw new InvalidOperationException($"Filesystem path '{path}' has no root.");
        string currentIdentity = Path.TrimEndingDirectorySeparator(rootPath);
        string relativePath = Path.GetRelativePath(rootPath, path);

        if (relativePath == ".")
        {
            return currentIdentity;
        }

        string[] segments = relativePath.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);

        for (int index = 0; index < segments.Length; index++)
        {
            string candidatePath = Path.Combine(currentIdentity, segments[index]);
            PathEntry entry = InspectEntry(candidatePath);

            switch (entry.Kind)
            {
                case PathEntryKind.Missing:
                    for (int remainingIndex = index; remainingIndex < segments.Length; remainingIndex++)
                    {
                        currentIdentity = Path.Combine(currentIdentity, segments[remainingIndex]);
                    }

                    return currentIdentity;

                case PathEntryKind.Directory:
                    currentIdentity = candidatePath;
                    break;

                case PathEntryKind.File:
                    if (index != segments.Length - 1)
                    {
                        throw new IOException($"Filesystem path '{path}' traverses non-directory entry '{candidatePath}'.");
                    }

                    currentIdentity = candidatePath;
                    break;

                case PathEntryKind.Link:
                    ResolvedLinkTarget target = ResolveLink(entry.FileSystemInfo ?? throw new InvalidOperationException("Linked filesystem entry has no metadata object."), resolutionDepth);

                    if (!target.IsDirectory && index != segments.Length - 1)
                    {
                        throw new IOException($"Filesystem path '{path}' traverses non-directory link target '{target.Identity}'.");
                    }

                    currentIdentity = target.Identity;
                    break;

                default:
                    throw new InvalidOperationException("Unknown filesystem entry state.");
            }
        }

        return currentIdentity;
    }

    private static ResolvedLinkTarget ResolveLink(FileSystemInfo link, int resolutionDepth)
    {
        FileSystemInfo? target;

        try
        {
            target = link.ResolveLinkTarget(returnFinalTarget: true);
        }
        catch (IOException exception)
        {
            throw new InvalidDataException($"Filesystem link '{link.FullName}' could not be resolved.", exception);
        }

        if (target is null)
        {
            throw new InvalidDataException($"Filesystem reparse point '{link.FullName}' cannot be resolved safely.");
        }

        target.Refresh();

        if (!target.Exists)
        {
            throw new InvalidDataException($"Filesystem link '{link.FullName}' has no existing target.");
        }

        bool isDirectory = target is DirectoryInfo;
        string targetIdentity = ResolveIdentityCore(Path.TrimEndingDirectorySeparator(Path.GetFullPath(target.FullName)), checked(resolutionDepth + 1));

        return new ResolvedLinkTarget(targetIdentity, isDirectory);
    }

    private static PathEntry InspectEntry(string path)
    {
        DirectoryInfo directory = new(path);

        directory.Refresh();

        if (directory.LinkTarget is not null)
        {
            return new PathEntry(PathEntryKind.Link, directory);
        }

        if (directory.Exists)
        {
            return (directory.Attributes & FileAttributes.ReparsePoint) != 0
                ? new PathEntry(PathEntryKind.Link, directory)
                : new PathEntry(PathEntryKind.Directory, directory);
        }

        FileInfo file = new(path);

        file.Refresh();

        if (file.LinkTarget is not null)
        {
            return new PathEntry(PathEntryKind.Link, file);
        }

        if (file.Exists)
        {
            return (file.Attributes & FileAttributes.ReparsePoint) != 0
                ? new PathEntry(PathEntryKind.Link, file)
                : new PathEntry(PathEntryKind.File, file);
        }

        return new PathEntry(PathEntryKind.Missing, FileSystemInfo: null);
    }

    private static bool AreRelated(string first, string second)
    {
        string firstPath = CreateComparisonPath(first);
        string secondPath = CreateComparisonPath(second);
        string firstWithSeparator = EnsureTrailingDirectorySeparator(firstPath);
        string secondWithSeparator = EnsureTrailingDirectorySeparator(secondPath);

        return string.Equals(firstPath, secondPath, PortablePathComparison)
            || firstPath.StartsWith(secondWithSeparator, PortablePathComparison)
            || secondPath.StartsWith(firstWithSeparator, PortablePathComparison);
    }

    private static string CreateComparisonPath(string path)
    {
        string comparisonPath = Path.TrimEndingDirectorySeparator(path).Normalize(NormalizationForm.FormC);

        return Path.AltDirectorySeparatorChar == Path.DirectorySeparatorChar
            ? comparisonPath
            : comparisonPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    }

    private static string EnsureTrailingDirectorySeparator(string path) => Path.EndsInDirectorySeparator(path) ? path : path + Path.DirectorySeparatorChar;

    private readonly record struct PathEntry(PathEntryKind Kind, FileSystemInfo? FileSystemInfo);
    private readonly record struct ResolvedLinkTarget(string Identity, bool IsDirectory);

    private enum PathEntryKind
    {
        Missing,
        Directory,
        File,
        Link,
    }
}
