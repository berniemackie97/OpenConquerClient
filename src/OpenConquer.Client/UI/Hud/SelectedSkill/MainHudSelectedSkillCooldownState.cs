namespace OpenConquer.Client.UI.Hud.SelectedSkill;

internal sealed class MainHudSelectedSkillCooldownState
{
    public uint RemainingMilliseconds
    {
        get; private set;
    }

    public void SetRemainingMilliseconds(uint remainingMilliseconds) => RemainingMilliseconds = remainingMilliseconds;
}
