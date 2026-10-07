namespace OpenConquer.Client.UI.Hud;

internal sealed class MainHudSelectedSkillCooldownState
{
    public uint RemainingMilliseconds
    {
        get; private set;
    }

    public void SetRemainingMilliseconds(uint remainingMilliseconds) => RemainingMilliseconds = remainingMilliseconds;
}
