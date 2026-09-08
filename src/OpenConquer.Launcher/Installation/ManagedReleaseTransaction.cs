namespace OpenConquer.Launcher.Installation;

/// <summary>
/// Enforces release policy and atomically changes the active generation after verified staging.
/// Acquisition and launcher self-update are deliberately outside this boundary.
/// </summary>
internal sealed class ManagedReleaseTransaction
{
    public const string LockFileName = ".openconquer.update.lock";

    private readonly ManagedReleaseStore _store;

    public ManagedReleaseTransaction(string productRoot, TrustedReleaseKeys trustedReleaseKeys, string? currentRuntime = null)
    {
        ArgumentNullException.ThrowIfNull(trustedReleaseKeys);
        if (string.IsNullOrWhiteSpace(productRoot) || !Path.IsPathFullyQualified(productRoot))
        {
            throw new ArgumentException("The product root must be a fully qualified path.", nameof(productRoot));
        }

        string normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(productRoot));
        ManagedReleaseVerifier verifier = new(trustedReleaseKeys, currentRuntime ?? ReleaseTargetRuntime.Current);
        _store = new ManagedReleaseStore(normalizedRoot, verifier, LockFileName);
    }

    public async Task<ManagedReleaseTransactionResult> ApplyAsync(string candidateReleasePath, ManagedReleaseChangeKind kind, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateReleasePath);
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (!Path.IsPathFullyQualified(candidateReleasePath))
        {
            throw new ArgumentException("The candidate release path must be fully qualified.", nameof(candidateReleasePath));
        }

        string candidateRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidateReleasePath));
        if (PathsOverlap(_store.ProductRoot, candidateRoot))
        {
            throw new ArgumentException("The candidate release must remain outside the managed product root.", nameof(candidateReleasePath));
        }

        StoredReleaseReadResult candidateResult = await _store.ReadCandidateAsync(candidateRoot, cancellationToken).ConfigureAwait(false);
        if (candidateResult is StoredReleaseReadResult.Rejected candidateRejected)
        {
            return Rejected(ManagedReleaseChangeIssue.CandidateRejected, candidateRejected.Issue);
        }

        AuthenticatedRelease candidate = ((StoredReleaseReadResult.Accepted)candidateResult).Release;

        try
        {
            using FileStream transactionLock = _store.AcquireLock();
            cancellationToken.ThrowIfCancellationRequested();

            ManifestReadResult descriptorResult = await ManagedInstallationManifest.ReadAsync(_store.DescriptorPath, cancellationToken).ConfigureAwait(false);
            if (descriptorResult is ManifestReadResult.Rejected descriptorRejected)
            {
                return RejectUnavailable(descriptorRejected.Issue);
            }

            ManagedInstallationManifest descriptor = ((ManifestReadResult.Accepted)descriptorResult).Manifest;
            ActiveReleaseLocation currentLocation = _store.GetActiveReleaseLocation(descriptor);
            StoredReleaseReadResult currentResult = await _store.ReadActiveMetadataAsync(currentLocation, cancellationToken).ConfigureAwait(false);
            if (currentResult is StoredReleaseReadResult.Rejected currentRejected)
            {
                return RejectUnavailable(currentRejected.Issue);
            }

            AuthenticatedRelease current = ((StoredReleaseReadResult.Accepted)currentResult).Release;
            string currentCanonicalId = ManagedReleaseId.Create(current.Manifest.ReleaseSequence, current.ManifestBytes);
            if (currentLocation.ReleaseId is not null && !ManagedReleaseId.Matches(currentLocation.ReleaseId, current.Manifest.ReleaseSequence, current.ManifestBytes))
            {
                return RejectUnavailable(ManagedInstallationIssue.ReleaseIdentityMismatch);
            }

            ManagedReleaseTransactionResult.Rejected? policyRejection = ValidatePolicy(kind, candidate, current, currentLocation.ReleaseId ?? currentCanonicalId);
            if (policyRejection is not null)
            {
                return policyRejection;
            }

            string releasesRoot = _store.EnsureReleasesRoot();
            List<string> createdGenerationPaths = [];

            try
            {
                string? fallbackReleaseId = kind == ManagedReleaseChangeKind.Update
                    ? await SelectUpdateFallbackAsync(descriptor, currentLocation, current, releasesRoot, createdGenerationPaths, cancellationToken).ConfigureAwait(false)
                    : await SelectExistingFallbackAsync(descriptor, cancellationToken).ConfigureAwait(false);

                string preferredCandidateId = kind == ManagedReleaseChangeKind.Repair
                    ? ManagedReleaseId.Create(candidate.Manifest.ReleaseSequence, candidate.ManifestBytes, Guid.NewGuid().ToString("N"))
                    : ManagedReleaseId.Create(candidate.Manifest.ReleaseSequence, candidate.ManifestBytes);

                ManagedReleaseGeneration installed = await _store.InstallGenerationAsync(candidate, releasesRoot, preferredCandidateId, cancellationToken).ConfigureAwait(false);
                if (installed.Created)
                {
                    createdGenerationPaths.Add(installed.RootPath);
                }

                cancellationToken.ThrowIfCancellationRequested();
                await ManagedInstallationManifest.WriteCurrentAsync(_store.DescriptorPath, installed.ReleaseId, fallbackReleaseId, cancellationToken).ConfigureAwait(false);

                createdGenerationPaths.Clear();
                return new ManagedReleaseTransactionResult.Applied(_store.CreateInstallation(installed.ReleaseId, fallbackReleaseId, installed.RootPath, installed.Release));
            }
            finally
            {
                foreach (string createdPath in createdGenerationPaths)
                {
                    ManagedReleaseStore.TryDeleteTree(createdPath);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ManagedReleaseLockUnavailableException)
        {
            return Rejected(ManagedReleaseChangeIssue.OperationAlreadyInProgress);
        }
        catch (StagedReleaseRejectedException exception)
        {
            return Rejected(ManagedReleaseChangeIssue.CandidateRejected, exception.Issue);
        }
        catch (UnauthorizedAccessException)
        {
            return Rejected(ManagedReleaseChangeIssue.AccessDenied);
        }
        catch (LinkedInstallationPathException)
        {
            return Rejected(ManagedReleaseChangeIssue.LinkedPath);
        }
        catch (IOException)
        {
            return Rejected(ManagedReleaseChangeIssue.FileSystemFailure);
        }
    }

    public async Task<ManagedReleaseTransactionResult> RollbackAsync(CancellationToken cancellationToken)
    {
        try
        {
            using FileStream transactionLock = _store.AcquireLock();
            cancellationToken.ThrowIfCancellationRequested();

            ManifestReadResult descriptorResult = await ManagedInstallationManifest.ReadAsync(_store.DescriptorPath, cancellationToken).ConfigureAwait(false);
            if (descriptorResult is not ManifestReadResult.Accepted accepted || accepted.Manifest.SchemaVersion < 2 || accepted.Manifest.FallbackRelease is null)
            {
                ManagedInstallationIssue? detail = descriptorResult is ManifestReadResult.Rejected rejected
                    ? rejected.Issue
                    : null;
                return Rejected(ManagedReleaseChangeIssue.FallbackUnavailable, detail);
            }

            ManagedInstallationManifest descriptor = accepted.Manifest;
            string fallbackId = descriptor.FallbackRelease;
            StoredReleaseReadResult fallbackResult = await _store.ReadGenerationAsync(fallbackId, cancellationToken).ConfigureAwait(false);
            if (fallbackResult is StoredReleaseReadResult.Rejected fallbackRejected)
            {
                return Rejected(ManagedReleaseChangeIssue.FallbackUnavailable, fallbackRejected.Issue);
            }

            AuthenticatedRelease fallback = ((StoredReleaseReadResult.Accepted)fallbackResult).Release;
            string? nextFallback = await _store.IsHealthyGenerationAsync(descriptor.ActiveRelease!, cancellationToken).ConfigureAwait(false)
                ? descriptor.ActiveRelease
                : null;

            cancellationToken.ThrowIfCancellationRequested();
            await ManagedInstallationManifest.WriteCurrentAsync(_store.DescriptorPath, fallbackId, nextFallback, cancellationToken).ConfigureAwait(false);

            string fallbackRoot = _store.GetGenerationRoot(fallbackId);
            return new ManagedReleaseTransactionResult.Applied(_store.CreateInstallation(fallbackId, nextFallback, fallbackRoot, fallback));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ManagedReleaseLockUnavailableException)
        {
            return Rejected(ManagedReleaseChangeIssue.OperationAlreadyInProgress);
        }
        catch (UnauthorizedAccessException)
        {
            return Rejected(ManagedReleaseChangeIssue.AccessDenied);
        }
        catch (LinkedInstallationPathException)
        {
            return Rejected(ManagedReleaseChangeIssue.LinkedPath);
        }
        catch (IOException)
        {
            return Rejected(ManagedReleaseChangeIssue.FileSystemFailure);
        }
    }

    private static ManagedReleaseTransactionResult.Rejected? ValidatePolicy(ManagedReleaseChangeKind kind, AuthenticatedRelease candidate, AuthenticatedRelease current, string currentReleaseId)
    {
        if (candidate.Manifest.ReleaseSequence < current.Manifest.ReleaseSequence)
        {
            return Rejected(ManagedReleaseChangeIssue.ReleaseRollbackBlocked);
        }

        if (kind == ManagedReleaseChangeKind.Update)
        {
            return candidate.Manifest.ReleaseSequence == current.Manifest.ReleaseSequence
                ? Rejected(ManagedReleaseChangeIssue.ReleaseNotNewer)
                : null;
        }

        return candidate.Manifest.ReleaseSequence != current.Manifest.ReleaseSequence || !ManagedReleaseId.Matches(currentReleaseId, candidate.Manifest.ReleaseSequence, candidate.ManifestBytes)
            ? Rejected(ManagedReleaseChangeIssue.RepairReleaseMismatch)
            : null;
    }

    private async Task<string?> SelectUpdateFallbackAsync(ManagedInstallationManifest descriptor, ActiveReleaseLocation currentLocation,
        AuthenticatedRelease current, string releasesRoot, List<string> createdGenerationPaths, CancellationToken cancellationToken)
    {
        if (await _store.VerifyClientAsync(current, cancellationToken).ConfigureAwait(false))
        {
            if (currentLocation.ReleaseId is not null)
            {
                return currentLocation.ReleaseId;
            }

            string releaseId = ManagedReleaseId.Create(current.Manifest.ReleaseSequence, current.ManifestBytes);
            ManagedReleaseGeneration migrated = await _store.InstallGenerationAsync(current, releasesRoot, releaseId, cancellationToken).ConfigureAwait(false);
            if (migrated.Created)
            {
                createdGenerationPaths.Add(migrated.RootPath);
            }

            return migrated.ReleaseId;
        }

        return await SelectExistingFallbackAsync(descriptor, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string?> SelectExistingFallbackAsync(ManagedInstallationManifest descriptor, CancellationToken cancellationToken)
    {
        if (descriptor.SchemaVersion < 2 || descriptor.FallbackRelease is null)
        {
            return null;
        }

        return await _store.IsHealthyGenerationAsync(descriptor.FallbackRelease, cancellationToken).ConfigureAwait(false)
            ? descriptor.FallbackRelease
            : null;
    }

    private static bool PathsOverlap(string left, string right) => IsSameOrChild(left, right) || IsSameOrChild(right, left);

    private static bool IsSameOrChild(string root, string candidate)
    {
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (string.Equals(root, candidate, comparison))
        {
            return true;
        }

        return candidate.StartsWith(root + Path.DirectorySeparatorChar, comparison);
    }

    private static ManagedReleaseTransactionResult.Rejected Rejected(ManagedReleaseChangeIssue issue, ManagedInstallationIssue? detail = null) => new(issue, detail);
    private static ManagedReleaseTransactionResult.Rejected RejectUnavailable(ManagedInstallationIssue issue) => Rejected(ManagedReleaseChangeIssue.InstallationUnavailable, issue);
}
