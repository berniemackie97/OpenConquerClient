using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.UI.Hud.SelectedSkill;

internal readonly record struct MainHudSelectedSkillBounds
{
    public MainHudSelectedSkillBounds(int x, int y)
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
    public static int Width => MainHudSelectedSkillDefinition.Width;
    public static int Height => MainHudSelectedSkillDefinition.Height;

    public bool Contains(int x, int y) =>
        x >= X && y >= Y &&
        (long)x < (long)X + Width &&
        (long)y < (long)Y + Height;
}

internal readonly record struct MainHudSelectedSkillLayout
{
    private const int DialogHeight = 141;

    private readonly int _dialogOriginY;

    private MainHudSelectedSkillLayout(int dialogOriginY) => _dialogOriginY = dialogOriginY;

    public static MainHudSelectedSkillLayout Create(LogicalRenderSize logicalRenderSize)
    {
        bool supported = logicalRenderSize is { Width: 800, Height: 600 } or { Width: 1024, Height: 768 };

        if (!supported)
        {
            throw new ArgumentOutOfRangeException(nameof(logicalRenderSize), logicalRenderSize, "The native 5517 main HUD selected-skill layout is only verified for 800x600 and 1024x768 logical rendering.");
        }

        return new MainHudSelectedSkillLayout(logicalRenderSize.Height - DialogHeight);
    }

    public MainHudSelectedSkillBounds GetBounds() =>
        new(MainHudSelectedSkillDefinition.LocalX, _dialogOriginY + MainHudSelectedSkillDefinition.LocalY);
}
