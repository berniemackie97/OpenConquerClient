using System.Text;
using OpenConquer.Content.Ani;

namespace OpenConquer.Content.Tests.Ani;

public sealed class AniIndexFileTests
{
    [Fact]
    public void Load_ParsesVerifiedSingleFrameSection()
    {
        using MemoryStream stream = CreateStream("[Syndicate]\nFrameAmount=1\nFrame0=data/pic/Syndicate.tga\n");

        AniIndexSection section = AniIndexFile.Load(stream, "ani/Common.Ani").GetRequiredSection("Syndicate");

        Assert.Equal("Syndicate", section.Name);
        Assert.Equal(1, section.FrameCount);
        Assert.Equal("data/pic/Syndicate.tga", section.FramePaths[0]);
    }

    [Fact]
    public void Load_RequiresFramesInSequentialNativeOrder()
    {
        using MemoryStream stream = CreateStream("[Cursor]\nFrameAmount=2\nFrame1=data/pic/cursor1.tga\nFrame0=data/pic/cursor0.tga\n");

        Assert.Throws<InvalidDataException>(() => AniIndexFile.Load(stream, "ani/Common.Ani"));
    }

    [Fact]
    public void TryGetSection_UsesOrdinalCaseSensitiveLookup()
    {
        using MemoryStream stream = CreateStream("[Syndicate]\nFrameAmount=0\n");

        AniIndexFile index = AniIndexFile.Load(stream, "ani/Common.Ani");

        Assert.True(index.TryGetSection("Syndicate", out _));
        Assert.False(index.TryGetSection("syndicate", out AniIndexSection? section));
        Assert.Null(section);
    }

    [Fact]
    public void Load_PreservesLatin1FramePath()
    {
        using MemoryStream stream = CreateStream("[Syndicate]\nFrameAmount=1\nFrame0=data/pic/Café.tga\n");

        AniIndexSection section = AniIndexFile.Load(stream, "ani/Common.Ani").GetRequiredSection("Syndicate");

        Assert.Equal("data/pic/Café.tga", section.FramePaths[0]);
    }

    [Fact]
    public void Load_ParsesFramePathUsingNativeWhitespaceTermination()
    {
        using MemoryStream stream = CreateStream("[Syndicate]\nFrameAmount=1\nFrame0=data/pic/Syndicate.tga ignored\n");

        AniIndexSection section = AniIndexFile.Load(stream, "ani/Common.Ani").GetRequiredSection("Syndicate");

        Assert.Equal("data/pic/Syndicate.tga", section.FramePaths[0]);
    }

    [Theory]
    [InlineData("64", 0)]
    [InlineData("65", 1)]
    [InlineData("127", 63)]
    [InlineData("128", 0)]
    public void Load_NormalizesFrameAmountUsingNativeModulo64Behavior(string rawFrameAmount, int expectedFrameCount)
    {
        StringBuilder contents = new();
        contents.Append("[Section]\nFrameAmount=").Append(rawFrameAmount).Append('\n');

        for (int frameIndex = 0; frameIndex < expectedFrameCount; frameIndex++)
        {
            contents.Append("Frame").Append(frameIndex).Append("=data/pic/frame.tga\n");
        }

        using MemoryStream stream = CreateStream(contents.ToString());

        AniIndexSection section = AniIndexFile.Load(stream, "ani/Common.Ani").GetRequiredSection("Section");

        Assert.Equal(expectedFrameCount, section.FrameCount);
    }

    [Fact]
    public void Load_RejectsNegativeNormalizedFrameAmount()
    {
        using MemoryStream stream = CreateStream("[Syndicate]\nFrameAmount=-1\n");

        Assert.Throws<InvalidDataException>(() => AniIndexFile.Load(stream, "ani/Common.Ani"));
    }

    [Fact]
    public void Load_RejectsMissingFrameAmount()
    {
        using MemoryStream stream = CreateStream("[Syndicate]\nFrame0=data/pic/Syndicate.tga\n");

        Assert.Throws<InvalidDataException>(() => AniIndexFile.Load(stream, "ani/Common.Ani"));
    }

    [Fact]
    public void Load_RejectsMissingRequiredFrame()
    {
        using MemoryStream stream = CreateStream("[Cursor]\nFrameAmount=2\nFrame0=data/pic/cursor0.tga\n");

        Assert.Throws<InvalidDataException>(() => AniIndexFile.Load(stream, "ani/Common.Ani"));
    }

    [Fact]
    public void Load_LastDuplicateSectionDefinitionWins()
    {
        using MemoryStream stream = CreateStream("[Syndicate]\nFrameAmount=0\n[Syndicate]\nFrameAmount=1\nFrame0=data/pic/Syndicate.tga\n");

        AniIndexSection section = AniIndexFile.Load(stream, "ani/Common.Ani").GetRequiredSection("Syndicate");

        Assert.Equal(1, section.FrameCount);
        Assert.Equal("data/pic/Syndicate.tga", section.FramePaths[0]);
    }

    [Fact]
    public void Load_FromContentSourceReadsLooseAniFile()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        temporaryDirectory.WriteFile("ani/Common.Ani", "[Syndicate]\nFrameAmount=1\nFrame0=data/pic/Syndicate.tga\n");

        ClientContentRoot contentSource = new(temporaryDirectory.RootPath);

        AniIndexSection section = AniIndexFile.Load(contentSource, "ani/Common.Ani", ContentLookupMode.LooseOnly).GetRequiredSection("Syndicate");

        Assert.Equal("data/pic/Syndicate.tga", section.FramePaths[0]);
    }

    [Fact]
    public void GetRequiredSection_RejectsMissingSection()
    {
        using MemoryStream stream = CreateStream("[Syndicate]\nFrameAmount=0\n");

        AniIndexFile index = AniIndexFile.Load(stream, "ani/Common.Ani");

        Assert.Throws<InvalidDataException>(() => index.GetRequiredSection("Missing"));
    }

    private static MemoryStream CreateStream(string contents)
    {
        return new MemoryStream(Encoding.Latin1.GetBytes(contents));
    }
}
