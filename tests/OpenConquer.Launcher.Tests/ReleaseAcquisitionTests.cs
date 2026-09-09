using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using OpenConquer.Launcher.Installation;
using OpenConquer.Launcher.Updates;
using OpenConquer.Product.Tool;

namespace OpenConquer.Launcher.Tests;

public sealed class ReleaseAcquisitionTests
{
    [Fact]
    public void CatalogSelectsNewestCompatibleReleaseForCurrentRuntime()
    {
        using Fixture fixture = new();
        ReleaseFiles old = fixture.CreateRelease(1, "old", "old.zip");
        ReleaseFiles current = fixture.CreateRelease(2, "current", "current.zip");
        string otherRuntime = fixture.Runtime == "win-x64" ? "linux-x64" : "win-x64";
        ReleaseFiles other = fixture.CreateRelease(10, "other", "other.zip",
            runtime: otherRuntime);
        CatalogFiles catalogFiles = fixture.CreateCatalog(old, other, current);

        ReleaseCatalogResult result = new ReleaseCatalog(fixture.TrustedKeys, fixture.Runtime)
            .Read(catalogFiles.CatalogBytes, catalogFiles.SignatureBytes);

        ReleaseCatalogResult.Selected selected =
            Assert.IsType<ReleaseCatalogResult.Selected>(result);
        Assert.Equal(2UL, selected.Release.ReleaseSequence);
        Assert.Equal(current.PackageUri, selected.Release.PackageUri);
    }

    [Fact]
    public void CatalogReportsWhenNewestPlatformReleaseRequiresLauncherUpdate()
    {
        using Fixture fixture = new();
        ReleaseFiles compatible = fixture.CreateRelease(2, "compatible", "compatible.zip");
        ReleaseFiles futureLauncher = fixture.CreateRelease(3, "future", "future.zip",
            minimumLauncherVersion: ManagedReleaseManifest.CurrentLauncherVersion + 1);
        CatalogFiles catalog = fixture.CreateCatalog(compatible, futureLauncher);

        ReleaseCatalogResult.Rejected rejected = Assert.IsType<ReleaseCatalogResult.Rejected>(
            new ReleaseCatalog(fixture.TrustedKeys, fixture.Runtime)
                .Read(catalog.CatalogBytes, catalog.SignatureBytes));

        Assert.Equal(ReleaseCatalogIssue.LauncherUpdateRequired, rejected.Issue);
    }

    [Fact]
    public void CatalogReportsAuthenticatedFutureSchemaAsUnsupported()
    {
        using Fixture fixture = new();
        byte[] catalog = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = ReleaseCatalog.CurrentSchemaVersion + 1,
            productId = ReleaseCatalog.ExpectedProductId,
            expiresUtc = DateTimeOffset.UtcNow.AddDays(7).ToString(
                "yyyy-MM-dd'T'HH:mm:ss'Z'",
                System.Globalization.CultureInfo.InvariantCulture),
            releases = Array.Empty<object>(),
            futureField = true,
        });

        ReleaseCatalogResult.Rejected rejected =
            Assert.IsType<ReleaseCatalogResult.Rejected>(
                new ReleaseCatalog(fixture.TrustedKeys, fixture.Runtime)
                    .Read(catalog, fixture.SignEnvelope(catalog)));

        Assert.Equal(ReleaseCatalogIssue.Unsupported, rejected.Issue);
    }

    [Fact]
    public void CatalogLocationRejectsOneResourceForBothRepresentations()
    {
        Uri uri = new("https://releases.example.test/openconquer.catalog.json");

        Assert.Throws<ArgumentException>(() => new ReleaseCatalogLocation(uri, uri));
    }

    [Fact]
    public async Task CatalogClientDoesNotContactNetworkWithoutReleaseAuthority()
    {
        Uri catalogUri = new("https://releases.example.test/catalog.json");
        Uri signatureUri = new("https://releases.example.test/catalog.sig");
        using StaticHttpHandler handler = new();
        using ReleaseHttpTransport transport = new(handler);
        TrustedReleaseKeys unconfigured = TrustedReleaseKeys.LoadEmbedded(
            typeof(ReleaseAcquisitionTests).Assembly);
        ReleaseCatalogClient client = new(new ReleaseCatalogLocation(catalogUri,
            signatureUri), transport, new ReleaseCatalog(unconfigured, "linux-x64"));

        ReleaseCatalogFetchResult.Rejected rejected =
            Assert.IsType<ReleaseCatalogFetchResult.Rejected>(
                await client.FetchLatestAsync(CancellationToken.None));

        Assert.Equal(ReleaseCatalogIssue.AuthorityUnavailable, rejected.CatalogIssue);
        Assert.Null(rejected.TransferIssue);
        Assert.Equal(0, handler.RequestCount(catalogUri));
        Assert.Equal(0, handler.RequestCount(signatureUri));
    }

    [Fact]
    public void CatalogRejectsTamperingAndSignedAmbiguity()
    {
        using Fixture fixture = new();
        ReleaseFiles release = fixture.CreateRelease(2, "current", "current.zip");
        CatalogFiles valid = fixture.CreateCatalog(release);
        byte[] tampered = valid.CatalogBytes.ToArray();
        tampered[^2] ^= 1;

        ReleaseCatalogResult.Rejected signatureRejected =
            Assert.IsType<ReleaseCatalogResult.Rejected>(
                new ReleaseCatalog(fixture.TrustedKeys, fixture.Runtime)
                    .Read(tampered, valid.SignatureBytes));
        Assert.Equal(ReleaseCatalogIssue.SignatureInvalid, signatureRejected.Issue);

        string duplicateJson = $$"""
            {
              "schemaVersion": 1,
              "schemaVersion": 1,
              "productId": "OpenConquer",
              "expiresUtc": "2026-09-15T12:00:00Z",
              "releases": []
            }
            """;
        byte[] duplicateBytes = System.Text.Encoding.UTF8.GetBytes(duplicateJson);
        ReleaseCatalogResult.Rejected invalid = Assert.IsType<ReleaseCatalogResult.Rejected>(
            new ReleaseCatalog(fixture.TrustedKeys, fixture.Runtime).Read(duplicateBytes,
                fixture.SignEnvelope(duplicateBytes)));
        Assert.Equal(ReleaseCatalogIssue.Invalid, invalid.Issue);
    }

    [Fact]
    public void CatalogRejectsExpiredAndExcessivelyLongLivedMetadata()
    {
        using Fixture fixture = new();
        ReleaseFiles release = fixture.CreateRelease(3, "current", "current.zip");
        CatalogFiles catalog = fixture.CreateCatalog(release);

        ReleaseCatalogResult.Rejected expired = Assert.IsType<ReleaseCatalogResult.Rejected>(
            new ReleaseCatalog(fixture.TrustedKeys, fixture.Runtime,
                new FixedTimeProvider(catalog.ExpiresAt.AddSeconds(1)))
                .Read(catalog.CatalogBytes, catalog.SignatureBytes));
        Assert.Equal(ReleaseCatalogIssue.Expired, expired.Issue);

        ReleaseCatalogResult.Rejected excessive =
            Assert.IsType<ReleaseCatalogResult.Rejected>(
                new ReleaseCatalog(fixture.TrustedKeys, fixture.Runtime,
                    new FixedTimeProvider(catalog.ExpiresAt -
                        ReleaseCatalog.MaximumRemainingLifetime - TimeSpan.FromSeconds(1)))
                    .Read(catalog.CatalogBytes, catalog.SignatureBytes));
        Assert.Equal(ReleaseCatalogIssue.Invalid, excessive.Issue);
    }

    [Theory]
    [InlineData("http://releases.example.test/package.zip")]
    [InlineData("https://user:password@releases.example.test/package.zip")]
    [InlineData("https://releases.example.test/package.zip?token=secret")]
    [InlineData("https://releases.example.test/package.zip#fragment")]
    [InlineData("https:\\releases.example.test\\package.zip")]
    public void UriPolicyRejectsUnsafeReleaseLocations(string value)
    {
        Assert.False(ReleaseUriPolicy.TryParseHttps(value, out _));
    }

    [Fact]
    public async Task TransportDownloadsExactPackageAndRejectsWrongDigest()
    {
        byte[] package = [0, 1, 2, 3, 255];
        Uri uri = new("https://releases.example.test/package.zip");
        using StaticHttpHandler handler = new();
        handler.AddBytes(uri, package);
        using ReleaseHttpTransport transport = new(handler);
        using TemporaryDirectory temporary = new("openconquer-transfer-tests");
        string destination = Path.Combine(temporary.RootPath, "package.zip");

        ReleaseTransferResult<string> downloaded = await transport.DownloadFileAsync(uri,
            destination, package.Length, SHA256.HashData(package), CancellationToken.None);

        Assert.IsType<ReleaseTransferResult<string>.Downloaded>(downloaded);
        Assert.Equal(package, File.ReadAllBytes(destination));

        string rejectedDestination = Path.Combine(temporary.RootPath, "rejected.zip");
        ReleaseTransferResult<string>.Rejected rejected =
            Assert.IsType<ReleaseTransferResult<string>.Rejected>(
                await transport.DownloadFileAsync(uri, rejectedDestination, package.Length,
                    new byte[SHA256.HashSizeInBytes], CancellationToken.None));
        Assert.Equal(ReleaseTransferIssue.IntegrityFailure, rejected.Issue);
        Assert.False(File.Exists(rejectedDestination));
    }

    [Fact]
    public async Task TransportRejectsRedirectAndOversizeWithoutReplacingDestination()
    {
        Uri catalogUri = new("https://releases.example.test/catalog.json");
        using StaticHttpHandler handler = new();
        handler.Add(catalogUri, request => new HttpResponseMessage(HttpStatusCode.Redirect)
        {
            RequestMessage = request,
            Headers = { Location = new Uri("https://elsewhere.example/catalog.json") },
        });
        Uri packageUri = new("https://releases.example.test/package.zip");
        handler.AddBytes(packageUri, [1, 2, 3, 4, 5]);
        using ReleaseHttpTransport transport = new(handler);

        ReleaseTransferResult<byte[]>.Rejected redirect =
            Assert.IsType<ReleaseTransferResult<byte[]>.Rejected>(
                await transport.DownloadBytesAsync(catalogUri, 1024,
                    CancellationToken.None));
        Assert.Equal(ReleaseTransferIssue.InvalidResponse, redirect.Issue);
        Assert.Equal(1, handler.RequestCount(catalogUri));

        ReleaseTransferResult<byte[]>.Rejected oversize =
            Assert.IsType<ReleaseTransferResult<byte[]>.Rejected>(
                await transport.DownloadBytesAsync(packageUri, 4,
                    CancellationToken.None));
        Assert.Equal(ReleaseTransferIssue.ContentTooLarge, oversize.Issue);

        using TemporaryDirectory temporary = new("openconquer-existing-download-tests");
        string existing = Path.Combine(temporary.RootPath, "existing.zip");
        File.WriteAllText(existing, "preserve");
        ReleaseTransferResult<string>.Rejected collision =
            Assert.IsType<ReleaseTransferResult<string>.Rejected>(
                await transport.DownloadFileAsync(packageUri, existing, 5,
                    SHA256.HashData([1, 2, 3, 4, 5]), CancellationToken.None));
        Assert.Equal(ReleaseTransferIssue.FileSystemFailure, collision.Issue);
        Assert.Equal("preserve", File.ReadAllText(existing));
    }

    [Fact]
    public async Task TransportBoundsWholeMetadataOperation()
    {
        using ReleaseHttpTransport transport = new(new HangingHttpHandler(),
            networkIdleTimeout: TimeSpan.FromSeconds(1),
            metadataOperationTimeout: TimeSpan.FromMilliseconds(50),
            packageOperationTimeout: TimeSpan.FromSeconds(1));

        ReleaseTransferResult<byte[]>.Rejected rejected =
            Assert.IsType<ReleaseTransferResult<byte[]>.Rejected>(
                await transport.DownloadBytesAsync(
                    new Uri("https://releases.example.test/catalog.json"), 1024,
                    CancellationToken.None));

        Assert.Equal(ReleaseTransferIssue.Unavailable, rejected.Issue);
    }

    [Fact]
    public async Task ExtractorProducesExactCandidateAndRejectsMetadataMismatch()
    {
        using Fixture fixture = new();
        ReleaseFiles release = fixture.CreateRelease(7, "seven", "seven.zip");
        CatalogFiles catalog = fixture.CreateCatalog(release);
        ReleaseCatalogEntry entry = fixture.ReadSelected(catalog);
        using TemporaryDirectory extraction = new("openconquer-extraction-tests");
        string candidate = Path.Combine(extraction.RootPath, "candidate");

        ReleasePackageExtractionResult result = await ReleasePackageExtractor.ExtractAsync(
            release.PackagePath, candidate, entry, CancellationToken.None);

        Assert.IsType<ReleasePackageExtractionResult.Extracted>(result);
        Assert.Equal("seven", File.ReadAllText(Path.Combine(candidate, "client",
            ReleaseTargetRuntime.ClientExecutable(fixture.Runtime))));
        Assert.True(ManagedReleaseDirectory.HasExpectedShape(candidate));
        if (!OperatingSystem.IsWindows())
        {
            UnixFileMode mode = File.GetUnixFileMode(Path.Combine(candidate, "client",
                ReleaseTargetRuntime.ClientExecutable(fixture.Runtime)));
            Assert.True((mode & UnixFileMode.UserExecute) != 0);
        }

        ReleaseCatalogEntry mismatch = entry with
        {
            ReleaseSequence = 8
        };
        ReleasePackageExtractionResult.Rejected rejected =
            Assert.IsType<ReleasePackageExtractionResult.Rejected>(
                await ReleasePackageExtractor.ExtractAsync(release.PackagePath,
                    Path.Combine(extraction.RootPath, "mismatch"), mismatch,
                    CancellationToken.None));
        Assert.Equal(ReleasePackageIssue.MetadataMismatch, rejected.Issue);
    }

    [Fact]
    public async Task ExtractorRejectsTraversalAndUnexpectedEntriesWithoutEscaping()
    {
        using Fixture fixture = new();
        ReleaseFiles release = fixture.CreateRelease(8, "eight", "eight.zip");
        string maliciousPackage = Path.Combine(fixture.RootPath, "malicious.zip");
        File.Copy(release.PackagePath, maliciousPackage);
        using (ZipArchive archive = ZipFile.Open(maliciousPackage, ZipArchiveMode.Update))
        {
            using StreamWriter writer = new(archive.CreateEntry("../escaped").Open());
            writer.Write("escape");
        }

        ReleaseCatalogEntry entry = Fixture.EntryForPackage(release, maliciousPackage);
        using TemporaryDirectory extraction = new("openconquer-traversal-tests");
        ReleasePackageExtractionResult.Rejected rejected =
            Assert.IsType<ReleasePackageExtractionResult.Rejected>(
                await ReleasePackageExtractor.ExtractAsync(maliciousPackage,
                    Path.Combine(extraction.RootPath, "candidate"), entry,
                    CancellationToken.None));

        Assert.Equal(ReleasePackageIssue.InvalidArchive, rejected.Issue);
        Assert.False(File.Exists(Path.Combine(extraction.RootPath, "escaped")));
    }

    [Fact]
    public async Task WorkspaceSerializesAcquisitionAndPreservesExistingLock()
    {
        using Fixture fixture = new();
        ReleaseFiles release = fixture.CreateRelease(9, "nine", "nine.zip");
        ReleaseCatalogEntry entry = fixture.ReadSelected(fixture.CreateCatalog(release));
        using StaticHttpHandler handler = new();
        handler.AddBytes(release.PackageUri, File.ReadAllBytes(release.PackagePath));
        using ReleaseHttpTransport transport = new(handler);
        string workspace = Path.Combine(fixture.RootPath, "workspace");
        string product = Path.Combine(fixture.RootPath, "product");
        Directory.CreateDirectory(workspace);
        MakePrivate(workspace);
        Directory.CreateDirectory(product);
        string lockPath = Path.Combine(workspace, ReleaseAcquisitionWorkspace.LockFileName);
        File.WriteAllText(lockPath, "preserve");
        using FileStream held = new(lockPath, FileMode.Open, FileAccess.ReadWrite,
            FileShare.None);
        ReleaseAcquisitionWorkspace acquisition = new(workspace, product, transport);

        ReleaseAcquisitionResult.Rejected rejected =
            Assert.IsType<ReleaseAcquisitionResult.Rejected>(
                await acquisition.AcquireAsync(entry, CancellationToken.None));

        Assert.Equal(ReleaseAcquisitionIssue.OperationAlreadyInProgress, rejected.Issue);
        held.Position = 0;
        using StreamReader reader = new(held, leaveOpen: true);
        Assert.Equal("preserve", reader.ReadToEnd());
        Assert.Equal(0, handler.RequestCount(release.PackageUri));
    }

    [Fact]
    public async Task WorkspaceRemovesOwnedStaleAttemptAndLeaseCleansCurrentAttempt()
    {
        using Fixture fixture = new();
        ReleaseFiles release = fixture.CreateRelease(10, "ten", "ten.zip");
        ReleaseCatalogEntry entry = fixture.ReadSelected(fixture.CreateCatalog(release));
        using StaticHttpHandler handler = new();
        handler.AddBytes(release.PackageUri, File.ReadAllBytes(release.PackagePath));
        using ReleaseHttpTransport transport = new(handler);
        string workspace = Path.Combine(fixture.RootPath, "workspace");
        string product = Path.Combine(fixture.RootPath, "product");
        Directory.CreateDirectory(workspace);
        MakePrivate(workspace);
        Directory.CreateDirectory(product);
        string stale = Path.Combine(workspace,
            ".openconquer-acquire-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        Directory.CreateDirectory(stale);
        File.WriteAllText(Path.Combine(stale, "partial"), "partial");
        string? linkedTarget = null;
        if (!OperatingSystem.IsWindows())
        {
            linkedTarget = Path.Combine(fixture.RootPath, "stale-link-target");
            Directory.CreateDirectory(linkedTarget);
            File.WriteAllText(Path.Combine(linkedTarget, "preserve"), "preserve");
            Directory.CreateSymbolicLink(Path.Combine(stale, "linked"), linkedTarget);
        }

        ReleaseAcquisitionResult.Acquired acquired =
            Assert.IsType<ReleaseAcquisitionResult.Acquired>(
                await new ReleaseAcquisitionWorkspace(workspace, product, transport)
                    .AcquireAsync(entry, CancellationToken.None));
        string attemptRoot = Directory.GetParent(acquired.Candidate.CandidateRoot)!.FullName;
        Assert.False(Directory.Exists(stale));
        Assert.True(Directory.Exists(acquired.Candidate.CandidateRoot));
        if (linkedTarget is not null)
        {
            Assert.Equal("preserve", File.ReadAllText(Path.Combine(linkedTarget, "preserve")));
        }

        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite,
                File.GetUnixFileMode(Path.Combine(workspace,
                    ReleaseAcquisitionWorkspace.LockFileName)));
        }

        acquired.Candidate.Dispose();

        Assert.False(Directory.Exists(attemptRoot));
    }

    [Fact]
    public void CandidateLeaseRejectsUnownedCleanupTarget()
    {
        using TemporaryDirectory temporary = new("openconquer-candidate-lease-tests");
        string unownedAttempt = Path.Combine(temporary.RootPath, "unowned");
        Directory.CreateDirectory(unownedAttempt);
        string candidate = Path.Combine(unownedAttempt,
            ReleaseAcquisitionWorkspace.CandidateDirectoryName);
        Directory.CreateDirectory(candidate);
        string lockPath = Path.Combine(temporary.RootPath, "lock");
        using FileStream workspaceLock = new(lockPath, FileMode.CreateNew,
            FileAccess.ReadWrite, FileShare.None);

        Assert.Throws<ArgumentException>(() =>
            new ReleaseCandidateLease(candidate, unownedAttempt, workspaceLock));
        Assert.True(Directory.Exists(unownedAttempt));
    }

    [Fact]
    public async Task WorkspaceRejectsLinkedOrOverpermissiveRootWithoutChangingTarget()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using Fixture fixture = new();
        ReleaseFiles release = fixture.CreateRelease(10, "ten", "ten.zip");
        ReleaseCatalogEntry entry = fixture.ReadSelected(fixture.CreateCatalog(release));
        using StaticHttpHandler handler = new();
        using ReleaseHttpTransport transport = new(handler);
        string product = Path.Combine(fixture.RootPath, "product");
        Directory.CreateDirectory(product);
        string target = Path.Combine(fixture.RootPath, "target");
        Directory.CreateDirectory(target);
        UnixFileMode originalMode = UnixFileMode.UserRead | UnixFileMode.UserWrite |
            UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.OtherRead;
        File.SetUnixFileMode(target, originalMode);
        string linkedWorkspace = Path.Combine(fixture.RootPath, "linked-workspace");
        Directory.CreateSymbolicLink(linkedWorkspace, target);

        ReleaseAcquisitionResult.Rejected linked =
            Assert.IsType<ReleaseAcquisitionResult.Rejected>(
                await new ReleaseAcquisitionWorkspace(linkedWorkspace, product, transport)
                    .AcquireAsync(entry, CancellationToken.None));
        Assert.Equal(ReleaseAcquisitionIssue.UnsafeWorkspace, linked.Issue);
        Assert.Equal(originalMode, File.GetUnixFileMode(target));

        string broadWorkspace = Path.Combine(fixture.RootPath, "broad-workspace");
        Directory.CreateDirectory(broadWorkspace);
        File.SetUnixFileMode(broadWorkspace, originalMode);
        ReleaseAcquisitionResult.Rejected broad =
            Assert.IsType<ReleaseAcquisitionResult.Rejected>(
                await new ReleaseAcquisitionWorkspace(broadWorkspace, product, transport)
                    .AcquireAsync(entry, CancellationToken.None));
        Assert.Equal(ReleaseAcquisitionIssue.UnsafeWorkspace, broad.Issue);
        Assert.Equal(0, handler.RequestCount(release.PackageUri));
    }

    [Fact]
    public async Task WorkspaceRejectsProductOverlapBeforeCreatingAcquisitionFiles()
    {
        using Fixture fixture = new();
        ReleaseFiles release = fixture.CreateRelease(11, "eleven", "eleven.zip");
        ReleaseCatalogEntry entry = fixture.ReadSelected(fixture.CreateCatalog(release));
        using StaticHttpHandler handler = new();
        using ReleaseHttpTransport transport = new(handler);
        string product = Path.Combine(fixture.RootPath, "product");
        Directory.CreateDirectory(product);
        string workspace = Path.Combine(product, "acquisition");

        ReleaseAcquisitionResult.Rejected rejected =
            Assert.IsType<ReleaseAcquisitionResult.Rejected>(
                await new ReleaseAcquisitionWorkspace(workspace, product, transport)
                    .AcquireAsync(entry, CancellationToken.None));

        Assert.Equal(ReleaseAcquisitionIssue.UnsafeWorkspace, rejected.Issue);
        Assert.False(Directory.Exists(workspace));
    }

    [Fact]
    public async Task MaintenanceUpdatesThroughAuthenticatedCatalogAndPackage()
    {
        using Fixture fixture = new();
        ReleaseFiles initial = fixture.CreateRelease(1, "one", "one.zip");
        string productRoot = fixture.StageInitial(initial);
        ReleaseFiles update = fixture.CreateRelease(2, "two", "two.zip");
        CatalogFiles catalog = fixture.CreateCatalog(update);
        using StaticHttpHandler handler = Fixture.CreateHandler(catalog, update);
        using ReleaseHttpTransport transport = new(handler);
        ManagedReleaseMaintenance maintenance = fixture.CreateMaintenance(productRoot,
            catalog, transport);

        ManagedReleaseMaintenanceResult.Changed changed =
            Assert.IsType<ManagedReleaseMaintenanceResult.Changed>(
                await maintenance.RunAsync(CancellationToken.None));

        Assert.Equal(ManagedReleaseChangeKind.Update, changed.Kind);
        Assert.Equal(2UL, changed.Installation.Release.Sequence);
        Assert.Equal("two", File.ReadAllText(changed.Installation.ClientExecutablePath));
        Assert.Equal(2UL, (await new ManagedInstallationResolver(productRoot,
            fixture.TrustedKeys, fixture.Runtime).ResolveAsync(CancellationToken.None) as
            ManagedInstallationResolution.Resolved)!.Installation.Release.Sequence);
    }

    [Fact]
    public async Task MaintenanceRepairsSameReleaseAndSkipsOlderCatalogForHealthyClient()
    {
        using Fixture fixture = new();
        ReleaseFiles current = fixture.CreateRelease(11, "healthy", "current.zip");
        string productRoot = fixture.StageInitial(current);
        ManagedInstallation installation = ((ManagedInstallationResolution.Resolved)
            await new ManagedInstallationResolver(productRoot, fixture.TrustedKeys,
                fixture.Runtime).ResolveAsync(CancellationToken.None)).Installation;
        File.AppendAllText(installation.ClientExecutablePath, "-damaged");
        CatalogFiles catalog = fixture.CreateCatalog(current);
        using StaticHttpHandler handler = Fixture.CreateHandler(catalog, current);
        using ReleaseHttpTransport transport = new(handler);

        ManagedReleaseMaintenanceResult.Changed repaired =
            Assert.IsType<ManagedReleaseMaintenanceResult.Changed>(
                await fixture.CreateMaintenance(productRoot, catalog, transport)
                    .RunAsync(CancellationToken.None));

        Assert.Equal(ManagedReleaseChangeKind.Repair, repaired.Kind);
        Assert.Equal("healthy", File.ReadAllText(repaired.Installation.ClientExecutablePath));

        ReleaseFiles older = fixture.CreateRelease(10, "older", "older.zip");
        CatalogFiles olderCatalog = fixture.CreateCatalog(older);
        using StaticHttpHandler olderHandler = Fixture.CreateHandler(olderCatalog, older);
        using ReleaseHttpTransport olderTransport = new(olderHandler);
        ManagedReleaseMaintenanceResult.UpToDate upToDate =
            Assert.IsType<ManagedReleaseMaintenanceResult.UpToDate>(
                await fixture.CreateMaintenance(productRoot, olderCatalog, olderTransport)
                    .RunAsync(CancellationToken.None));
        Assert.Equal(11UL, upToDate.Installation.Release.Sequence);
        Assert.Equal(0, olderHandler.RequestCount(older.PackageUri));
    }

    [Fact]
    public async Task MaintenanceReturnsPreciseCatalogFailureWithoutChangingInstallation()
    {
        using Fixture fixture = new();
        ReleaseFiles current = fixture.CreateRelease(12, "current", "current.zip");
        string productRoot = fixture.StageInitial(current);
        CatalogFiles catalog = fixture.CreateCatalog(current);
        using StaticHttpHandler handler = new();
        handler.Add(catalog.CatalogUri, request => new HttpResponseMessage(
            HttpStatusCode.ServiceUnavailable)
        {
            RequestMessage = request
        });
        using ReleaseHttpTransport transport = new(handler);

        ManagedReleaseMaintenanceResult.CatalogRejected rejected =
            Assert.IsType<ManagedReleaseMaintenanceResult.CatalogRejected>(
                await fixture.CreateMaintenance(productRoot, catalog, transport)
                    .RunAsync(CancellationToken.None));

        Assert.Equal(ReleaseTransferIssue.InvalidResponse, rejected.TransferIssue);
        Assert.Null(rejected.CatalogIssue);
        ManagedInstallationResolution.Resolved resolved =
            Assert.IsType<ManagedInstallationResolution.Resolved>(
                await new ManagedInstallationResolver(productRoot, fixture.TrustedKeys,
                    fixture.Runtime).ResolveAsync(CancellationToken.None));
        Assert.Equal(12UL, resolved.Installation.Release.Sequence);
    }

    [Fact]
    public async Task MaintenancePropagatesCancellationWithoutNetworkOrInstallationChange()
    {
        using Fixture fixture = new();
        ReleaseFiles current = fixture.CreateRelease(13, "current", "current.zip");
        string productRoot = fixture.StageInitial(current);
        CatalogFiles catalog = fixture.CreateCatalog(current);
        using StaticHttpHandler handler = Fixture.CreateHandler(catalog, current);
        using ReleaseHttpTransport transport = new(handler);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.CreateMaintenance(productRoot, catalog, transport)
                .RunAsync(cancellation.Token));

        Assert.Equal(0, handler.RequestCount(catalog.CatalogUri));
        ManagedInstallationResolution.Resolved resolved =
            Assert.IsType<ManagedInstallationResolution.Resolved>(
                await new ManagedInstallationResolver(productRoot, fixture.TrustedKeys,
                    fixture.Runtime).ResolveAsync(CancellationToken.None));
        Assert.Equal(13UL, resolved.Installation.Release.Sequence);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly TemporaryDirectory _temporary =
            new("openconquer-release-acquisition-tests");
        private readonly ECDsa _publisher =
            ECDsa.Create(ECCurve.NamedCurves.nistP256);
        private readonly string _publicKeyPath;

        public Fixture()
        {
            Runtime = ReleaseTargetRuntime.Current ?? "linux-x64";
            _publicKeyPath = Path.Combine(RootPath, "publisher.der");
            File.WriteAllBytes(_publicKeyPath, _publisher.ExportSubjectPublicKeyInfo());
            string trustPath = Path.Combine(RootPath, ProductReleaseTrust.FileName);
            ProductReleaseTrust.Create(new ReleaseTrustOptions([_publicKeyPath], trustPath));
            Assert.True(TrustedReleaseKeys.TryParse(File.ReadAllBytes(trustPath),
                out TrustedReleaseKeys? trustedKeys));
            TrustedKeys = trustedKeys!;
        }

        public string RootPath => _temporary.RootPath;

        public string Runtime
        {
            get;
        }

        public TrustedReleaseKeys TrustedKeys
        {
            get;
        }

        public ReleaseFiles CreateRelease(
            ulong sequence,
            string contents,
            string packageName,
            int minimumLauncherVersion = 1,
            string? runtime = null)
        {
            string selectedRuntime = runtime ?? Runtime;
            string releaseRoot = Path.Combine(RootPath,
                $"source-{sequence}-{Guid.NewGuid():N}");
            string clientRoot = Path.Combine(releaseRoot, "client");
            Directory.CreateDirectory(Path.Combine(clientRoot, "data"));
            File.WriteAllText(Path.Combine(clientRoot,
                ReleaseTargetRuntime.ClientExecutable(selectedRuntime)), contents);
            File.WriteAllBytes(Path.Combine(clientRoot, "data", "content.bin"), [1, 2, 3]);
            string manifestPath = Path.Combine(releaseRoot,
                ProductReleaseManifest.FileName);
            ProductReleaseManifest.Create(new ReleaseManifestOptions(clientRoot,
                selectedRuntime, $"1.0.{sequence}", sequence, minimumLauncherVersion,
                manifestPath));
            string rawSignaturePath = Path.Combine(releaseRoot, "release-raw.sig");
            File.WriteAllBytes(rawSignaturePath, _publisher.SignData(
                File.ReadAllBytes(manifestPath), HashAlgorithmName.SHA256,
                DSASignatureFormat.Rfc3279DerSequence));
            string signaturePath = Path.Combine(releaseRoot,
                ProductReleaseSignature.FileName);
            ProductReleaseSignature.Create(new ReleaseSignatureOptions(manifestPath,
                _publicKeyPath, rawSignaturePath, signaturePath));
            string packagePath = Path.Combine(RootPath, packageName);
            ProductReleasePackage.Create(new ReleasePackageOptions(clientRoot, manifestPath,
                signaturePath, _publicKeyPath, packagePath));
            Uri packageUri = new("https://releases.example.test/client/" + packageName);
            return new ReleaseFiles(clientRoot, manifestPath, signaturePath, packagePath,
                packageUri);
        }

        public CatalogFiles CreateCatalog(params ReleaseFiles[] releases)
        {
            string id = Guid.NewGuid().ToString("N");
            string catalogPath = Path.Combine(RootPath, $"catalog-{id}.json");
            DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddDays(7);
            ProductReleaseCatalog.Create(new ReleaseCatalogOptions(
                releases.Select(release => release.PackagePath).ToArray(),
                new Uri("https://releases.example.test/client/"),
                [_publicKeyPath], expiresAt, catalogPath));
            string rawSignaturePath = Path.Combine(RootPath, $"catalog-{id}.raw.sig");
            byte[] catalogBytes = File.ReadAllBytes(catalogPath);
            File.WriteAllBytes(rawSignaturePath, _publisher.SignData(catalogBytes,
                HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence));
            string signaturePath = Path.Combine(RootPath, $"catalog-{id}.sig");
            ProductReleaseSignature.Create(new CatalogSignatureOptions(catalogPath,
                _publicKeyPath, rawSignaturePath, signaturePath));
            return new CatalogFiles(
                new Uri($"https://releases.example.test/catalog/{id}.json"),
                new Uri($"https://releases.example.test/catalog/{id}.sig"),
                catalogBytes, File.ReadAllBytes(signaturePath), expiresAt);
        }

        public byte[] SignEnvelope(byte[] bytes)
        {
            byte[] signature = _publisher.SignData(bytes, HashAlgorithmName.SHA256,
                DSASignatureFormat.Rfc3279DerSequence);
            return JsonSerializer.SerializeToUtf8Bytes(new
            {
                schemaVersion = 1,
                keyId = TrustedReleaseKeys.GetKeyId(
                    _publisher.ExportSubjectPublicKeyInfo()),
                algorithm = ManagedReleaseSignature.Algorithm,
                signature = Convert.ToBase64String(signature),
            });
        }

        public ReleaseCatalogEntry ReadSelected(CatalogFiles catalog)
        {
            return Assert.IsType<ReleaseCatalogResult.Selected>(
                new ReleaseCatalog(TrustedKeys, Runtime).Read(catalog.CatalogBytes,
                    catalog.SignatureBytes)).Release;
        }

        public static ReleaseCatalogEntry EntryForPackage(
            ReleaseFiles release,
            string packagePath)
        {
            ProductReleaseManifest manifest = ProductReleaseManifest.Read(
                release.ManifestPath);
            byte[] bytes = File.ReadAllBytes(packagePath);
            return new ReleaseCatalogEntry(manifest.ReleaseSequence,
                manifest.ReleaseVersion, manifest.MinimumLauncherVersion,
                manifest.TargetRuntime, release.PackageUri, bytes.LongLength,
                SHA256.HashData(bytes));
        }

        public string StageInitial(ReleaseFiles release)
        {
            string launcher = Path.Combine(RootPath, $"launcher-{Guid.NewGuid():N}");
            Directory.CreateDirectory(launcher);
            File.WriteAllText(Path.Combine(launcher, "OpenConquer.Launcher"), "launcher");
            string product = Path.Combine(RootPath, $"product-{Guid.NewGuid():N}");
            ManagedProductStager.Stage(new ProductStageOptions(launcher,
                release.ClientRoot, release.ManifestPath, release.SignaturePath, product));
            return product;
        }

        public static StaticHttpHandler CreateHandler(
            CatalogFiles catalog,
            params ReleaseFiles[] releases)
        {
            StaticHttpHandler handler = new();
            handler.AddBytes(catalog.CatalogUri, catalog.CatalogBytes);
            handler.AddBytes(catalog.SignatureUri, catalog.SignatureBytes);
            foreach (ReleaseFiles release in releases)
            {
                handler.AddBytes(release.PackageUri, File.ReadAllBytes(release.PackagePath));
            }

            return handler;
        }

        public ManagedReleaseMaintenance CreateMaintenance(
            string productRoot,
            CatalogFiles catalog,
            ReleaseHttpTransport transport)
        {
            ManagedInstallationResolver resolver = new(productRoot, TrustedKeys, Runtime);
            return new ManagedReleaseMaintenance(resolver,
                new ReleaseCatalogClient(new ReleaseCatalogLocation(catalog.CatalogUri,
                    catalog.SignatureUri), transport,
                    new ReleaseCatalog(TrustedKeys, Runtime)),
                new ReleaseAcquisitionWorkspace(Path.Combine(RootPath,
                    $"workspace-{Guid.NewGuid():N}"), productRoot, transport),
                new ManagedReleaseTransaction(productRoot, TrustedKeys, Runtime));
        }

        public void Dispose()
        {
            _publisher.Dispose();
            _temporary.Dispose();
        }
    }

    private sealed record ReleaseFiles(
        string ClientRoot,
        string ManifestPath,
        string SignaturePath,
        string PackagePath,
        Uri PackageUri);

    private sealed record CatalogFiles(
        Uri CatalogUri,
        Uri SignatureUri,
        byte[] CatalogBytes,
        byte[] SignatureBytes,
        DateTimeOffset ExpiresAt);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static void MakePrivate(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite |
                UnixFileMode.UserExecute);
        }
    }

    private sealed class StaticHttpHandler : HttpMessageHandler
    {
        private readonly Dictionary<Uri, Func<HttpRequestMessage, HttpResponseMessage>>
            _responses = [];
        private readonly Dictionary<Uri, int> _counts = [];

        public void Add(
            Uri uri,
            Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responses.Add(uri, responseFactory);
        }

        public void AddBytes(Uri uri, byte[] bytes)
        {
            Add(uri, request => new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new ByteArrayContent(bytes),
            });
        }

        public int RequestCount(Uri uri) => _counts.GetValueOrDefault(uri);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Uri uri = request.RequestUri!;
            _counts[uri] = RequestCount(uri) + 1;
            if (!_responses.TryGetValue(uri, out var responseFactory))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    RequestMessage = request,
                });
            }

            return Task.FromResult(responseFactory(request));
        }
    }

    private sealed class HangingHttpHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("An infinite delay unexpectedly completed.");
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private readonly string _path;

        public TemporaryDirectory(string prefix)
        {
            _path = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_path);
        }

        public string RootPath => _path;

        public void Dispose()
        {
            try
            {
                Directory.Delete(_path, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
