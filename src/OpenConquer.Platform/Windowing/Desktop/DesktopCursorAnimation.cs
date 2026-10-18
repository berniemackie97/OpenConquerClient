namespace OpenConquer.Platform.Windowing.Desktop;

/// <summary>Pre-decoded RGBA cursor frames and the cursor's fixed hotspot.</summary>
public sealed class DesktopCursorAnimation
{
    private readonly byte[][] _frames;

    public DesktopCursorAnimation(IEnumerable<ReadOnlyMemory<byte>> rgbaFrames, int width, int height, int hotspotX, int hotspotY, TimeSpan frameDuration)
    {
        ArgumentNullException.ThrowIfNull(rgbaFrames);
        if (width is < 1 or > 128 || height is < 1 or > 128 ||
            hotspotX < 0 || hotspotX >= width || hotspotY < 0 || hotspotY >= height ||
            frameDuration < TimeSpan.FromMilliseconds(16) || frameDuration > TimeSpan.FromSeconds(2))
        {
            throw new ArgumentOutOfRangeException(nameof(rgbaFrames), "Cursor dimensions, hotspot, or frame interval are invalid.");
        }
        List<byte[]> frames = new();
        int bytesPerFrame = checked(width * height * 4);
        foreach (ReadOnlyMemory<byte> source in rgbaFrames)
        {
            if (frames.Count >= 32 || source.Length != bytesPerFrame)
            {
                throw new ArgumentException("Cursor animation has too many frames or an invalid frame length.", nameof(rgbaFrames));
            }
            frames.Add(source.ToArray());
        }
        if (frames.Count == 0)
        {
            throw new ArgumentException("Cursor animation requires at least one frame.", nameof(rgbaFrames));
        }
        _frames = frames.ToArray();
        Width = width;
        Height = height;
        HotspotX = hotspotX;
        HotspotY = hotspotY;
        FrameDuration = frameDuration;
    }

    public int Width
    {
        get;
    }
    public int Height
    {
        get;
    }
    public int HotspotX
    {
        get;
    }
    public int HotspotY
    {
        get;
    }
    public TimeSpan FrameDuration
    {
        get;
    }
    public int FrameCount => _frames.Length;
    internal Memory<byte> Frame(int index) => _frames[index];

    public int FrameAt(TimeSpan elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(elapsed, TimeSpan.Zero);
        long step = elapsed.Ticks / FrameDuration.Ticks;
        return (int)(step % _frames.Length);
    }
}
