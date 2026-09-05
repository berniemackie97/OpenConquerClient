namespace OpenConquer.Launcher.Installation;

/// <summary>Resolves the managed product installed in the launcher's own package context.</summary>
internal sealed class ManagedInstallationResolver : IManagedInstallationResolver
{
    private readonly string _launcherDirectory;

    public ManagedInstallationResolver(string launcherDirectory)
    {
        if (string.IsNullOrWhiteSpace(launcherDirectory) ||
            !Path.IsPathFullyQualified(launcherDirectory))
        {
            throw new ArgumentException(
                "The launcher directory must be a fully qualified path.",
                nameof(launcherDirectory)
            );
        }

        _launcherDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(launcherDirectory));
    }

    public async Task<ManagedInstallationResolution> ResolveAsync(
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        string manifestPath = Path.Combine(_launcherDirectory, ManagedInstallationManifest.FileName);
        ManifestReadResult manifestResult = await ManagedInstallationManifest.ReadAsync(
            manifestPath,
            cancellationToken
        ).ConfigureAwait(false);

        if (manifestResult is ManifestReadResult.Rejected rejected)
        {
            return new ManagedInstallationResolution.Rejected(rejected.Issue);
        }

        ManagedInstallationManifest manifest = ((ManifestReadResult.Accepted)manifestResult).Manifest;
        if (!TryResolveChildDirectory(
                _launcherDirectory,
                manifest.ClientRoot,
                out string? clientRootPath
            ))
        {
            return new ManagedInstallationResolution.Rejected(ManagedInstallationIssue.ManifestInvalid);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (clientRootPath is null || !IsDirectory(clientRootPath))
            {
                return new ManagedInstallationResolution.Rejected(
                    ManagedInstallationIssue.ClientComponentMissing
                );
            }

            return new ManagedInstallationResolution.Resolved(
                ManagedInstallation.Create(_launcherDirectory, clientRootPath, manifestPath)
            );
        }
        catch (UnauthorizedAccessException)
        {
            return new ManagedInstallationResolution.Rejected(ManagedInstallationIssue.AccessDenied);
        }
        catch (DirectoryNotFoundException)
        {
            return new ManagedInstallationResolution.Rejected(
                ManagedInstallationIssue.ClientComponentMissing
            );
        }
        catch (FileNotFoundException)
        {
            return new ManagedInstallationResolution.Rejected(
                ManagedInstallationIssue.ClientComponentMissing
            );
        }
        catch (LinkedInstallationPathException)
        {
            return new ManagedInstallationResolution.Rejected(ManagedInstallationIssue.LinkedPath);
        }
        catch (IOException)
        {
            return new ManagedInstallationResolution.Rejected(ManagedInstallationIssue.ReadFailure);
        }
    }

    private static bool TryResolveChildDirectory(
        string rootPath,
        string relativePath,
        out string? resolvedPath
    )
    {
        resolvedPath = null;
        if (string.IsNullOrWhiteSpace(relativePath) ||
            Path.IsPathFullyQualified(relativePath) ||
            relativePath.AsSpan().ContainsAny(Path.GetInvalidPathChars()))
        {
            return false;
        }

        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        string rootWithSeparator = rootPath.EndsWith(Path.DirectorySeparatorChar) ||
            rootPath.EndsWith(Path.AltDirectorySeparatorChar)
            ? rootPath
            : rootPath + Path.DirectorySeparatorChar;
        string candidate;
        try
        {
            candidate = Path.GetFullPath(Path.Combine(rootPath, relativePath));
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (PathTooLongException)
        {
            return false;
        }

        if (string.Equals(candidate, rootPath, comparison) ||
            !candidate.StartsWith(rootWithSeparator, comparison))
        {
            return false;
        }

        resolvedPath = Path.TrimEndingDirectorySeparator(candidate);
        return true;
    }

    private static bool IsDirectory(string path)
    {
        FileAttributes attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new LinkedInstallationPathException();
        }

        return (attributes & (FileAttributes.Directory | FileAttributes.Device)) == FileAttributes.Directory;
    }

    private sealed class LinkedInstallationPathException : IOException;
}
