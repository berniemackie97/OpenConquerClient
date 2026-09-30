namespace OpenConquer.Client.UI.Hud;

internal enum MainHudPkButtonSkin
{
    Button47,
    Button49,
    Button48,
    Button412,
}

internal static class MainHudPkButtonSkins
{
    public static string GetAniSectionName(MainHudPkButtonSkin skin) => skin switch
    {
        MainHudPkButtonSkin.Button47 => "Button47",
        MainHudPkButtonSkin.Button49 => "Button49",
        MainHudPkButtonSkin.Button48 => "Button48",
        MainHudPkButtonSkin.Button412 => "Button412",
        _ => throw new ArgumentOutOfRangeException(nameof(skin), skin, "Unknown native PK-button skin."),
    };
}
