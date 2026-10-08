namespace OpenConquer.Client.UI.Hud;

internal enum MainHudStatusHintKind : byte
{
    None = 0,
    WalkRun = 1,
    Chat = 2,
    Map = 3,
    ScreenShift = 4,
    Equipment = 5,
    Skill = 6,
    Mana = 7,
    Life = 8,
}

internal static class MainHudStatusHintDefinition
{
    public const int Category = 8;
    public const int SlotCount = 9;
    public const int RegionCount = 3;
    public const int MainDialogHeight = 141;
    public const int AnchorOffsetY = -20;

    public const int Dialog21FrameCount = 1;
    public const int Dialog21SourceWidth = 100;
    public const int Dialog21SourceHeight = 200;
    public const int Dialog21TextureWidth = 256;
    public const int Dialog21TextureHeight = 256;

    public const int WalkRunStringId = 0x2756;
    public const int ChatStringId = 0x2757;
    public const int MapStringId = 0x2758;
    public const int ScreenShiftOffStringId = 0x2759;
    public const int ScreenShiftOnStringId = 0x275A;
    public const int EquipmentStringId = 0x2882;

    public const int SkillMaximum = 100;
}
