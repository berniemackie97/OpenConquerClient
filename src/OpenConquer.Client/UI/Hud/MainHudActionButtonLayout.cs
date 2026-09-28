using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.UI.Hud;

internal readonly record struct MainHudActionButtonBounds
{
    public MainHudActionButtonBounds(int x, int y, int hitWidth, int hitHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hitWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hitHeight);

        X = x;
        Y = y;
        HitWidth = hitWidth;
        HitHeight = hitHeight;
    }

    public int X { get; }
    public int Y { get; }
    public int HitWidth { get; }
    public int HitHeight { get; }

    public bool Contains(int x, int y)
    {
        return x >= X && y >= Y && (long)x < (long)X + HitWidth && (long)y < (long)Y + HitHeight;
    }
}

internal readonly record struct MainHudActionButtonLayout
{
    private const int DialogHeight = 141;

    private readonly int _dialogOriginY;

    private MainHudActionButtonLayout(int dialogOriginY)
    {
        _dialogOriginY = dialogOriginY;
    }

    public static MainHudActionButtonLayout Create(LogicalRenderSize logicalRenderSize)
    {
        bool supported =
            logicalRenderSize is { Width: 800, Height: 600 } or { Width: 1024, Height: 768 };

        if (!supported)
        {
            throw new ArgumentOutOfRangeException(
                nameof(logicalRenderSize),
                logicalRenderSize,
                "The native 5517 main HUD action-button layout is only verified for 800x600 and 1024x768 logical rendering."
            );
        }

        return new MainHudActionButtonLayout(logicalRenderSize.Height - DialogHeight);
    }

    public MainHudActionButtonBounds GetBounds(MainHudActionButtonId id)
    {
        MainHudActionButtonDefinition definition = MainHudActionButtonDefinitions.Get(id);

        return new MainHudActionButtonBounds(
            definition.LocalX,
            _dialogOriginY + definition.LocalY,
            definition.HitWidth,
            definition.HitHeight
        );
    }
}
