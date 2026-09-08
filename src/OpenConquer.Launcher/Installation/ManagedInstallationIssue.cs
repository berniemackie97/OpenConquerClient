namespace OpenConquer.Launcher.Installation;

/// <summary>Expected reasons a managed OpenConquer installation cannot be resolved.</summary>
internal enum ManagedInstallationIssue
{
    ManifestMissing,
    ManifestInvalid,
    UnsupportedManifest,
    ActiveReleaseMissing,
    ReleaseIdentityMismatch,
    ClientComponentMissing,
    ReleaseMetadataMissing,
    ReleaseManifestInvalid,
    UnsupportedReleaseManifest,
    ReleaseAuthorityUnavailable,
    ReleaseSignatureInvalid,
    LauncherUpdateRequired,
    ClientPlatformMismatch,
    ClientIntegrityFailure,
    AccessDenied,
    LinkedPath,
    ReadFailure,
}
