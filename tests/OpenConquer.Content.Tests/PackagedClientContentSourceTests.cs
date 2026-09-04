using OpenConquer.Content.Tests.Wdf;

namespace OpenConquer.Content.Tests;

public sealed class PackagedClientContentSourceTests
{
    [Fact]
    public void TryOpenRead_LooseThenPackage_FallsBackToPackageWhenLooseFileIsMissing()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("ini/package.ini", "data.wdf\n");
        temporaryDirectory.WriteFile(
            "data.wdf",
            WdfTestArchiveBuilder.CreateSingleEntry("data/example.bin", [1, 2, 3])
        );

        PackagedClientContentSource source = PackagedClientContentSource.Open(
            temporaryDirectory.RootPath
        );

        Assert.Equal(
            [1, 2, 3],
            ReadAll(source, "DATA/EXAMPLE.BIN", ContentLookupMode.LooseThenPackage)
        );
    }

    [Fact]
    public void TryOpenRead_LooseThenPackage_PrefersLooseFileOverPackageEntry()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("ini/package.ini", "data.wdf\n");
        temporaryDirectory.WriteFile(
            "data.wdf",
            WdfTestArchiveBuilder.CreateSingleEntry("data/example.bin", [1, 2, 3])
        );
        temporaryDirectory.WriteFile("data/example.bin", [9, 8, 7]);

        PackagedClientContentSource source = PackagedClientContentSource.Open(
            temporaryDirectory.RootPath
        );

        Assert.Equal(
            [9, 8, 7],
            ReadAll(source, "data/example.bin", ContentLookupMode.LooseThenPackage)
        );
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
        temporaryDirectory.WriteFile(
            "data.wdf",
            WdfTestArchiveBuilder.CreateSingleEntry("data/example.bin", [1, 2, 3])
        );
        temporaryDirectory.WriteFile("data/example.bin", [9, 8, 7]);

        PackagedClientContentSource source = PackagedClientContentSource.Open(
            temporaryDirectory.RootPath
        );

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
        temporaryDirectory.WriteFile(
            "data.wdf",
            WdfTestArchiveBuilder.CreateSingleEntry("data/example.bin", [1, 2, 3])
        );

        PackagedClientContentSource source = PackagedClientContentSource.Open(
            temporaryDirectory.RootPath
        );

        Assert.False(
            source.TryOpenRead("data/example.bin", ContentLookupMode.LooseOnly, out Stream? stream)
        );

        Assert.Null(stream);
    }

    [Fact]
    public void TryOpenRead_RejectsUnknownLookupMode()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("data/example.bin", [1]);

        PackagedClientContentSource source = PackagedClientContentSource.Open(
            temporaryDirectory.RootPath
        );

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            source.TryOpenRead("data/example.bin", (ContentLookupMode)42, out _)
        );
    }

    [Theory]
    [InlineData("data//example.bin")]
    [InlineData(@"data\\example.bin")]
    [InlineData("data/example.bin/")]
    [InlineData("data/./example.bin")]
    [InlineData("data/example\0.bin")]
    public void TryOpenRead_PackageOnly_RejectsStructurallyInvalidVirtualPaths(string contentPath)
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("ini/package.ini", "data.wdf\n");
        temporaryDirectory.WriteFile(
            "data.wdf",
            WdfTestArchiveBuilder.CreateSingleEntry("data/example.bin", [1])
        );

        PackagedClientContentSource source = PackagedClientContentSource.Open(
            temporaryDirectory.RootPath
        );

        Assert.Throws<ArgumentException>(() =>
            source.TryOpenRead(contentPath, ContentLookupMode.PackageOnly, out _)
        );
    }

    [Fact]
    public void TryOpenRead_PackageOnly_DoesNotTrimVirtualPath()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("ini/package.ini", "data.wdf\n");
        temporaryDirectory.WriteFile(
            "data.wdf",
            WdfTestArchiveBuilder.CreateSingleEntry("data/example.bin", [1])
        );

        PackagedClientContentSource source = PackagedClientContentSource.Open(
            temporaryDirectory.RootPath
        );

        Assert.False(
            source.TryOpenRead(
                " data/example.bin",
                ContentLookupMode.PackageOnly,
                out Stream? stream
            )
        );

        Assert.Null(stream);
    }

    [Fact]
    public void OpenRequiredRead_ThrowsWhenTheModeCannotBeSatisfied()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("data/example.bin", [1]);

        PackagedClientContentSource source = PackagedClientContentSource.Open(
            temporaryDirectory.RootPath
        );

        Assert.Throws<FileNotFoundException>(() =>
            source.OpenRequiredRead("data/example.bin", ContentLookupMode.PackageOnly)
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
        temporaryDirectory.WriteFile(
            "data.wdf",
            WdfTestArchiveBuilder.CreateSingleEntry("data/example.bin", [1])
        );

        PackagedClientContentSource source = PackagedClientContentSource.Open(
            temporaryDirectory.RootPath
        );

        Assert.Throws<ArgumentException>(() =>
            source.TryOpenRead(contentPath, ContentLookupMode.PackageOnly, out _)
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
