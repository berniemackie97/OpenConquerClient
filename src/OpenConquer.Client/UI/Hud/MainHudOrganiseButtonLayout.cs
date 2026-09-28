using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.UI.Hud;

internal readonly record struct MainHudOrganiseButtonLayout
{
    private const int DialogHeight = 141;
    private const int ControlX = 702;
    private const int ControlY = 94;
    private const int ControlWidth = 46;
    private const int ControlHeight = 22;

    private MainHudOrganiseButtonLayout(int x, int y, int hitWidth, int hitHeight)
    {
        X = x;
        Y = y;
        HitWidth = hitWidth;
        HitHeight = hitHeight;
    }

    public int X
    {
        get;
    }

    public int Y
    {
        get;
    }

    public int HitWidth
    {
        get;
    }

    public int HitHeight
    {
        get;
    }

    public static MainHudOrganiseButtonLayout Create(LogicalRenderSize logicalRenderSize)
    {
        bool supported = logicalRenderSize is { Width: 800, Height: 600 } or { Width: 1024, Height: 768 };

        if (!supported)
        {
            throw new ArgumentOutOfRangeException(nameof(logicalRenderSize), logicalRenderSize,
                "The native 5517 main HUD organise-button layout is only verified for 800x600 and 1024x768 logical rendering.");
        }

        int dialogOriginY = logicalRenderSize.Height - DialogHeight;

        return new MainHudOrganiseButtonLayout(
            ControlX,
            dialogOriginY + ControlY,
            ControlWidth,
            ControlHeight);
    }
}
