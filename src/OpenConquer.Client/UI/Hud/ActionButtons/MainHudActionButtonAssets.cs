using OpenConquer.Content;
using OpenConquer.Content.Ani;
using OpenConquer.Content.Images;

namespace OpenConquer.Client.UI.Hud.ActionButtons;

internal sealed class MainHudActionButtonAssets
{
    private const string ControlAniPath = "ani/Control.ani";
    private const int FrameWidth = 64;
    private const int FrameHeight = 32;
    private const int PkFrameCount = 2;

    private readonly Dictionary<string, AniFrameSet?> _frameSets;

    private MainHudActionButtonAssets(Dictionary<string, AniFrameSet?> frameSets) => _frameSets = frameSets;

    public bool IsAvailable(MainHudActionButtonId id)
    {
        MainHudActionButtonDefinition definition = MainHudActionButtonDefinitions.Get(id);
        return _frameSets.GetValueOrDefault(definition.AniSectionName) is not null;
    }

    public RgbaImage? GetFrame(MainHudActionButtonId id, int frameIndex)
    {
        ValidateButtonFrameIndex(frameIndex);
        MainHudActionButtonDefinition definition = MainHudActionButtonDefinitions.Get(id);
        return _frameSets.GetValueOrDefault(definition.AniSectionName)?.GetFrame((uint)frameIndex);
    }

    public bool IsPkSkinAvailable(MainHudPkButtonSkin skin) => _frameSets.GetValueOrDefault(MainHudPkButtonSkins.GetAniSectionName(skin)) is not null;

    public RgbaImage? GetPkFrame(MainHudPkButtonSkin skin, int frameIndex)
    {
        ValidateButtonFrameIndex(frameIndex);
        return _frameSets.GetValueOrDefault(MainHudPkButtonSkins.GetAniSectionName(skin))?.GetFrame((uint)frameIndex);
    }

    public static MainHudActionButtonAssets Load(IClientContentSource contentSource)
    {
        ArgumentNullException.ThrowIfNull(contentSource);

        AniIndexFile controlAni;

        try
        {
            controlAni = AniIndexFile.Load(contentSource, ControlAniPath, ContentLookupMode.LooseOnly);
        }
        catch (FileNotFoundException)
        {
            return new MainHudActionButtonAssets(new Dictionary<string, AniFrameSet?>(StringComparer.Ordinal));
        }

        Dictionary<string, AniFrameSet?> frameSets = new(StringComparer.Ordinal);

        foreach (MainHudActionButtonDefinition definition in MainHudActionButtonDefinitions.NativeDrawOrder)
        {
            frameSets[definition.AniSectionName] = LoadSection(contentSource, controlAni, definition.AniSectionName, definition.ExpectedFrameCount);
        }

        foreach (MainHudPkButtonSkin skin in Enum.GetValues<MainHudPkButtonSkin>())
        {
            string sectionName = MainHudPkButtonSkins.GetAniSectionName(skin);
            if (!frameSets.ContainsKey(sectionName))
            {
                frameSets[sectionName] = LoadSection(contentSource, controlAni, sectionName, PkFrameCount);
            }
        }

        return new MainHudActionButtonAssets(frameSets);
    }

    private static AniFrameSet? LoadSection(IClientContentSource contentSource, AniIndexFile controlAni, string sectionName, int expectedFrameCount)
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
            RgbaImage frame = frames.GetFrame((uint)frameIndex);
            if (frame.Width != FrameWidth || frame.Height != FrameHeight)
            {
                throw new InvalidDataException($"ANI section [{sectionName}] frame {frameIndex} decoded as {frame.Width}x{frame.Height}; expected {FrameWidth}x{FrameHeight}.");
            }
        }

        return frames;
    }

    private static void ValidateButtonFrameIndex(int frameIndex)
    {
        if ((uint)frameIndex > MainHudActionButtonState.HoverFrame)
        {
            throw new ArgumentOutOfRangeException(nameof(frameIndex), frameIndex, $"Frame index must be between {MainHudActionButtonState.NormalFrame} and {MainHudActionButtonState.HoverFrame}.");
        }
    }
}
