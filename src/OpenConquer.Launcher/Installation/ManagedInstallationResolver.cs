namespace OpenConquer.Launcher.Installation;

/// <summary>Resolves the managed product installed in the launcher's own package context.</summary>
internal sealed class ManagedInstallationResolver : IManagedInstallationResolver
{
    private readonly string _launcherDirectory;
    private readonly ManagedReleaseVerifier _releaseVerifier;

    public ManagedInstallationResolver(string launcherDirectory, TrustedReleaseKeys trustedReleaseKeys, string? currentRuntime = null)
    {
        ArgumentNullException.ThrowIfNull(trustedReleaseKeys);
        if (string.IsNullOrWhiteSpace(launcherDirectory) || !Path.IsPathFullyQualified(launcherDirectory))
        {
            throw new ArgumentException("The launcher directory must be a fully qualified path.", nameof(launcherDirectory));
        }

        _launcherDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(launcherDirectory));
        _releaseVerifier = new ManagedReleaseVerifier(trustedReleaseKeys, currentRuntime);
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

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsDirectory(_launcherDirectory))
            {
                return new ManagedInstallationResolution.Rejected(ManagedInstallationIssue.ManifestMissing);
            }

            string releaseRootPath;
            string? activeReleaseId;

            if (manifest.SchemaVersion == 1)
            {
                releaseRootPath = _launcherDirectory;
                activeReleaseId = null;
            }
            else
            {
                string releasesRootPath = Path.Combine(_launcherDirectory, ManagedInstallationManifest.ReleasesRoot);
                if (!IsDirectory(releasesRootPath))
                {
                    return new ManagedInstallationResolution.Rejected(ManagedInstallationIssue.ActiveReleaseMissing);
                }

                activeReleaseId = manifest.ActiveRelease!;
                releaseRootPath = Path.Combine(releasesRootPath, activeReleaseId);
                if (!IsDirectory(releaseRootPath))
                {
                    return new ManagedInstallationResolution.Rejected(ManagedInstallationIssue.ActiveReleaseMissing);
                }

                if (!ManagedReleaseDirectory.HasExpectedShape(releaseRootPath))
                {
                    return new ManagedInstallationResolution.Rejected(ManagedInstallationIssue.ClientIntegrityFailure);
                }
            }

            string clientRootPath = Path.Combine(releaseRootPath, ManagedInstallationManifest.ExpectedClientRoot);

            if (!IsDirectory(clientRootPath))
            {
                return new ManagedInstallationResolution.Rejected(ManagedInstallationIssue.ClientComponentMissing);
            }

            string releaseManifestPath = Path.Combine(releaseRootPath, ManagedReleaseManifest.FileName);
            ReleaseManifestReadResult releaseManifestResult = await ManagedReleaseManifest.ReadAsync(releaseManifestPath, cancellationToken).ConfigureAwait(false);
            if (releaseManifestResult is ReleaseManifestReadResult.Rejected releaseManifestRejected)
            {
                return new ManagedInstallationResolution.Rejected(releaseManifestRejected.Issue);
            }

            ReleaseManifestReadResult.Accepted acceptedManifest = (ReleaseManifestReadResult.Accepted)releaseManifestResult;

            if (activeReleaseId is not null && !ManagedReleaseId.Matches(activeReleaseId, acceptedManifest.Manifest.ReleaseSequence, acceptedManifest.Bytes))
            {
                return new ManagedInstallationResolution.Rejected(ManagedInstallationIssue.ReleaseIdentityMismatch);
            }

            string releaseSignaturePath = Path.Combine(releaseRootPath, ManagedReleaseSignature.FileName);
            ReleaseSignatureReadResult releaseSignatureResult = await ManagedReleaseSignature.ReadAsync(releaseSignaturePath, cancellationToken).ConfigureAwait(false);
            if (releaseSignatureResult is ReleaseSignatureReadResult.Rejected releaseSignatureRejected)
            {
                return new ManagedInstallationResolution.Rejected(releaseSignatureRejected.Issue);
            }

            ReleaseSignatureReadResult.Accepted acceptedSignature = (ReleaseSignatureReadResult.Accepted)releaseSignatureResult;
            ManagedReleaseVerification verification = await _releaseVerifier.VerifyAsync(clientRootPath, releaseManifestPath, releaseSignaturePath, acceptedManifest.Manifest, acceptedManifest.Bytes, acceptedSignature.Signature, acceptedSignature.Bytes, cancellationToken).ConfigureAwait(false);
            if (verification is ManagedReleaseVerification.Rejected verificationRejected)
            {
                return new ManagedInstallationResolution.Rejected(verificationRejected.Issue);
            }

            if (activeReleaseId is not null && !ManagedReleaseDirectory.HasExpectedShape(releaseRootPath))
            {
                return new ManagedInstallationResolution.Rejected(ManagedInstallationIssue.ClientIntegrityFailure);
            }

            ManagedReleaseVerification.Verified verified = (ManagedReleaseVerification.Verified)verification;
            return new ManagedInstallationResolution.Resolved(ManagedInstallation.Create(rootPath: _launcherDirectory, releaseRootPath, clientRootPath, manifestPath, releaseManifestPath, releaseSignaturePath, verified.ClientExecutablePath, verified.Release, activeReleaseId, manifest.FallbackRelease));
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
        FileAttributes attributes;
        try
        {
            attributes = File.GetAttributes(path);
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }

        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new LinkedInstallationPathException();
        }

        return (attributes & (FileAttributes.Directory | FileAttributes.Device)) == FileAttributes.Directory;
    }

}
