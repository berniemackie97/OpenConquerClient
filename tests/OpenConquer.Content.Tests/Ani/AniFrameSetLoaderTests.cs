using System.Buffers.Binary;
using OpenConquer.Content.Ani;
using OpenConquer.Content.Tests.Wdf;

namespace OpenConquer.Content.Tests.Ani;

public sealed class AniFrameSetLoaderTests
{
    private const string AniContentPath = "ani/Effect.ani";
    private const string SectionName = "Effect";

    [Fact]
    public void Load_DecodesCompleteSectionInFrameOrder()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(AniContentPath, $"[{SectionName}]\nFrameAmount=2\nFrame0=data/effect/first.tga\nFrame1=data/effect/second.tga\n");
        temporaryDirectory.WriteFile("data/effect/first.tga", CreateSinglePixelTarga(red: 10, green: 20, blue: 30, alpha: 40));
        temporaryDirectory.WriteFile("data/effect/second.tga", CreateSinglePixelTarga(red: 50, green: 60, blue: 70, alpha: 80));

        ClientContentRoot contentSource = new(temporaryDirectory.RootPath);

        AniFrameSet frameSet = AniFrameSetLoader.Load(contentSource, AniContentPath, SectionName, ContentLookupMode.LooseOnly);

        Assert.Equal(SectionName, frameSet.SectionName);
        Assert.Equal(2, frameSet.FrameCount);
        Assert.Equal([10, 20, 30, 40], frameSet.Frames[0].Pixels.ToArray());
        Assert.Equal([50, 60, 70, 80], frameSet.Frames[1].Pixels.ToArray());
    }

    [Fact]
    public void Load_LooseThenPackage_LoadsPackagedFrames()
    {
        const string frameContentPath = "data/effect/frame.tga";

        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(AniContentPath, $"[{SectionName}]\nFrameAmount=1\nFrame0={frameContentPath}\n");
        temporaryDirectory.WriteFile("ini/package.ini", "data.wdf\n");
        temporaryDirectory.WriteFile("data.wdf", WdfTestArchiveBuilder.CreateSingleEntry(frameContentPath, CreateSinglePixelTarga(red: 10, green: 20, blue: 30, alpha: 40)));

        PackagedClientContentSource contentSource = PackagedClientContentSource.Open(temporaryDirectory.RootPath);

        AniFrameSet frameSet = AniFrameSetLoader.Load(contentSource, AniContentPath, SectionName, ContentLookupMode.LooseThenPackage);

        Assert.Single(frameSet.Frames);
        Assert.Equal([10, 20, 30, 40], frameSet.Frames[0].Pixels.ToArray());
    }

    [Fact]
    public void Load_RejectsEmptySection()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(AniContentPath, $"[{SectionName}]\nFrameAmount=0\n");

        ClientContentRoot contentSource = new(temporaryDirectory.RootPath);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => AniFrameSetLoader.Load(contentSource, AniContentPath, SectionName, ContentLookupMode.LooseOnly));

        Assert.Contains($"[{SectionName}]", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_WhenLaterFrameIsMissing_ThrowsInsteadOfReturningPartialSet()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(AniContentPath, $"[{SectionName}]\nFrameAmount=2\nFrame0=data/effect/first.tga\nFrame1=data/effect/missing.tga\n");
        temporaryDirectory.WriteFile("data/effect/first.tga", CreateSinglePixelTarga(red: 10, green: 20, blue: 30, alpha: 40));

        ClientContentRoot contentSource = new(temporaryDirectory.RootPath);

        Assert.Throws<FileNotFoundException>(() => AniFrameSetLoader.Load(contentSource, AniContentPath, SectionName, ContentLookupMode.LooseOnly));
    }

    [Fact]
    public void Load_RejectsUnsupportedFrameImage()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(AniContentPath, $"[{SectionName}]\nFrameAmount=1\nFrame0=data/effect/frame.bmp\n");

        ClientContentRoot contentSource = new(temporaryDirectory.RootPath);

        Assert.Throws<InvalidDataException>(() => AniFrameSetLoader.Load(contentSource, AniContentPath, SectionName, ContentLookupMode.LooseOnly));
    }

    [Fact]
    public void Load_RejectsMissingSection()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile(AniContentPath, "[Other]\nFrameAmount=0\n");

        ClientContentRoot contentSource = new(temporaryDirectory.RootPath);

        Assert.Throws<InvalidDataException>(() => AniFrameSetLoader.Load(contentSource, AniContentPath, SectionName, ContentLookupMode.LooseOnly));
    }

    private static byte[] CreateSinglePixelTarga(byte red, byte green, byte blue, byte alpha)
    {
        const int headerLength = 18;

        byte[] targa = new byte[headerLength + 5];

        targa[2] = 10;
        BinaryPrimitives.WriteUInt16LittleEndian(targa.AsSpan(12), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(targa.AsSpan(14), 1);
        targa[16] = 32;
        targa[17] = 0x08;
        targa[headerLength] = 0x80;
        targa[headerLength + 1] = blue;
        targa[headerLength + 2] = green;
        targa[headerLength + 3] = red;
        targa[headerLength + 4] = alpha;

        return targa;
    }
}
