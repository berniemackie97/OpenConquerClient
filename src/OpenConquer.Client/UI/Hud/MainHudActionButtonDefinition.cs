namespace OpenConquer.Client.UI.Hud;

internal enum MainHudActionButtonId
{
    Button410 = 0x3EE,
    Button42 = 0x3EF,
    Button43 = 0x3F0,
    Main3MissionBtn = 0x3F1,
    Button45 = 0x3F2,
    Button46 = 0x3F3,
    Button47 = 0x3F5,
    Main3OrganiseBtn = 0x400,
    Button41 = 0x402,
    Button40 = 0x5DF,
}

internal readonly record struct MainHudActionButtonDefinition
{
    public MainHudActionButtonDefinition(MainHudActionButtonId id, string aniSectionName, int localX, int localY, int hitWidth, int hitHeight, int expectedFrameCount)
    {
        if (!Enum.IsDefined(id))
        {
            throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown native main HUD action-button identifier.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(aniSectionName);
        ArgumentOutOfRangeException.ThrowIfNegative(localX);
        ArgumentOutOfRangeException.ThrowIfNegative(localY);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hitWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(hitHeight);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedFrameCount);

        Id = id;
        AniSectionName = aniSectionName;
        LocalX = localX;
        LocalY = localY;
        HitWidth = hitWidth;
        HitHeight = hitHeight;
        ExpectedFrameCount = expectedFrameCount;
    }

    public MainHudActionButtonId Id
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

    public int HitWidth
    {
        get;
    }

    public int HitHeight
    {
        get;
    }

    public int ExpectedFrameCount
    {
        get;
    }
}

internal static class MainHudActionButtonDefinitions
{
    private static readonly MainHudActionButtonDefinition s_button40 = new(MainHudActionButtonId.Button40, "Button40", 502, 94, 46, 22, 2);

    private static readonly MainHudActionButtonDefinition s_button410 = new(MainHudActionButtonId.Button410, "Button410", 702, 119, 46, 22, 2);

    private static readonly MainHudActionButtonDefinition s_button42 = new(MainHudActionButtonId.Button42, "Button42", 552, 94, 46, 22, 2);

    private static readonly MainHudActionButtonDefinition s_button43 = new(MainHudActionButtonId.Button43, "Button43", 652, 119, 46, 22, 2);

    private static readonly MainHudActionButtonDefinition s_main3MissionBtn = new(MainHudActionButtonId.Main3MissionBtn, "Main3_MissionBtn", 502, 119, 46, 22, 3);

    private static readonly MainHudActionButtonDefinition s_button45 = new(MainHudActionButtonId.Button45, "Button45", 552, 119, 46, 22, 2);

    private static readonly MainHudActionButtonDefinition s_button46 = new(MainHudActionButtonId.Button46, "Button46", 602, 119, 46, 22, 2);

    private static readonly MainHudActionButtonDefinition s_button47 = new(MainHudActionButtonId.Button47, "Button47", 652, 94, 46, 22, 2);

    private static readonly MainHudActionButtonDefinition s_main3OrganiseBtn = new(MainHudActionButtonId.Main3OrganiseBtn, "Main3_OrganiseBtn", 702, 94, 46, 22, 4);

    private static readonly MainHudActionButtonDefinition s_button41 = new(MainHudActionButtonId.Button41, "Button41", 602, 94, 46, 22, 3);

    private static readonly MainHudActionButtonDefinition[] s_nativeDrawOrder =
    [
        s_button40,
        s_button410,
        s_button42,
        s_button43,
        s_main3MissionBtn,
        s_button45,
        s_button46,
        s_button47,
        s_main3OrganiseBtn,
        s_button41,
    ];

    public static ReadOnlySpan<MainHudActionButtonDefinition> NativeDrawOrder => s_nativeDrawOrder;

    public static MainHudActionButtonDefinition Get(MainHudActionButtonId id)
    {
        return id switch
        {
            MainHudActionButtonId.Button40 => s_button40,
            MainHudActionButtonId.Button410 => s_button410,
            MainHudActionButtonId.Button42 => s_button42,
            MainHudActionButtonId.Button43 => s_button43,
            MainHudActionButtonId.Main3MissionBtn => s_main3MissionBtn,
            MainHudActionButtonId.Button45 => s_button45,
            MainHudActionButtonId.Button46 => s_button46,
            MainHudActionButtonId.Button47 => s_button47,
            MainHudActionButtonId.Main3OrganiseBtn => s_main3OrganiseBtn,
            MainHudActionButtonId.Button41 => s_button41,
            _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown native main HUD action-button identifier."),
        };
    }
}
