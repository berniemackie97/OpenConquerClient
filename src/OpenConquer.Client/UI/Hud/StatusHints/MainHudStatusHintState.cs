namespace OpenConquer.Client.UI.Hud.StatusHints;

internal sealed class MainHudStatusHintState
{
    public const int NoHoveredHotspot = 0xFF;
    public const int MagicCategory = 9;
    public const int DanceCategory = 0x38;
    public const int EquipmentSwitchCategory = 0x6A;

    public int HoveredHotspot { get; private set; } = NoHoveredHotspot;
    public MainHudStatusHintKind Kind
    {
        get; private set;
    }
    public int Category { get; private set; } = MainHudStatusHintDefinition.Category;
    public uint MagicType
    {
        get; private set;
    }
    public uint ContextParameter
    {
        get; private set;
    }
    public int MagicAnchorX
    {
        get; private set;
    }
    public int MagicStoredAnchorY
    {
        get; private set;
    }
    public bool IsVisible
    {
        get; private set;
    }
    public bool SkillDragActive
    {
        get; private set;
    }

    public bool CanRender => CanShow(MainHudStatusHintDefinition.Category) && Kind is >= MainHudStatusHintKind.WalkRun and <= MainHudStatusHintKind.Life;
    public bool CanRenderMagic => CanShow(MagicCategory);
    public bool CanRenderDance => CanShow(DanceCategory);
    public bool CanRenderEquipmentSwitch => CanShow(EquipmentSwitchCategory);

    public void SetSkillDragActive(bool active) => SkillDragActive = active;

    public void Select(int hotspot)
    {
        if ((uint)hotspot >= MainHudStatusHintDefinition.SlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(hotspot), hotspot, "Unknown main HUD hotspot.");
        }

        HoveredHotspot = hotspot;
        Kind = (MainHudStatusHintKind)hotspot;
        Category = MainHudStatusHintDefinition.Category;
        IsVisible = true;
    }

    public void SetMagicAnchor(int logicalX, int gridY)
    {
        MagicAnchorX = Math.Max(0, (int)unchecked((short)logicalX));
        MagicStoredAnchorY = unchecked((ushort)gridY + 42);
    }

    public void SelectMagic(uint fullType)
    {
        MagicType = fullType;
        SelectContext(MagicCategory, fullType);
    }

    public void SelectDance(uint type) => SelectContext(DanceCategory, type);
    public void SelectEquipmentSwitch(uint context) => SelectContext(EquipmentSwitchCategory, context);

    public void Clear()
    {
        HoveredHotspot = NoHoveredHotspot;
        Kind = MainHudStatusHintKind.None;
        IsVisible = false;
    }

    private bool CanShow(int category) => IsVisible && !SkillDragActive && Category == category;

    private void SelectContext(int category, uint parameter)
    {
        Category = category;
        ContextParameter = parameter;
        HoveredHotspot = NoHoveredHotspot;
        Kind = MainHudStatusHintKind.None;
        IsVisible = true;
    }
}
