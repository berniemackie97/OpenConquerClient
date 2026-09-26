using OpenConquer.Content;
using OpenConquer.Content.Ani;
using OpenConquer.Content.Images;

namespace OpenConquer.Client.UI.Hud;

internal sealed class MainHudVitalsAssets
{
    private const string ControlAniPath = "ani/Control.ani";
    private const string LifeSectionName = "Progress40";
    private const string ManaSectionName = "Progress41";
    private const string StaminaSectionName = "Progress46";
    private const string ExtendedStaminaSectionName = "Progress47";

    private const int LifeFrameCount = 3;
    private const int ManaFrameCount = 3;
    private const int StaminaFrameCount = 2;
    private const int ExtendedStaminaFrameCount = 2;

    private readonly AniFrameSet? _lifeFrames;
    private readonly AniFrameSet? _manaFrames;
    private readonly AniFrameSet? _staminaFrames;
    private readonly AniFrameSet? _extendedStaminaFrames;

    private MainHudVitalsAssets(AniFrameSet? lifeFrames, AniFrameSet? manaFrames, AniFrameSet? staminaFrames, AniFrameSet? extendedStaminaFrames)
    {
        _lifeFrames = lifeFrames;
        _manaFrames = manaFrames;
        _staminaFrames = staminaFrames;
        _extendedStaminaFrames = extendedStaminaFrames;
    }

    public bool HasLife => _lifeFrames is not null;
    public bool HasMana => _manaFrames is not null;
    public bool HasStamina => _staminaFrames is not null;
    public bool HasExtendedStamina => _extendedStaminaFrames is not null;

    public RgbaImage? GetLifeFrame(int frameIndex) => GetFrame(_lifeFrames, frameIndex, LifeFrameCount);
    public RgbaImage? GetManaFrame(int frameIndex) => GetFrame(_manaFrames, frameIndex, ManaFrameCount);
    public RgbaImage? GetStaminaFrame(int frameIndex) => GetFrame(_staminaFrames, frameIndex, StaminaFrameCount);
    public RgbaImage? GetExtendedStaminaFrame(int frameIndex) => GetFrame(_extendedStaminaFrames, frameIndex, ExtendedStaminaFrameCount);

    public static MainHudVitalsAssets Load(IClientContentSource contentSource)
    {
        ArgumentNullException.ThrowIfNull(contentSource);

        AniIndexFile controlAni;

        try
        {
            controlAni = AniIndexFile.Load(contentSource, ControlAniPath, ContentLookupMode.LooseOnly);
        }
        catch (FileNotFoundException)
        {
            return new MainHudVitalsAssets(null, null, null, null);
        }

        AniFrameSet? lifeFrames = LoadGaugeFrames(contentSource, controlAni, LifeSectionName, LifeFrameCount, 128, 128);
        AniFrameSet? manaFrames = LoadGaugeFrames(contentSource, controlAni, ManaSectionName, ManaFrameCount, 128, 128);
        AniFrameSet? staminaFrames = LoadGaugeFrames(contentSource, controlAni, StaminaSectionName, StaminaFrameCount, 128, 128);
        AniFrameSet? extendedStaminaFrames = LoadGaugeFrames(contentSource, controlAni, ExtendedStaminaSectionName, ExtendedStaminaFrameCount, 32, 32);

        return new MainHudVitalsAssets(lifeFrames, manaFrames, staminaFrames, extendedStaminaFrames);
    }

    private static AniFrameSet? LoadGaugeFrames(IClientContentSource contentSource, AniIndexFile controlAni, string sectionName, int expectedFrameCount, int expectedWidth, int expectedHeight)
    {
        if (!controlAni.TryGetSection(sectionName, out AniIndexSection? section))
        {
            return null;
        }

        if (section.FrameCount != expectedFrameCount)
        {
            throw new InvalidDataException($"ANI section [{sectionName}] must contain exactly {expectedFrameCount} frames; found {section.FrameCount}.");
        }

        AniFrameSet frames;

        try
        {
            frames = AniFrameSetLoader.Load(contentSource, section, ContentLookupMode.LooseThenPackage);
        }
        catch (FileNotFoundException)
        {
            return null;
        }

        for (int frameIndex = 0; frameIndex < frames.FrameCount; frameIndex++)
        {
            ValidateDimensions(frames.GetFrame((uint)frameIndex), sectionName, frameIndex, expectedWidth, expectedHeight);
        }

        return frames;
    }

    private static RgbaImage? GetFrame(AniFrameSet? frames, int frameIndex, int expectedFrameCount)
    {
        if ((uint)frameIndex >= (uint)expectedFrameCount)
        {
            throw new ArgumentOutOfRangeException(nameof(frameIndex), frameIndex, $"Frame index must be between 0 and {expectedFrameCount - 1}.");
        }

        return frames?.GetFrame((uint)frameIndex);
    }

    private static void ValidateDimensions(RgbaImage image, string sectionName, int frameIndex, int expectedWidth, int expectedHeight)
    {
        if (image.Width != expectedWidth || image.Height != expectedHeight)
        {
            throw new InvalidDataException($"ANI section [{sectionName}] frame {frameIndex} decoded as {image.Width}x{image.Height}; expected {expectedWidth}x{expectedHeight}.");
        }
    }
}
