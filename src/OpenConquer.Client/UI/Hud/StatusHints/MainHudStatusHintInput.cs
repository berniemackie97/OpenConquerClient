using OpenConquer.Content.Regions;

namespace OpenConquer.Client.UI.Hud.StatusHints;

internal sealed class MainHudStatusHintInput
{
    private readonly MainHudStatusHintState _state;
    private readonly MainHudStatusHintLayout _layout;
    private readonly ClientRegionFile[] _regions;
    private readonly bool _alternateLayout;

    public MainHudStatusHintInput(MainHudStatusHintState state, MainHudStatusHintLayout layout, ClientRegionFile skillRegion, ClientRegionFile manaRegion, ClientRegionFile lifeRegion, bool alternateLayout)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(skillRegion);
        ArgumentNullException.ThrowIfNull(manaRegion);
        ArgumentNullException.ThrowIfNull(lifeRegion);

        _state = state;
        _layout = layout;
        _regions = [skillRegion, manaRegion, lifeRegion];
        _alternateLayout = alternateLayout;
    }

    public void HandlePointerMoved(int logicalX, int logicalY)
    {
        (int x, int y) = _layout.ToDialogLocal(logicalX, logicalY);
        int hovered = _state.HoveredHotspot;

        if (hovered == MainHudStatusHintState.NoHoveredHotspot)
        {
            for (int index = 0; index < 6; index++)
            {
                if (!MainHudStatusHintLayout.GetRectangle(index).Contains(x, y))
                {
                    continue;
                }

                _state.Select(index);
                break;
            }

            int regionX = _layout.GetRegionX(x, _alternateLayout);

            for (int index = 0; index < _regions.Length; index++)
            {
                if (!_regions[index].Contains(regionX, y))
                {
                    continue;
                }

                _state.Select(index + 6);
                break;
            }

            return;
        }

        bool remainsInside = hovered switch
        {
            >= 0 and < 6 => MainHudStatusHintLayout.GetRectangle(hovered).Contains(x, y),
            >= 6 and < 9 => _regions[hovered - 6].Contains(_layout.GetRegionX(x, _alternateLayout), y),
            _ => false,
        };

        if (!remainsInside)
        {
            _state.Clear();
        }
    }

    public void Clear() => _state.Clear();
}
