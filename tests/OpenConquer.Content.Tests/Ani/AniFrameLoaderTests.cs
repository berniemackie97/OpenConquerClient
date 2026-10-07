using System.Buffers.Binary;
using OpenConquer.Content.Ani;
using OpenConquer.Content.Images;
using OpenConquer.Content.Tests.Images;
using OpenConquer.Content.Tests.Wdf;

namespace OpenConquer.Content.Tests.Ani;

public sealed class AniFrameLoaderTests
{
    private const string AniContentPath = "ani/Common.Ani";
    private const string TargaFrameContentPath = "data/pic/Syndicate.tga";
    private const string DdsFrameContentPath = "data/firework/yinfa1/1.dds";
    private const string SectionName = "Syndicate";
    private const int DdsPixelDataOffset = 128;

    [Fact]
    public void Load_LooseThenPackage_LoadsLooseAniAndPackagedTargaFrame()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(AniContentPath, $"[{SectionName}]\nFrameAmount=1\nFrame0={TargaFrameContentPath}\n");
        temporaryDirectory.WriteFile("ini/package.ini", "data.wdf\n");
        temporaryDirectory.WriteFile("data.wdf", WdfTestArchiveBuilder.CreateSingleEntry(TargaFrameContentPath, CreateSinglePixelTarga()));

        PackagedClientContentSource contentSource = PackagedClientContentSource.Open(temporaryDirectory.RootPath);

        RgbaImage image = AniFrameLoader.Load(contentSource, AniContentPath, SectionName, frameIndex: 0, ContentLookupMode.LooseThenPackage);

        Assert.Equal(1, image.Width);
        Assert.Equal(1, image.Height);
        Assert.Equal([10, 20, 30, 40], image.Pixels.ToArray());
    }

    [Fact]
    public void Load_LooseThenPackage_LoadsLooseAniAndPackagedDxt3Frame()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(AniContentPath, $"[{SectionName}]\nFrameAmount=1\nFrame0={DdsFrameContentPath}\n");
        temporaryDirectory.WriteFile("ini/package.ini", "data.wdf\n");
        temporaryDirectory.WriteFile("data.wdf", WdfTestArchiveBuilder.CreateSingleEntry(DdsFrameContentPath, CreateSinglePixelDxt3()));

        PackagedClientContentSource contentSource = PackagedClientContentSource.Open(temporaryDirectory.RootPath);

        RgbaImage image = AniFrameLoader.Load(contentSource, AniContentPath, SectionName, frameIndex: 0, ContentLookupMode.LooseThenPackage);

        Assert.Equal(1, image.Width);
        Assert.Equal(1, image.Height);
        Assert.Equal([255, 0, 0, 68], image.Pixels.ToArray());
    }

    [Fact]
    public void Load_AcceptsCaseInsensitiveDdsExtension()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        const string frameContentPath = "data/firework/yinfa1/1.DDS";

        temporaryDirectory.WriteFile(AniContentPath, $"[{SectionName}]\nFrameAmount=1\nFrame0={frameContentPath}\n");
        temporaryDirectory.WriteFile(frameContentPath, CreateSinglePixelDxt3());

        ClientContentRoot contentSource = new(temporaryDirectory.RootPath);

        RgbaImage image = AniFrameLoader.Load(contentSource, AniContentPath, SectionName, frameIndex: 0, ContentLookupMode.LooseOnly);

        Assert.Equal(1, image.Width);
        Assert.Equal(1, image.Height);
        Assert.Equal([255, 0, 0, 68], image.Pixels.ToArray());
    }

    [Fact]
    public void Load_RejectsFrameIndexOutsideSection()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(AniContentPath, $"[{SectionName}]\nFrameAmount=1\nFrame0={TargaFrameContentPath}\n");

        ClientContentRoot contentSource = new(temporaryDirectory.RootPath);

        Assert.Throws<ArgumentOutOfRangeException>(() => AniFrameLoader.Load(contentSource, AniContentPath, SectionName, frameIndex: 1, ContentLookupMode.LooseOnly));
    }

    [Fact]
    public void Load_RejectsUnsupportedFrameImage()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(AniContentPath, $"[{SectionName}]\nFrameAmount=1\nFrame0=data/pic/Syndicate.bmp\n");

        ClientContentRoot contentSource = new(temporaryDirectory.RootPath);

        Assert.Throws<InvalidDataException>(() => AniFrameLoader.Load(contentSource, AniContentPath, SectionName, frameIndex: 0, ContentLookupMode.LooseOnly));
    }

    [Theory]
    [InlineData(0u, true)]
    [InlineData(1u, false)]
    [InlineData(2u, true)]
    [InlineData(3u, false)]
    [InlineData(uint.MaxValue, false)]
    public void LoadWrappedFrame_UsesUnsignedModuloSelection(uint frameIndex, bool expectedRed)
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(AniContentPath,
            $"[{SectionName}]\nFrameAmount=2\nFrame0=data/test/a.dds\nFrame1=data/test/b.dds\n");
        temporaryDirectory.WriteFile("data/test/a.dds", CreateSinglePixelDxt3(red: true));
        temporaryDirectory.WriteFile("data/test/b.dds", CreateSinglePixelDxt3(red: false));

        ClientContentRoot contentSource = new(temporaryDirectory.RootPath);
        AniIndexSection section = AniIndexFile.Load(contentSource, AniContentPath, ContentLookupMode.LooseOnly).GetRequiredSection(SectionName);

        RgbaImage image = AniFrameLoader.LoadWrappedFrame(contentSource, section, frameIndex, ContentLookupMode.LooseOnly);

        Assert.Equal(expectedRed ? byte.MaxValue : (byte)0, image.Pixels.Span[0]);
        Assert.Equal(expectedRed ? (byte)0 : byte.MaxValue, image.Pixels.Span[2]);
        Assert.Equal(68, image.Pixels.Span[3]);
    }

    [Fact]
    public void LoadWrappedFrame_DoesNotResolveUnselectedFrames()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(AniContentPath,
            $"[{SectionName}]\nFrameAmount=2\nFrame0={DdsFrameContentPath}\nFrame1=data/test/missing.dds\n");
        temporaryDirectory.WriteFile(DdsFrameContentPath, CreateSinglePixelDxt3());

        ClientContentRoot contentSource = new(temporaryDirectory.RootPath);
        AniIndexSection section = AniIndexFile.Load(contentSource, AniContentPath, ContentLookupMode.LooseOnly).GetRequiredSection(SectionName);

        RgbaImage image = AniFrameLoader.LoadWrappedFrame(contentSource, section, frameIndex: 0, ContentLookupMode.LooseOnly);

        Assert.Equal(1, image.Width);
        Assert.Equal(1, image.Height);
        Assert.Equal([255, 0, 0, 68], image.Pixels.ToArray());
    }

    [Fact]
    public void LoadWrappedFrame_RejectsZeroFrameSection()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(AniContentPath, $"[{SectionName}]\nFrameAmount=0\n");

        ClientContentRoot contentSource = new(temporaryDirectory.RootPath);
        AniIndexSection section = AniIndexFile.Load(contentSource, AniContentPath, ContentLookupMode.LooseOnly).GetRequiredSection(SectionName);

        Assert.Throws<InvalidDataException>(() =>
            AniFrameLoader.LoadWrappedFrame(contentSource, section, frameIndex: 0, ContentLookupMode.LooseOnly));
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

    private static byte[] CreateSinglePixelDxt3(bool red = true)
    {
        byte[] dds = new byte[DdsPixelDataOffset + 16];

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

        Span<byte> block = dds.AsSpan(DdsPixelDataOffset, 16);

        BinaryPrimitives.WriteUInt64LittleEndian(block, 0x4);

        ushort color = red ? (ushort)0xF800 : (ushort)0x001F;
        BinaryPrimitives.WriteUInt16LittleEndian(block[8..], color);
        BinaryPrimitives.WriteUInt16LittleEndian(block[10..], color);

        return dds;
    }
}
