using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.UI.Hud.CheckControls;

internal readonly record struct MainHudCheckControlBounds
{
    public MainHudCheckControlBounds(int x, int y)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfNegative(y);

        X = x;
        Y = y;
    }

    public int X
    {
        get;
    }

    public int Y
    {
        get;
    }

    public bool Contains(int x, int y)
    {
        return x >= X && y >= Y && (long)x < (long)X + MainHudCheckControlDefinitions.Width && (long)y < (long)Y + MainHudCheckControlDefinitions.Height;
    }
}

internal readonly record struct MainHudCheckControlLayout
{
    private const int DialogHeight = 141;

    private readonly int _dialogOriginY;

    private MainHudCheckControlLayout(int dialogOriginY)
    {
        _dialogOriginY = dialogOriginY;
    }

    public static MainHudCheckControlLayout Create(LogicalRenderSize logicalRenderSize)
    {
        bool supported = logicalRenderSize is { Width: 800, Height: 600 } or { Width: 1024, Height: 768 };

        if (!supported)
        {
            throw new ArgumentOutOfRangeException(nameof(logicalRenderSize), logicalRenderSize, "The native 5517 main HUD check-control layout is only verified for 800x600 and 1024x768 logical rendering.");
        }

        return new MainHudCheckControlLayout(logicalRenderSize.Height - DialogHeight);
    }

    public MainHudCheckControlBounds GetBounds(MainHudCheckControlId id)
    {
        MainHudCheckControlDefinition definition = MainHudCheckControlDefinitions.Get(id);
        return new MainHudCheckControlBounds(definition.LocalX, _dialogOriginY + definition.LocalY);
    }
}
