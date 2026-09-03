using System.Buffers.Binary;
using OpenConquer.Content.Wdf;

namespace OpenConquer.Content.Tests;

public sealed class PackagedClientContentSourceTests
{
    [Fact]
    public void TryOpenRead_LooseThenPackage_FallsBackToPackageWhenLooseFileIsMissing()
    {
        using TemporaryContentDirectory temporaryDirectory = new();
        temporaryDirectory.WriteFile("ini/package.ini", "data.wdf\n");
        temporaryDirectory.WriteFile("data.wdf", CreateWdf("data/example.bin", [1, 2, 3]));

        PackagedClientContentSource source = PackagedClientContentSource.Open(temporaryDirectory.RootPath);

        Assert.Equal([1, 2, 3], ReadAll(source, "DATA/EXAMPLE.BIN", ContentLookupMode.LooseThenPackage));
    }

    [Fact]
    public void TryOpenRead_LooseThenPackage_PrefersLooseFileOverPackageEntry()
    {
        using TemporaryContentDirectory temporaryDirectory = new();
        temporaryDirectory.WriteFile("ini/package.ini", "data.wdf\n");
        temporaryDirectory.WriteFile("data.wdf", CreateWdf("data/example.bin", [1, 2, 3]));
        temporaryDirectory.WriteFile("data/example.bin", [9, 8, 7]);

        PackagedClientContentSource source = PackagedClientContentSource.Open(temporaryDirectory.RootPath);

        Assert.Equal([9, 8, 7], ReadAll(source, "data/example.bin", ContentLookupMode.LooseThenPackage));
    }

    /// <summary>
    /// Native <c>TqFOpen</c> (<c>0x100042B0</c>) hard-codes package-only lookup at
    /// <c>0x10001747</c>, so a loose override is invisible to that entry point.
    /// </summary>
    [Fact]
    public void TryOpenRead_PackageOnly_IgnoresLooseOverride()
    {
        using TemporaryContentDirectory temporaryDirectory = new();
        temporaryDirectory.WriteFile("ini/package.ini", "data.wdf\n");
        temporaryDirectory.WriteFile("data.wdf", CreateWdf("data/example.bin", [1, 2, 3]));
        temporaryDirectory.WriteFile("data/example.bin", [9, 8, 7]);

        PackagedClientContentSource source = PackagedClientContentSource.Open(temporaryDirectory.RootPath);

        Assert.Equal([1, 2, 3], ReadAll(source, "data/example.bin", ContentLookupMode.PackageOnly));
    }

    /// <summary>
    /// Native <c>TqFDump_Inner</c> mode 1 (<c>0x10001B18</c>) never consults a package.
    /// </summary>
    [Fact]
    public void TryOpenRead_LooseOnly_IgnoresPackageEntry()
    {
        using TemporaryContentDirectory temporaryDirectory = new();
        temporaryDirectory.WriteFile("ini/package.ini", "data.wdf\n");
        temporaryDirectory.WriteFile("data.wdf", CreateWdf("data/example.bin", [1, 2, 3]));

        PackagedClientContentSource source = PackagedClientContentSource.Open(temporaryDirectory.RootPath);

        Assert.False(source.TryOpenRead("data/example.bin", ContentLookupMode.LooseOnly, out Stream? stream));
        Assert.Null(stream);
    }

    [Fact]
    public void TryOpenRead_RejectsUnknownLookupMode()
    {
        using TemporaryContentDirectory temporaryDirectory = new();
        temporaryDirectory.WriteFile("data/example.bin", [1]);

        PackagedClientContentSource source = PackagedClientContentSource.Open(temporaryDirectory.RootPath);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => source.TryOpenRead("data/example.bin", (ContentLookupMode)42, out _)
        );
    }

    /// <summary>
    /// Native <c>sub_100014F0</c> discards <c>WdfHandler_OpenFile</c>'s failure at
    /// <c>0x10001620</c>, so an absent declared package is recorded and tolerated.
    /// </summary>
    [Fact]
    public void Open_RecordsDeclaredPackagesThatAreNotPresent()
    {
        using TemporaryContentDirectory temporaryDirectory = new();
        temporaryDirectory.WriteFile("ini/package.ini", "data.wdf c3.wdf\n");
        temporaryDirectory.WriteFile("data.wdf", CreateWdf("data/example.bin", [1]));

        PackagedClientContentSource source = PackagedClientContentSource.Open(temporaryDirectory.RootPath);

        Assert.Equal(
            [
                new WdfPackageRegistration("data.wdf", "data", WdfPackageRegistrationOutcome.Registered),
                new WdfPackageRegistration("c3.wdf", "c3", WdfPackageRegistrationOutcome.FileNotFound),
            ],
            source.PackageRegistrations
        );
    }

    /// <summary>
    /// Native <c>TqPackagesOpen</c> returns at <c>0x10003DEF</c> when the prefix is already
    /// registered, so the first declaration wins and the duplicate is discarded.
    /// </summary>
    [Fact]
    public void Open_WhenTwoDeclarationsSharePrefix_KeepsTheFirstRegistration()
    {
        using TemporaryContentDirectory temporaryDirectory = new();
        temporaryDirectory.WriteFile("ini/package.ini", "data.wdf\ndata.dat\n");
        temporaryDirectory.WriteFile("data.wdf", CreateWdf("data/example.bin", [1, 2, 3]));
        temporaryDirectory.WriteFile("data.dat", CreateWdf("data/example.bin", [9]));

        PackagedClientContentSource source = PackagedClientContentSource.Open(temporaryDirectory.RootPath);

        Assert.Equal(
            [
                new WdfPackageRegistration("data.wdf", "data", WdfPackageRegistrationOutcome.Registered),
                new WdfPackageRegistration("data.dat", "data", WdfPackageRegistrationOutcome.DuplicatePrefix),
            ],
            source.PackageRegistrations
        );

        Assert.Equal([1, 2, 3], ReadAll(source, "data/example.bin", ContentLookupMode.PackageOnly));
    }

    /// <summary>
    /// Native strips from the <b>last</b> <c>'.'</c> (<c>strrchr</c> at <c>0x10003D86</c>), so a
    /// multi-dot declaration registers a prefix no virtual path can reach.
    /// </summary>
    [Fact]
    public void Open_DerivesPrefixFromTheLastDot()
    {
        using TemporaryContentDirectory temporaryDirectory = new();
        temporaryDirectory.WriteFile("ini/package.ini", "data.v2.wdf\n");
        temporaryDirectory.WriteFile("data.v2.wdf", CreateWdf("data/example.bin", [1]));

        PackagedClientContentSource source = PackagedClientContentSource.Open(temporaryDirectory.RootPath);

        Assert.Equal(
            [new WdfPackageRegistration("data.v2.wdf", "data.v2", WdfPackageRegistrationOutcome.Registered)],
            source.PackageRegistrations
        );

        Assert.False(source.TryOpenRead("data/example.bin", ContentLookupMode.PackageOnly, out _));
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

        PackagedClientContentSource source = PackagedClientContentSource.Open(temporaryDirectory.RootPath);

        Assert.Empty(source.PackageRegistrations);
        Assert.Equal([4, 5], ReadAll(source, "data/example.bin", ContentLookupMode.LooseThenPackage));
    }

    [Fact]
    public void OpenRequiredRead_ThrowsWhenTheModeCannotBeSatisfied()
    {
        using TemporaryContentDirectory temporaryDirectory = new();
        temporaryDirectory.WriteFile("data/example.bin", [1]);

        PackagedClientContentSource source = PackagedClientContentSource.Open(temporaryDirectory.RootPath);

        Assert.Throws<FileNotFoundException>(
            () => source.OpenRequiredRead("data/example.bin", ContentLookupMode.PackageOnly)
        );
    }

    [Theory]
    [InlineData("/data/example.bin")]
    [InlineData("data/../../example.bin")]
    [InlineData("data/./example.bin")]
    public void TryOpenRead_RejectsPathsThatEscapeTheContentRoot(string contentPath)
    {
        using TemporaryContentDirectory temporaryDirectory = new();
        temporaryDirectory.WriteFile("ini/package.ini", "data.wdf\n");
        temporaryDirectory.WriteFile("data.wdf", CreateWdf("data/example.bin", [1]));

        PackagedClientContentSource source = PackagedClientContentSource.Open(temporaryDirectory.RootPath);

        Assert.Throws<ArgumentException>(
            () => source.TryOpenRead(contentPath, ContentLookupMode.PackageOnly, out _)
        );
    }

    private static byte[] ReadAll(PackagedClientContentSource source, string contentPath, ContentLookupMode mode)
    {
        using Stream stream = source.OpenRequiredRead(contentPath, mode);
        using MemoryStream destination = new();

        stream.CopyTo(destination);

        return destination.ToArray();
    }

    private static byte[] CreateWdf(string contentPath, ReadOnlySpan<byte> payload)
    {
        int tableOffset = WdfArchive.HeaderLength + payload.Length;
        byte[] archive = new byte[tableOffset + WdfArchive.EntryLength];

        BinaryPrimitives.WriteUInt32LittleEndian(archive, WdfArchive.Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(archive.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(archive.AsSpan(8), (uint)tableOffset);
        payload.CopyTo(archive.AsSpan(WdfArchive.HeaderLength));
        BinaryPrimitives.WriteUInt32LittleEndian(archive.AsSpan(tableOffset), WdfPathHash.Compute(contentPath));
        BinaryPrimitives.WriteUInt32LittleEndian(archive.AsSpan(tableOffset + 4), WdfArchive.HeaderLength);
        BinaryPrimitives.WriteUInt32LittleEndian(archive.AsSpan(tableOffset + 8), (uint)payload.Length);

        return archive;
    }
}
