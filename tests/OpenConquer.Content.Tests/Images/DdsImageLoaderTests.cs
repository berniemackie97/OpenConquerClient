using System.Buffers.Binary;
using OpenConquer.Content.Images;
using OpenConquer.Content.Tests.Wdf;

namespace OpenConquer.Content.Tests.Images;

public sealed class DdsImageLoaderTests
{
    private const string ContentPath = "data/firework/yinfa1/1.dds";
    private const int PixelDataOffset = 128;

    [Fact]
    public void Load_LooseOnly_DecodesLooseDxt3()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(ContentPath, CreateSinglePixelDxt3());

        ClientContentRoot contentSource = new(temporaryDirectory.RootPath);

        RgbaImage image = DdsImageLoader.Load(contentSource, ContentPath, ContentLookupMode.LooseOnly);

        AssertImage(image);
    }

    [Fact]
    public void Load_PackageOnly_DecodesPackagedDxt3()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("ini/package.ini", "data.wdf\n");
        temporaryDirectory.WriteFile("data.wdf", WdfTestArchiveBuilder.CreateSingleEntry(ContentPath, CreateSinglePixelDxt3()));

        PackagedClientContentSource contentSource = PackagedClientContentSource.Open(temporaryDirectory.RootPath);

        RgbaImage image = DdsImageLoader.Load(contentSource, ContentPath, ContentLookupMode.PackageOnly);

        AssertImage(image);
    }

    private static byte[] CreateSinglePixelDxt3()
    {
        byte[] dds = new byte[PixelDataOffset + 16];

        BinaryPrimitives.WriteUInt32LittleEndian(dds, 0x20534444);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(4), 124);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(8), 0x00081007);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(12), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(16), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(20), 16);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(76), 32);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(80), 4);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(84), 0x33545844);
        BinaryPrimitives.WriteUInt32LittleEndian(dds.AsSpan(108), 0x00001000);

        Span<byte> block = dds.AsSpan(PixelDataOffset, 16);

        BinaryPrimitives.WriteUInt64LittleEndian(block, 0x4);
        BinaryPrimitives.WriteUInt16LittleEndian(block[8..], 0xF800);
        BinaryPrimitives.WriteUInt16LittleEndian(block[10..], 0);

        return dds;
    }

    private static void AssertImage(RgbaImage image)
    {
        Assert.Equal(1, image.Width);
        Assert.Equal(1, image.Height);
        Assert.Equal([255, 0, 0, 68], image.Pixels.ToArray());
    }
}
