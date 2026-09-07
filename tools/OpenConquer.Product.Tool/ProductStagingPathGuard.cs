using System.Text;

namespace OpenConquer.Product.Tool;

/// <summary>
/// Resolves and validates filesystem identities used while composing a managed
/// product.
/// </summary>
internal static class ProductStagingPathGuard
{
    /*
     * Managed-product roots must remain portable between Windows, macOS, and
     * Linux. Relationship checks therefore use a conservative case-insensitive
     * policy even when the current filesystem is case-sensitive.
     *
     * Unicode normalization is comparison-only. Actual filesystem paths are
     * never rewritten.
     */
    private const StringComparison PortablePathComparison = StringComparison.OrdinalIgnoreCase;

    private const int MaximumLinkResolutionDepth = 64;

    public static string RequireDirectory(string path, string parameterName)
    {
        string normalizedPath = NormalizePath(path, parameterName);

        PathEntry entry = InspectEntry(normalizedPath);

        switch (entry.Kind)
        {
            case PathEntryKind.Link:
                throw new InvalidDataException(
                    $"Linked paths are not allowed for product staging roots: '{normalizedPath}'."
                );

            case PathEntryKind.Directory:
                /*
                 * Resolve the full identity as well. Ancestor aliases such as
                 * macOS /var -> /private/var are valid, but dangling or invalid
                 * ancestors are not.
                 */
                _ = ResolveIdentity(normalizedPath);

                return normalizedPath;

            case PathEntryKind.File:
            case PathEntryKind.Missing:
                throw new DirectoryNotFoundException(
                    $"The {parameterName} directory '{normalizedPath}' does not exist."
                );

            default:
                throw new InvalidOperationException("Unknown filesystem entry state.");
        }
    }

    public static string RequireRegularFile(string path, string parameterName)
    {
        string normalizedPath = NormalizePath(path, parameterName);
        return InspectEntry(normalizedPath).Kind switch
        {
            PathEntryKind.File => normalizedPath,
            PathEntryKind.Link => throw new InvalidDataException(
                $"Linked paths are not allowed for {parameterName}: '{normalizedPath}'."),
            _ => throw new FileNotFoundException($"The {parameterName} file '{normalizedPath}' does not exist.", normalizedPath),
        };
    }

    public static string NormalizePath(string path, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, parameterName);

        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException("Product staging paths must be absolute.", parameterName);
        }

        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException(
                "Product staging path is invalid.",
                parameterName,
                exception
            );
        }
        catch (NotSupportedException exception)
        {
            throw new ArgumentException(
                "Product staging path is unsupported.",
                parameterName,
                exception
            );
        }
        catch (PathTooLongException exception)
        {
            throw new ArgumentException(
                "Product staging path is too long.",
                parameterName,
                exception
            );
        }
    }

    public static void RejectOverlappingRoots(
        string launcherRoot,
        string clientRoot,
        string outputRoot
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(launcherRoot);

        ArgumentException.ThrowIfNullOrWhiteSpace(clientRoot);

        ArgumentException.ThrowIfNullOrWhiteSpace(outputRoot);

        string launcherIdentity = ResolveIdentity(launcherRoot);

        string clientIdentity = ResolveIdentity(clientRoot);

        string outputIdentity = ResolveIdentity(outputRoot);

        if (
            AreRelated(launcherIdentity, clientIdentity)
            || AreRelated(launcherIdentity, outputIdentity)
            || AreRelated(clientIdentity, outputIdentity)
        )
        {
            throw new InvalidOperationException(
                "Launcher, client, and output paths must not overlap."
            );
        }
    }

    public static void RejectPathWithinRoot(string rootPath, string candidatePath, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidatePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (AreRelated(ResolveIdentity(rootPath), ResolveIdentity(candidatePath)))
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void PrepareFileOutput(string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        EnsureOutputDoesNotExist(outputPath);
        string? parentPath = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(parentPath))
        {
            throw new InvalidOperationException("The output file must have a parent directory.");
        }

        _ = ResolveIdentity(parentPath);
        Directory.CreateDirectory(parentPath);
        _ = RequireDirectory(parentPath, "output parent");
        EnsureOutputDoesNotExist(outputPath);
    }

    public static void PrepareOutputParent(string outputRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRoot);

        EnsureOutputDoesNotExist(outputRoot);

        string? parentPath = Path.GetDirectoryName(outputRoot);

        if (string.IsNullOrWhiteSpace(parentPath))
        {
            throw new InvalidOperationException("The product output must have a parent directory.");
        }

        /*
         * Resolve the prospective parent first. Existing aliases are followed
         * for identity purposes; dangling aliases and non-directory ancestors
         * fail before anything is created.
         */
        _ = ResolveIdentity(parentPath);

        Directory.CreateDirectory(parentPath);

        /*
         * Resolve again after creation so newly created components participate
         * in the same identity model.
         */
        _ = ResolveIdentity(parentPath);

        /*
         * Another process may have claimed the requested destination while its
         * parent was being prepared. Staging never adopts or replaces it.
         */
        EnsureOutputDoesNotExist(outputRoot);
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
            throw new IOException(
                "Filesystem link resolution exceeded the supported safety limit."
            );
        }

        string rootPath =
            Path.GetPathRoot(path)
            ?? throw new InvalidOperationException($"Filesystem path '{path}' has no root.");

        string currentIdentity = Path.TrimEndingDirectorySeparator(rootPath);

        string relativePath = Path.GetRelativePath(rootPath, path);

        if (relativePath == ".")
        {
            return currentIdentity;
        }

        string[] segments = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries
        );

        for (int index = 0; index < segments.Length; index++)
        {
            string candidatePath = Path.Combine(currentIdentity, segments[index]);

            PathEntry entry = InspectEntry(candidatePath);

            switch (entry.Kind)
            {
                case PathEntryKind.Missing:
                    /*
                     * No deeper component can already exist beneath a missing
                     * ancestor. Append the remaining lexical segments to the
                     * resolved existing prefix.
                     */
                    for (
                        int remainingIndex = index;
                        remainingIndex < segments.Length;
                        remainingIndex++
                    )
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
                        throw new IOException(
                            $"Filesystem path '{path}' traverses non-directory entry '{candidatePath}'."
                        );
                    }

                    currentIdentity = candidatePath;

                    break;

                case PathEntryKind.Link:
                    ResolvedLinkTarget target = ResolveLink(
                        entry.FileSystemInfo
                            ?? throw new InvalidOperationException(
                                "Linked filesystem entry has no metadata object."
                            ),
                        resolutionDepth
                    );

                    if (!target.IsDirectory && index != segments.Length - 1)
                    {
                        throw new IOException(
                            $"Filesystem path '{path}' traverses non-directory link target '{target.Identity}'."
                        );
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
            throw new InvalidDataException(
                $"Filesystem link '{link.FullName}' could not be resolved.",
                exception
            );
        }

        if (target is null)
        {
            throw new InvalidDataException(
                $"Filesystem reparse point '{link.FullName}' cannot be resolved safely."
            );
        }

        target.Refresh();

        if (!target.Exists)
        {
            throw new InvalidDataException(
                $"Filesystem link '{link.FullName}' has no existing target."
            );
        }

        bool isDirectory = target is DirectoryInfo;

        string targetIdentity = ResolveIdentityCore(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(target.FullName)),
            checked(resolutionDepth + 1)
        );

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
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                return new PathEntry(PathEntryKind.Link, directory);
            }

            return new PathEntry(PathEntryKind.Directory, directory);
        }

        /*
         * DirectoryInfo.Exists is false for ordinary files and dangling
         * symlinks. Inspect the same lexical entry as a FileInfo before
         * deciding it is absent.
         */
        FileInfo file = new(path);

        file.Refresh();

        if (file.LinkTarget is not null)
        {
            return new PathEntry(PathEntryKind.Link, file);
        }

        if (file.Exists)
        {
            if ((file.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                return new PathEntry(PathEntryKind.Link, file);
            }

            return new PathEntry(PathEntryKind.File, file);
        }

        return new PathEntry(PathEntryKind.Missing, FileSystemInfo: null);
    }

    private static bool AreRelated(string first, string second)
    {
        string firstComparisonPath = CreateComparisonPath(first);

        string secondComparisonPath = CreateComparisonPath(second);

        string firstWithSeparator = EnsureTrailingDirectorySeparator(firstComparisonPath);

        string secondWithSeparator = EnsureTrailingDirectorySeparator(secondComparisonPath);

        return string.Equals(firstComparisonPath, secondComparisonPath, PortablePathComparison)
            || firstComparisonPath.StartsWith(secondWithSeparator, PortablePathComparison)
            || secondComparisonPath.StartsWith(firstWithSeparator, PortablePathComparison);
    }

    private static string CreateComparisonPath(string path)
    {
        string comparisonPath = Path.TrimEndingDirectorySeparator(path)
            .Normalize(NormalizationForm.FormC);

        if (Path.AltDirectorySeparatorChar != Path.DirectorySeparatorChar)
        {
            comparisonPath = comparisonPath.Replace(
                Path.AltDirectorySeparatorChar,
                Path.DirectorySeparatorChar
            );
        }

        return comparisonPath;
    }

    private static string EnsureTrailingDirectorySeparator(string path)
    {
        return Path.EndsInDirectorySeparator(path) ? path : path + Path.DirectorySeparatorChar;
    }

    private static void EnsureOutputDoesNotExist(string outputRoot)
    {
        PathEntry entry = InspectEntry(outputRoot);

        switch (entry.Kind)
        {
            case PathEntryKind.Missing:
                return;

            case PathEntryKind.Link:
                throw new InvalidDataException(
                    $"The output path '{outputRoot}' is a linked filesystem entry; staging never replaces or follows an existing destination."
                );

            case PathEntryKind.Directory:
            case PathEntryKind.File:
                throw new InvalidOperationException(
                    $"The output path '{outputRoot}' already exists; staging never replaces an existing product."
                );

            default:
                throw new InvalidOperationException("Unknown filesystem entry state.");
        }
    }

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
