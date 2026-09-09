using OpenConquer.Content.Ani;
using OpenConquer.Content.Images;

namespace OpenConquer.Content.Tests.Ani;

public sealed class AniFrameSetTests
{
    [Fact]
    public void Constructor_ExposesSectionAndFrames()
    {
        RgbaImage first = CreateImage(10);
        RgbaImage second = CreateImage(20);
        AniFrameSet frameSet = new("Effect", [first, second]);

        Assert.Equal("Effect", frameSet.SectionName);
        Assert.Equal(2, frameSet.FrameCount);
        Assert.Equal(2, frameSet.Frames.Count);
        Assert.Same(first, frameSet.Frames[0]);
        Assert.Same(second, frameSet.Frames[1]);
    }

    [Fact]
    public void Constructor_ClonesFrameArray()
    {
        RgbaImage first = CreateImage(10);
        RgbaImage second = CreateImage(20);
        RgbaImage replacement = CreateImage(30);
        RgbaImage[] frames = [first, second];

        AniFrameSet frameSet = new("Effect", frames);
        frames[0] = replacement;

        Assert.Same(first, frameSet.Frames[0]);
        Assert.Same(second, frameSet.Frames[1]);
    }

    [Fact]
    public void Frames_CannotBeModifiedThroughExposedCollection()
    {
        RgbaImage first = CreateImage(10);
        RgbaImage replacement = CreateImage(20);
        AniFrameSet frameSet = new("Effect", [first]);

        IList<RgbaImage> frames = Assert.IsAssignableFrom<IList<RgbaImage>>(frameSet.Frames);

        Assert.Throws<NotSupportedException>(() => frames[0] = replacement);
        Assert.Same(first, frameSet.Frames[0]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Constructor_RejectsMissingSectionName(string? sectionName)
    {
        RgbaImage[] frames = [CreateImage(10)];

        Assert.ThrowsAny<ArgumentException>(() => new AniFrameSet(sectionName!, frames));
    }

    [Fact]
    public void Constructor_RejectsNullFrameArray()
    {
        Assert.Throws<ArgumentNullException>(() => new AniFrameSet("Effect", null!));
    }

    [Fact]
    public void Constructor_RejectsEmptyFrameArray()
    {
        Assert.Throws<ArgumentException>(() => new AniFrameSet("Effect", []));
    }

    [Fact]
    public void Constructor_RejectsNullFrame()
    {
        RgbaImage[] frames = [CreateImage(10), null!];

        ArgumentException exception = Assert.Throws<ArgumentException>(() => new AniFrameSet("Effect", frames));

        Assert.Equal("frames", exception.ParamName);
    }

    [Theory]
    [InlineData(0U, 0)]
    [InlineData(1U, 1)]
    [InlineData(2U, 2)]
    [InlineData(3U, 3)]
    [InlineData(4U, 0)]
    [InlineData(5U, 1)]
    [InlineData(8U, 0)]
    [InlineData(uint.MaxValue, 3)]
    public void GetFrame_UsesUnsignedModuloSelection(uint requestedFrame, int expectedFrame)
    {
        RgbaImage[] frames =
        [
            CreateImage(10),
            CreateImage(20),
            CreateImage(30),
            CreateImage(40),
        ];

        AniFrameSet frameSet = new("Effect", frames);

        Assert.Same(frames[expectedFrame], frameSet.GetFrame(requestedFrame));
    }

    [Fact]
    public void GetFrame_SingleFrameAlwaysReturnsThatFrame()
    {
        RgbaImage frame = CreateImage(10);
        AniFrameSet frameSet = new("Static", [frame]);

        Assert.Same(frame, frameSet.GetFrame(0));
        Assert.Same(frame, frameSet.GetFrame(1));
        Assert.Same(frame, frameSet.GetFrame(uint.MaxValue));
    }

    private static RgbaImage CreateImage(byte value)
    {
        return new RgbaImage(width: 1, height: 1, [value, value, value, byte.MaxValue]);
    }
}
