using OpenConquer.Content;
using OpenConquer.Content.Ani;
using OpenConquer.Content.Images;

namespace OpenConquer.Client.UI.Hud.SkillExperience;

internal sealed class MainHudSkillAssets
{
    private const string ControlAniPath = "ani/Control.ani";
    private const string SkillSectionName = "Progress42";
    private const int SkillFrameCount = 3;

    private readonly AniFrameSet? _skillFrames;

    private MainHudSkillAssets(AniFrameSet? skillFrames)
    {
        _skillFrames = skillFrames;
    }

    public bool HasSkill => _skillFrames is not null;

    public RgbaImage? GetSkillFrame(int frameIndex)
    {
        if ((uint)frameIndex >= SkillFrameCount)
        {
            throw new ArgumentOutOfRangeException(nameof(frameIndex), frameIndex, $"Frame index must be between 0 and {SkillFrameCount - 1}.");
        }

        return _skillFrames?.GetFrame((uint)frameIndex);
    }

    public static MainHudSkillAssets Load(IClientContentSource contentSource)
    {
        ArgumentNullException.ThrowIfNull(contentSource);

        AniIndexFile controlAni;

        try
        {
            controlAni = AniIndexFile.Load(contentSource, ControlAniPath, ContentLookupMode.LooseOnly);
        }
        catch (FileNotFoundException)
        {
            return new MainHudSkillAssets(null);
        }

        if (!controlAni.TryGetSection(SkillSectionName, out AniIndexSection? section))
        {
            return new MainHudSkillAssets(null);
        }

        if (section.FrameCount != SkillFrameCount)
        {
            throw new InvalidDataException($"ANI section [{SkillSectionName}] must contain exactly {SkillFrameCount} frames; found {section.FrameCount}.");
        }

        AniFrameSet frames;

        try
        {
            frames = AniFrameSetLoader.Load(contentSource, section, ContentLookupMode.LooseThenPackage);
        }
        catch (FileNotFoundException)
        {
            return new MainHudSkillAssets(null);
        }

        for (int frameIndex = 0; frameIndex < frames.FrameCount; frameIndex++)
        {
            RgbaImage frame = frames.GetFrame((uint)frameIndex);

            if (frame.Width != 128 || frame.Height != 128)
            {
                throw new InvalidDataException($"ANI section [{SkillSectionName}] frame {frameIndex} decoded as {frame.Width}x{frame.Height}; expected 128x128.");
            }
        }

        return new MainHudSkillAssets(frames);
    }
}
