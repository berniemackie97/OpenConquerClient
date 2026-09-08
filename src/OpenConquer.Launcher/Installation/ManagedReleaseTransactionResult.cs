namespace OpenConquer.Launcher.Installation;

internal enum ManagedReleaseChangeKind
{
    Update,
    Repair,
}

internal enum ManagedReleaseChangeIssue
{
    InstallationUnavailable,
    CandidateRejected,
    ReleaseNotNewer,
    ReleaseRollbackBlocked,
    RepairReleaseMismatch,
    FallbackUnavailable,
    OperationAlreadyInProgress,
    AccessDenied,
    LinkedPath,
    FileSystemFailure,
}

internal abstract record ManagedReleaseTransactionResult
{
    private ManagedReleaseTransactionResult()
    {
    }

    internal sealed record Applied(ManagedInstallation Installation) : ManagedReleaseTransactionResult;

    internal sealed record Rejected(ManagedReleaseChangeIssue Issue, ManagedInstallationIssue? IntegrityIssue = null) : ManagedReleaseTransactionResult;
}
