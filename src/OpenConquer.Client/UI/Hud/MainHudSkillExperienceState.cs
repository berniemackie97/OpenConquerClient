namespace OpenConquer.Client.UI.Hud;

internal sealed class MainHudSkillExperienceState
{
    private const uint HighlightDurationMilliseconds = 500;

    private MainHudSkillExperienceSnapshot? _snapshot;
    private int _skillSubVariant;
    private uint _highlightStartedAt;
    private bool _highlightActive;

    public MainHudSkillExperienceSnapshot? Snapshot => _snapshot;
    public int SkillSubVariant => _skillSubVariant;

    public void SetSnapshot(MainHudSkillExperienceSnapshot snapshot)
    {
        _snapshot = snapshot;
    }

    public void ClearSnapshot()
    {
        _snapshot = null;
    }

    public void ActivateSkillSubVariant()
    {
        _skillSubVariant = 1;
    }

    public void ResetSkillSubVariant()
    {
        _skillSubVariant = 0;
    }

    public void ArmSkillHighlight()
    {
        _highlightStartedAt = 0;
        _highlightActive = true;
    }

    public void AdvanceAfterHudDraw(uint now)
    {
        if (!_highlightActive)
        {
            return;
        }

        if (_highlightStartedAt == 0)
        {
            _skillSubVariant = 2;
            _highlightStartedAt = now;
            return;
        }

        uint deadline = unchecked(_highlightStartedAt + HighlightDurationMilliseconds);

        if (now >= deadline)
        {
            _skillSubVariant = 0;
            _highlightActive = false;
        }
    }
}
