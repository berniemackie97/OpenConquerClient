namespace OpenConquer.Client.UI.Hud;

internal sealed class MainHudStatusHintState
{
    public const int NoHoveredHotspot = 0xFF;

    public int HoveredHotspot { get; private set; } = NoHoveredHotspot;

    public MainHudStatusHintKind Kind
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

    public bool CanRender =>
        IsVisible &&
        !SkillDragActive &&
        Kind is >= MainHudStatusHintKind.WalkRun and <= MainHudStatusHintKind.Life;

    public void SetSkillDragActive(bool active) => SkillDragActive = active;

    public void Select(int hotspot)
    {
        if ((uint)hotspot >= MainHudStatusHintDefinition.SlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(hotspot), hotspot, "Unknown native main HUD hotspot.");
        }

        HoveredHotspot = hotspot;
        Kind = (MainHudStatusHintKind)hotspot;
        IsVisible = true;
    }

    public void Clear()
    {
        HoveredHotspot = NoHoveredHotspot;
        Kind = MainHudStatusHintKind.None;
        IsVisible = false;
    }
}
