using System.Buffers.Binary;
using OpenConquer.Content.Ani;
using OpenConquer.Content.Images;
using OpenConquer.Content.Tests.Images;
using OpenConquer.Content.Tests.Wdf;

namespace OpenConquer.Content.Tests.Ani;

public sealed class AniFrameLoaderTests
{
    private const string AniContentPath = "ani/Common.Ani";
    private const string FrameContentPath = "data/pic/Syndicate.tga";
    private const string SectionName = "Syndicate";

    [Fact]
    public void Load_LooseThenPackage_LoadsLooseAniAndPackagedFrame()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(AniContentPath, $"[{SectionName}]\nFrameAmount=1\nFrame0={FrameContentPath}\n");
        temporaryDirectory.WriteFile("ini/package.ini", "data.wdf\n");
        temporaryDirectory.WriteFile("data.wdf", WdfTestArchiveBuilder.CreateSingleEntry(FrameContentPath, CreateSinglePixelTarga()));

        PackagedClientContentSource contentSource = PackagedClientContentSource.Open(temporaryDirectory.RootPath);

        RgbaImage image = AniFrameLoader.Load(contentSource, AniContentPath, SectionName, frameIndex: 0, ContentLookupMode.LooseThenPackage);

        Assert.Equal(1, image.Width);
        Assert.Equal(1, image.Height);
        Assert.Equal([10, 20, 30, 40], image.Pixels.ToArray());
    }

    [Fact]
    public void Load_RejectsFrameIndexOutsideSection()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(AniContentPath, $"[{SectionName}]\nFrameAmount=1\nFrame0={FrameContentPath}\n");

        ClientContentRoot contentSource = new(temporaryDirectory.RootPath);

        Assert.Throws<ArgumentOutOfRangeException>(() => AniFrameLoader.Load(contentSource, AniContentPath, SectionName, frameIndex: 1, ContentLookupMode.LooseOnly));
    }

    [Fact]
    public void Load_RejectsUnsupportedFrameImage()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(AniContentPath, $"[{SectionName}]\nFrameAmount=1\nFrame0=data/pic/Syndicate.dds\n");

        ClientContentRoot contentSource = new(temporaryDirectory.RootPath);

        Assert.Throws<InvalidDataException>(() => AniFrameLoader.Load(contentSource, AniContentPath, SectionName, frameIndex: 0, ContentLookupMode.LooseOnly));
    }

    private static byte[] CreateSinglePixelTarga()
    {
        const int HeaderLength = 18;

        byte[] targa = new byte[HeaderLength + 5];

        targa[2] = 10;
        BinaryPrimitives.WriteUInt16LittleEndian(targa.AsSpan(12), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(targa.AsSpan(14), 1);
        targa[16] = 32;
        targa[17] = 0x08;
        targa[HeaderLength] = 0x80;
        targa.AsSpan(HeaderLength + 1, 4).CopyFrom([30, 20, 10, 40]);

        return targa;
    }
}
