using OpenConquer.Content.Images;

namespace OpenConquer.Content.Ani;

/// <summary>
/// Represents a non-empty decoded frame set from one retail ANI section.
/// </summary>
public sealed class AniFrameSet
{
    private readonly RgbaImage[] _frames;

    internal AniFrameSet(string sectionName, RgbaImage[] frames)
    {
        ArgumentException.ThrowIfNullOrEmpty(sectionName);
        ArgumentNullException.ThrowIfNull(frames);

        if (frames.Length == 0)
        {
            throw new ArgumentException("An ANI frame set must contain at least one frame.", nameof(frames));
        }

        for (int i = 0; i < frames.Length; i++)
        {
            if (frames[i] is null)
            {
                throw new ArgumentException($"ANI frame {i} is null.", nameof(frames));
            }
        }

        SectionName = sectionName;
        _frames = (RgbaImage[])frames.Clone();
        Frames = Array.AsReadOnly(_frames);
    }

    public string SectionName
    {
        get;
    }

    public IReadOnlyList<RgbaImage> Frames
    {
        get;
    }

    public int FrameCount => _frames.Length;

    /// <summary>
    /// Resolves a requested frame using the unsigned modulo behavior of retail CAni.
    /// </summary>
    public RgbaImage GetFrame(uint frameIndex)
    {
        return _frames[(int)(frameIndex % (uint)_frames.Length)];
    }
}
