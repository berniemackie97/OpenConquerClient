using OpenConquer.Rendering.Presentation;
using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Client.UI.Hud;

internal readonly record struct MainHudChromeLayout
{
    public const int BackgroundX = 0;
    public const int DialogPanelAX = 0;
    public const int DialogPanelBX = 256;
    public const int DialogPanelCX = 512;
    public const int DialogPanelDX = 768;

    public static readonly SpriteSourceRectangle DialogPanelASource = new(0, 112, 256, 144);
    public static readonly SpriteSourceRectangle DialogPanelBSource = new(0, 0, 256, 54);
    public static readonly SpriteSourceRectangle DialogPanelCSource = new(0, 0, 256, 54);
    public static readonly SpriteSourceRectangle DialogPanelDSource = new(0, 64, 256, 54);

    private MainHudChromeLayout(int originY)
    {
        OriginY = originY;
    }

    public int OriginY
    {
        get;
    }
    public int BackgroundY => OriginY;
    public int DialogPanelAY => OriginY - 3;
    public int DialogPanelBY => OriginY + 88;
    public int DialogPanelCY => OriginY + 88;
    public int DialogPanelDY => OriginY + 88;

    public static MainHudChromeLayout Create(LogicalRenderSize logicalRenderSize)
    {
        bool supported = logicalRenderSize is { Width: 800, Height: 600 } or { Width: 1024, Height: 768 };

        if (!supported)
        {
            throw new ArgumentOutOfRangeException(nameof(logicalRenderSize), logicalRenderSize, "The native 5517 main HUD layout is only verified for 800x600 and 1024x768 logical rendering.");
        }

        return new MainHudChromeLayout(logicalRenderSize.Height - 141);
    }
}
