using System.Diagnostics.CodeAnalysis;
using OpenConquer.Launcher.Installation;

namespace OpenConquer.Launcher.Updates;

internal abstract record ManagedReleaseMaintenanceResult
{
    private ManagedReleaseMaintenanceResult()
    {
    }

    internal sealed record UpToDate(ManagedInstallation Installation)
        : ManagedReleaseMaintenanceResult;

    internal sealed record Changed(
        ManagedReleaseChangeKind Kind,
        ManagedInstallation Installation) : ManagedReleaseMaintenanceResult;

    internal sealed record CatalogRejected(
        ReleaseTransferIssue? TransferIssue,
        ReleaseCatalogIssue? CatalogIssue) : ManagedReleaseMaintenanceResult;

    internal sealed record AcquisitionRejected(
        ReleaseAcquisitionIssue Issue,
        ReleaseTransferIssue? TransferIssue,
        ReleasePackageIssue? PackageIssue) : ManagedReleaseMaintenanceResult;

    internal sealed record ChangeRejected(
        ManagedReleaseChangeIssue Issue,
        ManagedInstallationIssue? IntegrityIssue) : ManagedReleaseMaintenanceResult;
}

/// <summary>
/// Coordinates authenticated release selection, acquisition, update, and same-release repair.
/// It does not own account authentication or game-session protocol behavior.
/// </summary>
internal sealed class ManagedReleaseMaintenance
{
    private readonly IManagedInstallationResolver _installationResolver;
    private readonly ReleaseCatalogClient _catalogClient;
    private readonly ReleaseAcquisitionWorkspace _acquisitionWorkspace;
    private readonly ManagedReleaseTransaction _transaction;

    public ManagedReleaseMaintenance(
        IManagedInstallationResolver installationResolver,
        ReleaseCatalogClient catalogClient,
        ReleaseAcquisitionWorkspace acquisitionWorkspace,
        ManagedReleaseTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(installationResolver);
        ArgumentNullException.ThrowIfNull(catalogClient);
        ArgumentNullException.ThrowIfNull(acquisitionWorkspace);
        ArgumentNullException.ThrowIfNull(transaction);
        _installationResolver = installationResolver;
        _catalogClient = catalogClient;
        _acquisitionWorkspace = acquisitionWorkspace;
        _transaction = transaction;
    }

    public async Task<ManagedReleaseMaintenanceResult> RunAsync(
        CancellationToken cancellationToken)
    {
        ManagedInstallationResolution initial = await _installationResolver.ResolveAsync(
            cancellationToken).ConfigureAwait(false);
        ReleaseCatalogFetchResult catalog = await _catalogClient.FetchLatestAsync(
            cancellationToken).ConfigureAwait(false);
        if (catalog is ReleaseCatalogFetchResult.Rejected catalogRejected)
        {
            return new ManagedReleaseMaintenanceResult.CatalogRejected(
                catalogRejected.TransferIssue, catalogRejected.CatalogIssue);
        }

        ReleaseCatalogEntry release = ((ReleaseCatalogFetchResult.Selected)catalog).Release;
        if (TryUseCurrent(initial, release.ReleaseSequence,
                out ManagedInstallation? currentInstallation))
        {
            return new ManagedReleaseMaintenanceResult.UpToDate(currentInstallation);
        }

        ReleaseAcquisitionResult acquisition = await _acquisitionWorkspace.AcquireAsync(
            release, cancellationToken).ConfigureAwait(false);
        if (acquisition is ReleaseAcquisitionResult.Rejected acquisitionRejected)
        {
            return new ManagedReleaseMaintenanceResult.AcquisitionRejected(
                acquisitionRejected.Issue, acquisitionRejected.TransferIssue,
                acquisitionRejected.PackageIssue);
        }

        using ReleaseCandidateLease candidate =
            ((ReleaseAcquisitionResult.Acquired)acquisition).Candidate;
        ManagedReleaseTransactionResult update = await _transaction.ApplyAsync(
            candidate.CandidateRoot, ManagedReleaseChangeKind.Update, cancellationToken)
            .ConfigureAwait(false);
        if (update is ManagedReleaseTransactionResult.Applied updateApplied)
        {
            return new ManagedReleaseMaintenanceResult.Changed(
                ManagedReleaseChangeKind.Update, updateApplied.Installation);
        }

        ManagedReleaseTransactionResult.Rejected updateRejected =
            (ManagedReleaseTransactionResult.Rejected)update;
        if (updateRejected.Issue is ManagedReleaseChangeIssue.ReleaseNotNewer or
            ManagedReleaseChangeIssue.ReleaseRollbackBlocked)
        {
            ManagedInstallationResolution refreshed = await _installationResolver.ResolveAsync(
                cancellationToken).ConfigureAwait(false);
            if (TryUseCurrent(refreshed, release.ReleaseSequence,
                    out ManagedInstallation? refreshedInstallation))
            {
                return new ManagedReleaseMaintenanceResult.UpToDate(refreshedInstallation);
            }

            if (updateRejected.Issue == ManagedReleaseChangeIssue.ReleaseNotNewer)
            {
                ManagedReleaseTransactionResult repair = await _transaction.ApplyAsync(
                    candidate.CandidateRoot, ManagedReleaseChangeKind.Repair, cancellationToken)
                    .ConfigureAwait(false);
                if (repair is ManagedReleaseTransactionResult.Applied repairApplied)
                {
                    return new ManagedReleaseMaintenanceResult.Changed(
                        ManagedReleaseChangeKind.Repair, repairApplied.Installation);
                }

                updateRejected = (ManagedReleaseTransactionResult.Rejected)repair;
            }
        }

        return new ManagedReleaseMaintenanceResult.ChangeRejected(
            updateRejected.Issue, updateRejected.IntegrityIssue);
    }

    private static bool TryUseCurrent(
        ManagedInstallationResolution resolution,
        ulong selectedSequence,
        [NotNullWhen(true)] out ManagedInstallation? installation)
    {
        installation = (resolution as ManagedInstallationResolution.Resolved)?.Installation;
        return installation is not null && installation.Release.Sequence >= selectedSequence;
    }
}
