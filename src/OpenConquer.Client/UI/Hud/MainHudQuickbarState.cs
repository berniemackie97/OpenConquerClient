namespace OpenConquer.Client.UI.Hud;

internal sealed class MainHudQuickbarState
{
    public MainHudQuickbarSlotsState Slots { get; } = new();
    public bool InteractionEnabled { get; private set; } = true;
    public bool PickupEnabled { get; private set; } = true;

    public bool IsHoverActive
    {
        get; private set;
    }

    public int HoveredColumnOneBased
    {
        get; private set;
    }

    public int HoveredRowOneBased
    {
        get; private set;
    }

    public uint HoveredPayload
    {
        get; private set;
    }

    public uint HoveredContentId
    {
        get; private set;
    }

    public void SetInteractionEnabled(bool enabled) => InteractionEnabled = enabled;
    public void SetPickupEnabled(bool enabled) => PickupEnabled = enabled;

    public void SetHover(int columnOneBased, int rowOneBased, uint payload, uint contentId)
    {
        HoveredColumnOneBased = columnOneBased;
        HoveredRowOneBased = rowOneBased;
        HoveredPayload = payload;
        HoveredContentId = contentId;
        IsHoverActive = true;
    }

    public void SetHoveredCoordinates(int columnOneBased, int rowOneBased)
    {
        HoveredColumnOneBased = columnOneBased;
        HoveredRowOneBased = rowOneBased;
    }

    public void ClearHover()
    {
        IsHoverActive = false;
        HoveredColumnOneBased = 0;
        HoveredRowOneBased = 0;
        HoveredPayload = 0;
        HoveredContentId = 0;
    }
}
