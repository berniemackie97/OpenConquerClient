using OpenConquer.Content;
using OpenConquer.Content.Ani;
using OpenConquer.Content.Images;

namespace OpenConquer.Client.UI.Hud;

internal sealed class MainHudOrganiseButtonAssets
{
    private const string ControlAniPath = "ani/Control.ani";
    private const string SectionName = "Main3_OrganiseBtn";
    private const int FrameCount = 4;
    private const int FrameWidth = 64;
    private const int FrameHeight = 32;

    private readonly AniFrameSet? _frames;

    private MainHudOrganiseButtonAssets(AniFrameSet? frames)
    {
        _frames = frames;
    }

    public bool IsAvailable => _frames is not null;

    public RgbaImage? GetFrame(int frameIndex)
    {
        if ((uint)frameIndex >= FrameCount)
        {
            throw new ArgumentOutOfRangeException(nameof(frameIndex), frameIndex, $"Frame index must be between 0 and {FrameCount - 1}.");
        }

        return _frames?.GetFrame((uint)frameIndex);
    }

    public static MainHudOrganiseButtonAssets Load(IClientContentSource contentSource)
    {
        ArgumentNullException.ThrowIfNull(contentSource);

        AniIndexFile controlAni;

        try
        {
            controlAni = AniIndexFile.Load(contentSource, ControlAniPath, ContentLookupMode.LooseOnly);
        }
        catch (FileNotFoundException)
        {
            return new MainHudOrganiseButtonAssets(null);
        }

        if (!controlAni.TryGetSection(SectionName, out AniIndexSection? section))
        {
            return new MainHudOrganiseButtonAssets(null);
        }

        if (section.FrameCount != FrameCount)
        {
            throw new InvalidDataException($"ANI section [{SectionName}] must contain exactly {FrameCount} frames; found {section.FrameCount}.");
        }

        AniFrameSet frames;

        try
        {
            frames = AniFrameSetLoader.Load(contentSource, section, ContentLookupMode.LooseThenPackage);
        }
        catch (FileNotFoundException)
        {
            return new MainHudOrganiseButtonAssets(null);
        }

        for (int frameIndex = 0; frameIndex < frames.FrameCount; frameIndex++)
        {
            RgbaImage frame = frames.GetFrame((uint)frameIndex);

            if (frame.Width != FrameWidth || frame.Height != FrameHeight)
            {
                throw new InvalidDataException($"ANI section [{SectionName}] frame {frameIndex} decoded as {frame.Width}x{frame.Height}; expected {FrameWidth}x{FrameHeight}.");
            }
        }

        return new MainHudOrganiseButtonAssets(frames);
    }
}
