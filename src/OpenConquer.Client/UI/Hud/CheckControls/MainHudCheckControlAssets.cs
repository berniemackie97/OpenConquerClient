using OpenConquer.Content;
using OpenConquer.Content.Ani;
using OpenConquer.Content.Images;

namespace OpenConquer.Client.UI.Hud.CheckControls;

internal sealed class MainHudCheckControlAssets
{
    private const string ControlAniPath = "ani/Control.ani";
    private const int FrameWidth = 32;
    private const int FrameHeight = 32;

    private readonly Dictionary<string, AniFrameSet?> _frameSets;

    private MainHudCheckControlAssets(Dictionary<string, AniFrameSet?> frameSets)
    {
        _frameSets = frameSets;
    }

    public bool IsAvailable(MainHudCheckControlId id)
    {
        MainHudCheckControlDefinition definition = MainHudCheckControlDefinitions.Get(id);
        return _frameSets.GetValueOrDefault(definition.AniSectionName) is not null;
    }

    public RgbaImage? GetFrame(MainHudCheckControlId id, int frameIndex)
    {
        ValidateFrameIndex(frameIndex);

        MainHudCheckControlDefinition definition = MainHudCheckControlDefinitions.Get(id);
        return _frameSets.GetValueOrDefault(definition.AniSectionName)?.GetFrame((uint)frameIndex);
    }

    public static MainHudCheckControlAssets Load(IClientContentSource contentSource)
    {
        ArgumentNullException.ThrowIfNull(contentSource);

        AniIndexFile controlAni;

        try
        {
            controlAni = AniIndexFile.Load(contentSource, ControlAniPath, ContentLookupMode.LooseOnly);
        }
        catch (FileNotFoundException)
        {
            return new MainHudCheckControlAssets(new Dictionary<string, AniFrameSet?>(StringComparer.Ordinal));
        }

        Dictionary<string, AniFrameSet?> frameSets = new(StringComparer.Ordinal);

        foreach (MainHudCheckControlDefinition definition in MainHudCheckControlDefinitions.NativeDrawOrder)
        {
            frameSets[definition.AniSectionName] = LoadSection(contentSource, controlAni, definition.AniSectionName);
        }

        return new MainHudCheckControlAssets(frameSets);
    }

    private static AniFrameSet? LoadSection(IClientContentSource contentSource, AniIndexFile controlAni, string sectionName)
    {
        if (!controlAni.TryGetSection(sectionName, out AniIndexSection? section))
        {
            return null;
        }

        if (section.FrameCount != MainHudCheckControlDefinitions.FrameCount)
        {
            throw new InvalidDataException($"ANI section [{sectionName}] must contain exactly {MainHudCheckControlDefinitions.FrameCount} frames; found {section.FrameCount}.");
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

    private static void ValidateFrameIndex(int frameIndex)
    {
        if ((uint)frameIndex >= MainHudCheckControlDefinitions.FrameCount)
        {
            throw new ArgumentOutOfRangeException(nameof(frameIndex), frameIndex, $"Frame index must be between 0 and {MainHudCheckControlDefinitions.FrameCount - 1}.");
        }
    }
}
