using System.Buffers.Binary;
using OpenConquer.Content.Images;
using OpenConquer.Content.Tests.Wdf;

namespace OpenConquer.Content.Tests.Images;

public sealed class TargaImageLoaderTests
{
    private const string ContentPath = "data/pic/Syndicate.tga";

    [Fact]
    public void Load_LooseOnly_DecodesLooseTarga()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(ContentPath, CreateSinglePixelTarga());

        ClientContentRoot contentSource = new(temporaryDirectory.RootPath);

        RgbaImage image = TargaImageLoader.Load(contentSource, ContentPath, ContentLookupMode.LooseOnly);

        AssertImage(image);
    }

    [Fact]
    public void Load_PackageOnly_DecodesPackagedTarga()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("ini/package.ini", "data.wdf\n");
        temporaryDirectory.WriteFile(
            "data.wdf",
            WdfTestArchiveBuilder.CreateSingleEntry(ContentPath, CreateSinglePixelTarga())
        );

        PackagedClientContentSource contentSource = PackagedClientContentSource.Open(temporaryDirectory.RootPath);

        RgbaImage image = TargaImageLoader.Load(contentSource, ContentPath, ContentLookupMode.PackageOnly);

        AssertImage(image);
    }

    private static byte[] CreateSinglePixelTarga()
    {
        const int headerLength = 18;

        byte[] targa = new byte[headerLength + 5];

        targa[2] = 10;
        BinaryPrimitives.WriteUInt16LittleEndian(targa.AsSpan(12), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(targa.AsSpan(14), 1);
        targa[16] = 32;
        targa[17] = 0x08;

        targa[headerLength] = 0x80;
        targa.AsSpan(headerLength + 1, 4).CopyFrom([30, 20, 10, 40]);

        return targa;
    }

    private static void AssertImage(RgbaImage image)
    {
        Assert.Equal(1, image.Width);
        Assert.Equal(1, image.Height);
        Assert.Equal([10, 20, 30, 40], image.Pixels.ToArray());
    }
}
