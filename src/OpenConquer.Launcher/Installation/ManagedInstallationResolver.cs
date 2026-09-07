namespace OpenConquer.Launcher.Installation;

/// <summary>Resolves the managed product installed in the launcher's own package context.</summary>
internal sealed class ManagedInstallationResolver : IManagedInstallationResolver
{
    private readonly string _launcherDirectory;
    private readonly ManagedReleaseVerifier _releaseVerifier;

    public ManagedInstallationResolver(string launcherDirectory, TrustedReleaseKeys trustedReleaseKeys)
    {
        ArgumentNullException.ThrowIfNull(trustedReleaseKeys);
        if (string.IsNullOrWhiteSpace(launcherDirectory) || !Path.IsPathFullyQualified(launcherDirectory))
        {
            throw new ArgumentException("The launcher directory must be a fully qualified path.", nameof(launcherDirectory));
        }

        _launcherDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(launcherDirectory));
        _releaseVerifier = new ManagedReleaseVerifier(trustedReleaseKeys);
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

            string releaseManifestPath = Path.Combine(_launcherDirectory, ManagedReleaseManifest.FileName);
            ReleaseManifestReadResult releaseManifestResult = await ManagedReleaseManifest.ReadAsync(releaseManifestPath, cancellationToken).ConfigureAwait(false);
            if (releaseManifestResult is ReleaseManifestReadResult.Rejected releaseManifestRejected)
            {
                return new ManagedInstallationResolution.Rejected(releaseManifestRejected.Issue);
            }

            ReleaseManifestReadResult.Accepted acceptedManifest = (ReleaseManifestReadResult.Accepted)releaseManifestResult;
            string releaseSignaturePath = Path.Combine(_launcherDirectory, ManagedReleaseSignature.FileName);
            ReleaseSignatureReadResult releaseSignatureResult = await ManagedReleaseSignature.ReadAsync(releaseSignaturePath, cancellationToken).ConfigureAwait(false);
            if (releaseSignatureResult is ReleaseSignatureReadResult.Rejected releaseSignatureRejected)
            {
                return new ManagedInstallationResolution.Rejected(releaseSignatureRejected.Issue);
            }

            ManagedReleaseVerification verification = await _releaseVerifier.VerifyAsync(clientRootPath, acceptedManifest.Manifest, acceptedManifest.Bytes, ((ReleaseSignatureReadResult.Accepted)releaseSignatureResult).Signature, cancellationToken).ConfigureAwait(false);
            if (verification is ManagedReleaseVerification.Rejected verificationRejected)
            {
                return new ManagedInstallationResolution.Rejected(verificationRejected.Issue);
            }

            ManagedReleaseVerification.Verified verified = (ManagedReleaseVerification.Verified)verification;
            return new ManagedInstallationResolution.Resolved(ManagedInstallation.Create(rootPath: _launcherDirectory, clientRootPath, manifestPath, releaseManifestPath, releaseSignaturePath, verified.ClientExecutablePath, verified.Release));
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

}
