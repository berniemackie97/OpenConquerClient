namespace OpenConquer.Client.UI.Hud.CheckControls;

internal enum MainHudCheckControlId
{
    Check40 = 0x3F4,
    Check43 = 0x3F7,
    Button411 = 0x3F8,
    Check46 = 0x3FF,
}

internal readonly record struct MainHudCheckControlDefinition
{
    public MainHudCheckControlDefinition(MainHudCheckControlId id, string aniSectionName, int localX, int localY)
    {
        if (!Enum.IsDefined(id))
        {
            throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown native main HUD check-control identifier.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(aniSectionName);
        ArgumentOutOfRangeException.ThrowIfNegative(localX);
        ArgumentOutOfRangeException.ThrowIfNegative(localY);

        Id = id;
        AniSectionName = aniSectionName;
        LocalX = localX;
        LocalY = localY;
    }

    public MainHudCheckControlId Id
    {
        get;
    }

    public int ControlId => (int)Id;

    public string AniSectionName
    {
        get;
    }

    public int LocalX
    {
        get;
    }

    public int LocalY
    {
        get;
    }
}

internal static class MainHudCheckControlDefinitions
{
    public const int Width = 22;
    public const int Height = 22;
    public const int FrameCount = 2;

    private static readonly MainHudCheckControlDefinition s_check40 = new(MainHudCheckControlId.Check40, "Check40", 0, 23);
    private static readonly MainHudCheckControlDefinition s_check43 = new(MainHudCheckControlId.Check43, "Check43", 72, 23);
    private static readonly MainHudCheckControlDefinition s_check46 = new(MainHudCheckControlId.Check46, "Check46", 50, 11);
    private static readonly MainHudCheckControlDefinition s_button411 = new(MainHudCheckControlId.Button411, "Button411", 22, 11);

    private static readonly MainHudCheckControlDefinition[] s_nativeDrawOrder =
    [
        s_check40,
        s_check43,
        s_check46,
        s_button411,
    ];

    public static ReadOnlySpan<MainHudCheckControlDefinition> NativeDrawOrder => s_nativeDrawOrder;

    public static MainHudCheckControlDefinition Get(MainHudCheckControlId id)
    {
        return id switch
        {
            MainHudCheckControlId.Check40 => s_check40,
            MainHudCheckControlId.Check43 => s_check43,
            MainHudCheckControlId.Check46 => s_check46,
            MainHudCheckControlId.Button411 => s_button411,
            _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown native main HUD check-control identifier."),
        };
    }
}
