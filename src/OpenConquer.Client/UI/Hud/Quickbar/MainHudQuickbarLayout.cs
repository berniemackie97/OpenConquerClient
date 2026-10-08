using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.UI.Hud.Quickbar;

internal readonly record struct MainHudQuickbarBounds
{
    public MainHudQuickbarBounds(int x, int y)
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

    public bool Contains(int x, int y) =>
        x >= X && y >= Y &&
        x < (long)X + MainHudQuickbarDefinition.Width &&
        y < (long)Y + MainHudQuickbarDefinition.Height;
}

internal readonly record struct MainHudQuickbarSlotBounds
{
    public MainHudQuickbarSlotBounds(int x, int y)
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

    public bool ContainsInputPoint(int x, int y) =>
        x >= X && y >= Y &&
        x < (long)X + MainHudQuickbarDefinition.HorizontalStride &&
        y < (long)Y + MainHudQuickbarDefinition.CellHeight;
}

internal readonly record struct MainHudQuickbarLayout
{
    private const int DialogHeight = 141;
    private readonly int _dialogOriginY;

    private MainHudQuickbarLayout(int dialogOriginY) => _dialogOriginY = dialogOriginY;

    public static MainHudQuickbarLayout Create(LogicalRenderSize logicalRenderSize)
    {
        bool supported = logicalRenderSize is { Width: 800, Height: 600 } or { Width: 1024, Height: 768 };

        if (!supported)
        {
            throw new ArgumentOutOfRangeException(nameof(logicalRenderSize), logicalRenderSize, "The native 5517 main HUD quickbar layout is only verified for 800x600 and 1024x768 logical rendering.");
        }

        return new MainHudQuickbarLayout(logicalRenderSize.Height - DialogHeight);
    }

    public MainHudQuickbarBounds GetBounds() => new(MainHudQuickbarDefinition.LocalX, _dialogOriginY + MainHudQuickbarDefinition.LocalY);

    public MainHudQuickbarSlotBounds GetSlotBounds(int slotIndex)
    {
        ValidateSlotIndex(slotIndex);
        MainHudQuickbarBounds bounds = GetBounds();
        return new MainHudQuickbarSlotBounds(bounds.X + slotIndex * MainHudQuickbarDefinition.HorizontalStride, bounds.Y);
    }

    public bool TryGetSlotIndex(int x, int y, out int slotIndex)
    {
        MainHudQuickbarBounds bounds = GetBounds();

        if (!bounds.Contains(x, y))
        {
            slotIndex = -1;
            return false;
        }

        slotIndex = (x - bounds.X) / MainHudQuickbarDefinition.HorizontalStride;
        return true;
    }

    private static void ValidateSlotIndex(int slotIndex)
    {
        if ((uint)slotIndex >= MainHudQuickbarDefinition.SlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(slotIndex), slotIndex, $"Slot index must be between 0 and {MainHudQuickbarDefinition.SlotCount - 1}.");
        }
    }
}
