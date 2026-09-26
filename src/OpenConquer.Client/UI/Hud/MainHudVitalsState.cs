namespace OpenConquer.Client.UI.Hud;

internal sealed class MainHudVitalsState
{
    private MainHudVitalsSnapshot? _snapshot;
    private int _lifeSubVariant = 1;
    private int _manaSubVariant;

    public MainHudVitalsSnapshot? Snapshot => _snapshot;
    public int LifeSubVariant => _lifeSubVariant;
    public int ManaSubVariant => _manaSubVariant;

    public void SetSnapshot(MainHudVitalsSnapshot snapshot)
    {
        _snapshot = snapshot;

        if (_lifeSubVariant == 1 && ClampToRange(snapshot.CurrentMana, snapshot.MaxMana) > 0)
        {
            _lifeSubVariant = 0;
        }
    }

    public void ClearSnapshot()
    {
        _snapshot = null;
    }

    public void SetManaAlternateSubVariant(bool enabled)
    {
        _manaSubVariant = enabled ? 1 : 0;
    }

    private static int ClampToRange(int value, int maximum)
    {
        if (maximum <= 0 || value <= 0)
        {
            return 0;
        }

        return Math.Min(value, maximum);
    }
}
