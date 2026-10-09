namespace OpenConquer.Client.UI.Hud.StatusHints;

internal sealed class MainHudStatusHintState
{
    public const int NoHoveredHotspot = 0xFF;
    public const int MagicCategory = 9;

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

    public bool CanRender => IsVisible && !SkillDragActive && Category == MainHudStatusHintDefinition.Category && Kind is >= MainHudStatusHintKind.WalkRun and <= MainHudStatusHintKind.Life;

    public bool CanRenderMagic => IsVisible && !SkillDragActive && Category == MagicCategory;

    public void SetSkillDragActive(bool active) => SkillDragActive = active;

    public void Select(int hotspot)
    {
        if ((uint)hotspot >= MainHudStatusHintDefinition.SlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(hotspot), hotspot, "Unknown native main HUD hotspot.");
        }

        HoveredHotspot = hotspot;
        Kind = (MainHudStatusHintKind)hotspot;
        Category = MainHudStatusHintDefinition.Category;
        IsVisible = true;
    }

    public void SetMagicAnchor(int logicalX, int gridY)
    {
        int nativeX = unchecked((short)logicalX);
        MagicAnchorX = Math.Max(0, nativeX);
        MagicStoredAnchorY = unchecked((ushort)gridY + 42);
    }

    public void SelectMagic(uint fullType)
    {
        MagicType = fullType;
        Category = MagicCategory;
        HoveredHotspot = NoHoveredHotspot;
        Kind = MainHudStatusHintKind.None;
        IsVisible = true;
    }

    public void Clear()
    {
        HoveredHotspot = NoHoveredHotspot;
        Kind = MainHudStatusHintKind.None;
        IsVisible = false;
    }
}
