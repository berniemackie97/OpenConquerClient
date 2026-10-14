using OpenConquer.Platform.Windowing.Desktop;

namespace OpenConquer.Platform.Tests.Windowing;

public sealed class DesktopCursorAnimationTests
{
    [Fact]
    public void CursorTimelineUsesBoundedMonotonicStepsAndWraps()
    {
        DesktopCursorAnimation frames = new(Enumerable.Range(0, 5).Select(_ => (ReadOnlyMemory<byte>)new byte[32 * 32 * 4]),
            32, 32, 0, 0, TimeSpan.FromMilliseconds(100));
        Assert.Equal(5, frames.FrameCount);
        Assert.Equal(0, frames.FrameAt(TimeSpan.Zero));
        Assert.Equal(0, frames.FrameAt(TimeSpan.FromMilliseconds(99)));
        Assert.Equal(1, frames.FrameAt(TimeSpan.FromMilliseconds(100)));
        Assert.Equal(4, frames.FrameAt(TimeSpan.FromMilliseconds(499)));
        Assert.Equal(0, frames.FrameAt(TimeSpan.FromMilliseconds(500)));
        Assert.Throws<ArgumentOutOfRangeException>(() => frames.FrameAt(TimeSpan.FromMilliseconds(-1)));
    }

    [Fact]
    public void CursorFramesAreCopiedAndInvalidDimensionsRefused()
    {
        byte[] original = new byte[4 * 4 * 4];
        original[0] = 10;
        DesktopCursorAnimation frames = new([(ReadOnlyMemory<byte>)original], 4, 4, 1, 2, TimeSpan.FromMilliseconds(100));
        original[0] = 99;
        Assert.Equal((byte)10, frames.Frame(0).Span[0]);
        Assert.Throws<ArgumentException>(() => new DesktopCursorAnimation([(ReadOnlyMemory<byte>)new byte[3]], 4, 4, 0, 0, TimeSpan.FromMilliseconds(100)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DesktopCursorAnimation([(ReadOnlyMemory<byte>)new byte[64]], 4, 4, 4, 0, TimeSpan.FromMilliseconds(100)));
    }
}
