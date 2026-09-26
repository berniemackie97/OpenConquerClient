using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.UI.Hud;

internal readonly record struct MainHudVitalsLayout
{
    public const int LifeX = 4;
    public const int ManaX = 52;
    public const int StaminaX = 42;
    public const int ExtendedStaminaX = 42;

    private MainHudVitalsLayout(int originY)
    {
        OriginY = originY;
    }

    public int OriginY
    {
        get;
    }
    public int LifeY => OriginY + 54;
    public int ManaY => OriginY + 54;
    public int StaminaY => OriginY + 58;
    public int ExtendedStaminaY => OriginY + 52;

    public static MainHudVitalsLayout Create(LogicalRenderSize logicalRenderSize)
    {
        bool supported = logicalRenderSize is { Width: 800, Height: 600 } or { Width: 1024, Height: 768 };

        if (!supported)
        {
            throw new ArgumentOutOfRangeException(nameof(logicalRenderSize), logicalRenderSize, "The native 5517 main HUD vitals layout is only verified for 800x600 and 1024x768 logical rendering.");
        }

        return new MainHudVitalsLayout(logicalRenderSize.Height - 141);
    }
}
