using System.Buffers.Binary;
using OpenConquer.Content.Startup;
using OpenConquer.Content.Tests.Images;
using OpenConquer.Content.Wdf;

namespace OpenConquer.Content.Tests.Startup;

public sealed class StartupLogoTests
{
    [Theory]
    [InlineData(100, 1)]
    [InlineData(101, 2)]
    public void Load_SelectsVariantFromMonotonicTick(long tick, int expectedVariant)
    {
        using TemporaryContentDirectory temporaryDirectory = new();
        temporaryDirectory.WriteFile("ini/info.ini", "[DlgLogo]\nBgFormat=Data/Main/Logo%d.bmp\n");
        temporaryDirectory.WriteFile("data/main/Logo1.bmp", WindowsBitmapReaderTests.CreateTwoByTwoBitmap());
        temporaryDirectory.WriteFile("data/main/Logo2.bmp", WindowsBitmapReaderTests.CreateTwoByTwoBitmap());

        StartupLogo logo = StartupLogo.Load(new ClientContentRoot(temporaryDirectory.RootPath), tick);

        Assert.Equal(expectedVariant, logo.VariantIndex);
        Assert.Equal($"Data/Main/Logo{expectedVariant}.bmp", logo.ContentPath);
        Assert.Null(logo.UnavailableReason);
        Assert.NotNull(logo.Image);
        Assert.Equal(2, logo.Image.Width);
        Assert.Equal(2, logo.Image.Height);
    }

    /// <summary>
    /// Native stores a null <c>HBITMAP</c> at <c>0x4B0A8E</c> without checking it and
    /// <c>OnInitDialog</c> still returns <c>TRUE</c> at <c>0x4B0AAB</c>, so a missing bitmap must
    /// not fail the load.
    /// </summary>
    [Fact]
    public void Load_WhenBitmapIsMissing_ReportsUnavailableWithoutThrowing()
    {
        using TemporaryContentDirectory temporaryDirectory = new();
        temporaryDirectory.WriteFile("ini/info.ini", "[DlgLogo]\nBgFormat=Data/Main/Logo%d.bmp\n");

        StartupLogo logo = StartupLogo.Load(new ClientContentRoot(temporaryDirectory.RootPath), monotonicTickMilliseconds: 0);

        Assert.Equal(1, logo.VariantIndex);
        Assert.Equal("Data/Main/Logo1.bmp", logo.ContentPath);
        Assert.Null(logo.Image);
        Assert.Contains("was not found as a loose file", logo.UnavailableReason);
    }

    [Fact]
    public void Load_WhenBitmapCannotBeDecoded_ReportsUnavailableWithoutThrowing()
    {
        using TemporaryContentDirectory temporaryDirectory = new();
        temporaryDirectory.WriteFile("ini/info.ini", "[DlgLogo]\nBgFormat=Data/Main/Logo%d.bmp\n");
        temporaryDirectory.WriteFile("data/main/Logo1.bmp", "not a bitmap");

        StartupLogo logo = StartupLogo.Load(new ClientContentRoot(temporaryDirectory.RootPath), monotonicTickMilliseconds: 0);

        Assert.Null(logo.Image);
        Assert.Contains("could not be loaded", logo.UnavailableReason);
    }

    /// <summary>
    /// Native loads the bitmap through <c>LoadImageA(..., LR_LOADFROMFILE)</c> at <c>0x4B0A88</c>,
    /// which is raw Win32 file I/O. A logo present only inside a package must therefore not resolve.
    /// </summary>
    [Fact]
    public void Load_DoesNotResolveTheBitmapFromAPackage()
    {
        using TemporaryContentDirectory temporaryDirectory = new();
        temporaryDirectory.WriteFile("ini/info.ini", "[DlgLogo]\nBgFormat=Data/Main/Logo%d.bmp\n");
        temporaryDirectory.WriteFile("ini/package.ini", "data.wdf\n");
        temporaryDirectory.WriteFile(
            "data.wdf",
            CreateWdf("data/main/logo1.bmp", WindowsBitmapReaderTests.CreateTwoByTwoBitmap())
        );

        PackagedClientContentSource source = PackagedClientContentSource.Open(temporaryDirectory.RootPath);

        Assert.True(source.TryOpenRead("data/main/logo1.bmp", ContentLookupMode.PackageOnly, out Stream? packagedStream));
        packagedStream.Dispose();

        StartupLogo logo = StartupLogo.Load(source, monotonicTickMilliseconds: 0);

        Assert.Null(logo.Image);
        Assert.Contains("was not found as a loose file", logo.UnavailableReason);
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
