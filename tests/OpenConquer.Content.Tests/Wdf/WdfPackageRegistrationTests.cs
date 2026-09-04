using OpenConquer.Content.Wdf;

namespace OpenConquer.Content.Tests.Wdf;

public sealed class WdfPackageRegistrationTests
{
    /// <summary>
    /// Native <c>sub_100014F0</c> discards <c>WdfHandler_OpenFile</c>'s failure at
    /// <c>0x10001620</c>, so an absent declared package is recorded and tolerated.
    /// </summary>
    [Fact]
    public void Open_RecordsDeclaredPackagesThatAreNotPresent()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("ini/package.ini", "data.wdf c3.wdf\n");
        temporaryDirectory.WriteFile(
            "data.wdf",
            WdfTestArchiveBuilder.CreateSingleEntry("data/example.bin", [1])
        );

        PackagedClientContentSource source = PackagedClientContentSource.Open(
            temporaryDirectory.RootPath
        );

        Assert.Equal(
            [
                new WdfPackageRegistration(
                    "data.wdf",
                    "data",
                    WdfPackageRegistrationOutcome.Registered
                ),
                new WdfPackageRegistration(
                    "c3.wdf",
                    "c3",
                    WdfPackageRegistrationOutcome.FileNotFound
                ),
            ],
            source.PackageRegistrations
        );
    }

    /// <summary>
    /// Native registers the package object before attempting to open its WDF, and the caller
    /// discards the open result. An existing archive that cannot be parsed therefore behaves as an
    /// empty registered package rather than aborting initialization.
    /// </summary>
    [Fact]
    public void Open_WhenExistingArchiveIsInvalid_RecordsUnavailableAndContinues()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("ini/package.ini", "data.wdf c3.wdf\n");
        temporaryDirectory.WriteFile("data.wdf", "not a WDF archive");
        temporaryDirectory.WriteFile(
            "c3.wdf",
            WdfTestArchiveBuilder.CreateSingleEntry("c3/example.c3", [7, 8, 9])
        );

        PackagedClientContentSource source = PackagedClientContentSource.Open(
            temporaryDirectory.RootPath
        );

        Assert.Equal(
            [
                new WdfPackageRegistration(
                    "data.wdf",
                    "data",
                    WdfPackageRegistrationOutcome.ArchiveUnavailable
                ),
                new WdfPackageRegistration(
                    "c3.wdf",
                    "c3",
                    WdfPackageRegistrationOutcome.Registered
                ),
            ],
            source.PackageRegistrations
        );

        Assert.False(
            source.TryOpenRead(
                "data/example.bin",
                ContentLookupMode.PackageOnly,
                out Stream? unavailableStream
            )
        );

        Assert.Null(unavailableStream);

        Assert.Equal([7, 8, 9], ReadAll(source, "c3/example.c3", ContentLookupMode.PackageOnly));
    }

    /// <summary>
    /// Prefix-hash ownership precedes archive opening in native registration. Therefore an
    /// unusable first archive still prevents a later same-prefix declaration from replacing it.
    /// </summary>
    [Fact]
    public void Open_WhenUnavailableFirstDeclarationOwnsPrefix_RejectsLaterDuplicate()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("ini/package.ini", "data.wdf\ndata.dat\n");
        temporaryDirectory.WriteFile("data.wdf", "not a WDF archive");
        temporaryDirectory.WriteFile(
            "data.dat",
            WdfTestArchiveBuilder.CreateSingleEntry("data/example.bin", [9])
        );

        PackagedClientContentSource source = PackagedClientContentSource.Open(
            temporaryDirectory.RootPath
        );

        Assert.Equal(
            [
                new WdfPackageRegistration(
                    "data.wdf",
                    "data",
                    WdfPackageRegistrationOutcome.ArchiveUnavailable
                ),
                new WdfPackageRegistration(
                    "data.dat",
                    "data",
                    WdfPackageRegistrationOutcome.DuplicatePrefix
                ),
            ],
            source.PackageRegistrations
        );

        Assert.False(
            source.TryOpenRead(
                "data/example.bin",
                ContentLookupMode.PackageOnly,
                out Stream? stream
            )
        );

        Assert.Null(stream);
    }

    /// <summary>
    /// Package-path resolution is part of the native package-open attempt. Rejecting a linked
    /// archive is a modern filesystem-safety decision, but that expected availability failure must
    /// still preserve native non-fatal registration and first-wins routing-hash ownership.
    /// </summary>
    [Fact]
    public void Open_WhenPackageResolutionRejectsLinkedArchive_RecordsUnavailableAndKeepsPrefix()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("ini/package.ini", "data.wdf\ndata.dat\n");

        string targetPath = temporaryDirectory.WriteFile(
            "actual-data.wdf",
            WdfTestArchiveBuilder.CreateSingleEntry("data/example.bin", [1])
        );

        _ = File.CreateSymbolicLink(
            Path.Combine(temporaryDirectory.RootPath, "data.wdf"),
            targetPath
        );

        temporaryDirectory.WriteFile(
            "data.dat",
            WdfTestArchiveBuilder.CreateSingleEntry("data/example.bin", [9])
        );

        PackagedClientContentSource source = PackagedClientContentSource.Open(
            temporaryDirectory.RootPath
        );

        Assert.Equal(
            [
                new WdfPackageRegistration(
                    "data.wdf",
                    "data",
                    WdfPackageRegistrationOutcome.ArchiveUnavailable
                ),
                new WdfPackageRegistration(
                    "data.dat",
                    "data",
                    WdfPackageRegistrationOutcome.DuplicatePrefix
                ),
            ],
            source.PackageRegistrations
        );

        Assert.False(
            source.TryOpenRead(
                "data/example.bin",
                ContentLookupMode.PackageOnly,
                out Stream? stream
            )
        );

        Assert.Null(stream);
    }

    /// <summary>
    /// Native creates and registers the package object before opening its WDF. Therefore an absent
    /// first declaration still owns its routing hash, and a later declaration with the same prefix
    /// is a duplicate even when that later package exists.
    /// </summary>
    [Fact]
    public void Open_WhenMissingFirstDeclarationOwnsPrefix_RejectsLaterDuplicate()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("ini/package.ini", "data.wdf\ndata.dat\n");
        temporaryDirectory.WriteFile(
            "data.dat",
            WdfTestArchiveBuilder.CreateSingleEntry("data/example.bin", [9])
        );

        PackagedClientContentSource source = PackagedClientContentSource.Open(
            temporaryDirectory.RootPath
        );

        Assert.Equal(
            [
                new WdfPackageRegistration(
                    "data.wdf",
                    "data",
                    WdfPackageRegistrationOutcome.FileNotFound
                ),
                new WdfPackageRegistration(
                    "data.dat",
                    "data",
                    WdfPackageRegistrationOutcome.DuplicatePrefix
                ),
            ],
            source.PackageRegistrations
        );

        Assert.False(
            source.TryOpenRead(
                "data/example.bin",
                ContentLookupMode.PackageOnly,
                out Stream? stream
            )
        );

        Assert.Null(stream);
    }

    /// <summary>
    /// Native <c>TqPackagesOpen</c> returns at <c>0x10003DEF</c> when the routing hash is already
    /// registered, so the first declaration wins and the duplicate is discarded.
    /// </summary>
    [Fact]
    public void Open_WhenTwoDeclarationsSharePrefix_KeepsTheFirstRegistration()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("ini/package.ini", "data.wdf\ndata.dat\n");
        temporaryDirectory.WriteFile(
            "data.wdf",
            WdfTestArchiveBuilder.CreateSingleEntry("data/example.bin", [1, 2, 3])
        );
        temporaryDirectory.WriteFile(
            "data.dat",
            WdfTestArchiveBuilder.CreateSingleEntry("data/example.bin", [9])
        );

        PackagedClientContentSource source = PackagedClientContentSource.Open(
            temporaryDirectory.RootPath
        );

        Assert.Equal(
            [
                new WdfPackageRegistration(
                    "data.wdf",
                    "data",
                    WdfPackageRegistrationOutcome.Registered
                ),
                new WdfPackageRegistration(
                    "data.dat",
                    "data",
                    WdfPackageRegistrationOutcome.DuplicatePrefix
                ),
            ],
            source.PackageRegistrations
        );

        Assert.Equal([1, 2, 3], ReadAll(source, "data/example.bin", ContentLookupMode.PackageOnly));
    }

    /// <summary>
    /// Native package registration and lookup compare only the 32-bit prefix hash. Distinct prefix
    /// strings that collide therefore share one package-routing identity, with the first
    /// registration winning.
    /// </summary>
    [Fact]
    public void Open_WhenDistinctPrefixesHaveSameNativeHash_FirstRegistrationWins()
    {
        const string firstPrefix = "pkg1f6f";
        const string secondPrefix = "pkg809e";
        const uint expectedPrefixHash = 0xA207E2C0;

        Assert.Equal(expectedPrefixHash, WdfPathHash.Compute(firstPrefix));

        Assert.Equal(expectedPrefixHash, WdfPathHash.Compute(secondPrefix));

        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("ini/package.ini", $"{firstPrefix}.wdf\n{secondPrefix}.wdf\n");

        temporaryDirectory.WriteFile(
            $"{firstPrefix}.wdf",
            WdfTestArchiveBuilder.CreateSingleEntry($"{secondPrefix}/example.bin", [1])
        );

        temporaryDirectory.WriteFile(
            $"{secondPrefix}.wdf",
            WdfTestArchiveBuilder.CreateSingleEntry($"{secondPrefix}/example.bin", [9])
        );

        PackagedClientContentSource source = PackagedClientContentSource.Open(
            temporaryDirectory.RootPath
        );

        Assert.Equal(
            [
                new WdfPackageRegistration(
                    $"{firstPrefix}.wdf",
                    firstPrefix,
                    WdfPackageRegistrationOutcome.Registered
                ),
                new WdfPackageRegistration(
                    $"{secondPrefix}.wdf",
                    secondPrefix,
                    WdfPackageRegistrationOutcome.DuplicatePrefix
                ),
            ],
            source.PackageRegistrations
        );

        Assert.Equal(
            [1],
            ReadAll(source, $"{secondPrefix}/example.bin", ContentLookupMode.PackageOnly)
        );
    }

    /// <summary>
    /// Native strips from the <b>last</b> <c>'.'</c> (<c>strrchr</c> at <c>0x10003D86</c>), so a
    /// multi-dot declaration registers a prefix no ordinary first-segment virtual path can match.
    /// </summary>
    [Fact]
    public void Open_DerivesPrefixFromTheLastDot()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("ini/package.ini", "data.v2.wdf\n");
        temporaryDirectory.WriteFile(
            "data.v2.wdf",
            WdfTestArchiveBuilder.CreateSingleEntry("data/example.bin", [1])
        );

        PackagedClientContentSource source = PackagedClientContentSource.Open(
            temporaryDirectory.RootPath
        );

        Assert.Equal(
            [
                new WdfPackageRegistration(
                    "data.v2.wdf",
                    "data.v2",
                    WdfPackageRegistrationOutcome.Registered
                ),
            ],
            source.PackageRegistrations
        );

        Assert.False(source.TryOpenRead("data/example.bin", ContentLookupMode.PackageOnly, out _));
    }

    /// <summary>
    /// Native treats failure to open <c>ini/package.ini</c> as non-fatal and continues with zero
    /// packages. The modern content boundary additionally rejects linked files, so a linked
    /// declaration file maps to the same non-fatal package-unavailable behavior.
    /// </summary>
    [Fact]
    public void Open_WhenPackageDeclarationFileIsLinked_RegistersNothingAndStillServesLooseFiles()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        string declarationTarget = temporaryDirectory.WriteFile(
            "ini/package-target.ini",
            "data.wdf\n"
        );

        _ = File.CreateSymbolicLink(
            Path.Combine(temporaryDirectory.RootPath, "ini", "package.ini"),
            declarationTarget
        );

        temporaryDirectory.WriteFile(
            "data.wdf",
            WdfTestArchiveBuilder.CreateSingleEntry("data/example.bin", [1])
        );
        temporaryDirectory.WriteFile("data/example.bin", [4, 5]);

        PackagedClientContentSource source = PackagedClientContentSource.Open(
            temporaryDirectory.RootPath
        );

        Assert.Empty(source.PackageRegistrations);

        Assert.False(
            source.TryOpenRead(
                "data/example.bin",
                ContentLookupMode.PackageOnly,
                out Stream? packageStream
            )
        );

        Assert.Null(packageStream);

        Assert.Equal(
            [4, 5],
            ReadAll(source, "data/example.bin", ContentLookupMode.LooseThenPackage)
        );
    }

    /// <summary>
    /// The 64 KiB declaration-file ceiling is a modern resource-safety policy, not a native
    /// format limit. Because package registration itself is non-gating in retail, exceeding that
    /// ceiling safely disables package registration rather than aborting client startup.
    /// </summary>
    [Fact]
    public void Open_WhenPackageDeclarationExceedsSafetyLimit_RegistersNothingAndStillServesLooseFiles()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("ini/package.ini", new string('x', (64 * 1024) + 1));

        temporaryDirectory.WriteFile("data/example.bin", [4, 5]);

        PackagedClientContentSource source = PackagedClientContentSource.Open(
            temporaryDirectory.RootPath
        );

        Assert.Empty(source.PackageRegistrations);

        Assert.Equal(
            [4, 5],
            ReadAll(source, "data/example.bin", ContentLookupMode.LooseThenPackage)
        );
    }

    /// <summary>
    /// Native logs and continues with zero packages when the declaration file is absent
    /// (<c>0x1001A3B0</c>).
    /// </summary>
    [Fact]
    public void Open_WithoutPackageDeclarationFile_RegistersNothingAndStillServesLooseFiles()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("data/example.bin", [4, 5]);

        PackagedClientContentSource source = PackagedClientContentSource.Open(
            temporaryDirectory.RootPath
        );

        Assert.Empty(source.PackageRegistrations);

        Assert.Equal(
            [4, 5],
            ReadAll(source, "data/example.bin", ContentLookupMode.LooseThenPackage)
        );
    }

    /// <summary>
    /// Registration results are a completed startup snapshot. Callers must not be able to cast the
    /// advertised read-only view back to the mutable construction list and alter routing evidence.
    /// </summary>
    [Fact]
    public void Open_PublishesPackageRegistrationsAsAnImmutableSnapshot()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("ini/package.ini", "data.wdf\n");

        PackagedClientContentSource source = PackagedClientContentSource.Open(
            temporaryDirectory.RootPath
        );

        ICollection<WdfPackageRegistration> registrations = Assert.IsAssignableFrom<
            ICollection<WdfPackageRegistration>
        >(source.PackageRegistrations);

        Assert.True(registrations.IsReadOnly);

        Assert.Throws<NotSupportedException>(() =>
            registrations.Add(
                new WdfPackageRegistration("c3.wdf", "c3", WdfPackageRegistrationOutcome.Registered)
            )
        );
    }

    private static byte[] ReadAll(
        PackagedClientContentSource source,
        string contentPath,
        ContentLookupMode mode
    )
    {
        using Stream stream = source.OpenRequiredRead(contentPath, mode);

        using MemoryStream destination = new();

        stream.CopyTo(destination);

        return destination.ToArray();
    }
}
