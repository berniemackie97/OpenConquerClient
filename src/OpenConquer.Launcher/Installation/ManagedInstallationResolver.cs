namespace OpenConquer.Launcher.Installation;

/// <summary>Resolves the managed product installed in the launcher's own package context.</summary>
internal sealed class ManagedInstallationResolver : IManagedInstallationResolver
{
    private readonly string _launcherDirectory;

    public ManagedInstallationResolver(string launcherDirectory)
    {
        if (string.IsNullOrWhiteSpace(launcherDirectory) || !Path.IsPathFullyQualified(launcherDirectory))
        {
            throw new ArgumentException("The launcher directory must be a fully qualified path.", nameof(launcherDirectory));
        }

        _launcherDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(launcherDirectory));
    }

    public async Task<ManagedInstallationResolution> ResolveAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string manifestPath = Path.Combine(_launcherDirectory, ManagedInstallationManifest.FileName);

        ManifestReadResult manifestResult = await ManagedInstallationManifest.ReadAsync(manifestPath, cancellationToken).ConfigureAwait(false);

        if (manifestResult is ManifestReadResult.Rejected rejected)
        {
            return new ManagedInstallationResolution.Rejected(rejected.Issue);
        }

        ManagedInstallationManifest manifest = ((ManifestReadResult.Accepted)manifestResult).Manifest;

        string clientRootPath = Path.Combine(_launcherDirectory, manifest.ClientRoot);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsDirectory(clientRootPath))
            {
                return new ManagedInstallationResolution.Rejected(ManagedInstallationIssue.ClientComponentMissing);
            }

            return new ManagedInstallationResolution.Resolved(ManagedInstallation.Create(_launcherDirectory, clientRootPath, manifestPath));
        }
        catch (UnauthorizedAccessException)
        {
            return new ManagedInstallationResolution.Rejected(ManagedInstallationIssue.AccessDenied);
        }
        catch (DirectoryNotFoundException)
        {
            return new ManagedInstallationResolution.Rejected(ManagedInstallationIssue.ClientComponentMissing);
        }
        catch (FileNotFoundException)
        {
            return new ManagedInstallationResolution.Rejected(ManagedInstallationIssue.ClientComponentMissing);
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
