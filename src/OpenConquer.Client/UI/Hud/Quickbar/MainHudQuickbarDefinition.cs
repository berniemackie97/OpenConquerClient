namespace OpenConquer.Client.UI.Hud.Quickbar;

internal enum MainHudQuickbarContentKind : byte
{
    Item = 1,
    Action = 2,
    Magic = 3,
    XpMagic = 4,
    Dance = 5,
    WeaponSwap = 6,
}

internal static class MainHudQuickbarDefinition
{
    public const int ControlId = 0x3FD;
    public const int RowCount = 1;
    public const int ColumnCount = 10;
    public const int SlotCount = RowCount * ColumnCount;
    public const int NativeContext = 3;
    public const int LocalX = 90;
    public const int LocalY = 98;
    public const int CellWidth = 40;
    public const int CellHeight = 40;
    public const int HorizontalStride = 41;
    public const int Width = ColumnCount * HorizontalStride;
    public const int Height = CellHeight;
    public const string CoverAniSectionName = "Compose_CoverPic";
    public const int CoverFrameCount = 1;
}
