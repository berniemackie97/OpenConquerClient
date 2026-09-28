namespace OpenConquer.Client.UI.Hud;

internal sealed class MainHudActionButtonStripState
{
    private const uint BlinkIntervalMilliseconds = 500;
    private const uint PkBlinkDurationMilliseconds = 30000;

    private readonly Dictionary<MainHudActionButtonId, MainHudActionButtonState> _buttons = CreateButtonStates();

    private MainHudPkButtonSkin _pkSkin = MainHudPkButtonSkin.Button47;
    private uint _pkBlinkStartedAt;
    private uint _organiseBlinkStartedAt;
    private bool _pkBlinkClockInitialized;
    private bool _pkBlinkActive;
    private bool _organiseBlinkClockInitialized;
    private bool _organiseBlinkActive;

    public MainHudPkButtonSkin PkSkin => _pkSkin;
    public bool IsPkBlinkActive => _pkBlinkActive;
    public bool IsOrganiseBlinkActive => _organiseBlinkActive;

    public MainHudActionButtonState GetButton(MainHudActionButtonId id)
    {
        if (_buttons.TryGetValue(id, out MainHudActionButtonState? state))
        {
            return state;
        }

        throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown native main HUD action-button identifier.");
    }

    public void ApplyPkMode(int mode)
    {
        switch (mode)
        {
            case 0:
                _pkSkin = MainHudPkButtonSkin.Button47;
                _pkBlinkActive = true;
                break;
            case 1:
                _pkSkin = MainHudPkButtonSkin.Button49;
                break;
            case 2:
                _pkSkin = MainHudPkButtonSkin.Button48;
                break;
            case 3:
                _pkSkin = MainHudPkButtonSkin.Button412;
                break;
        }
    }

    public void ArmOrganiseBlink() => _organiseBlinkActive = true;

    public void ResetOrganiseBlink()
    {
        if (!_organiseBlinkActive)
        {
            return;
        }

        _organiseBlinkActive = false;
        GetButton(MainHudActionButtonId.Main3OrganiseBtn).SetCurrentFrame(MainHudActionButtonState.NormalFrame);
    }

    public void AdvancePkBeforeDraw(Func<uint> readTickCount)
    {
        ArgumentNullException.ThrowIfNull(readTickCount);
        if (!_pkBlinkActive)
        {
            return;
        }

        if (!_pkBlinkClockInitialized)
        {
            _pkBlinkClockInitialized = true;
            _pkBlinkStartedAt = readTickCount();
        }

        if (_pkBlinkStartedAt == 0)
        {
            _pkBlinkStartedAt = readTickCount();
        }

        uint phaseNow = readTickCount();
        uint phaseElapsed = unchecked(phaseNow - _pkBlinkStartedAt);
        GetButton(MainHudActionButtonId.Button47).SetCurrentFrame((int)((phaseElapsed / BlinkIntervalMilliseconds) & 1));

        uint expiryNow = readTickCount();
        uint expiryElapsed = unchecked(expiryNow - _pkBlinkStartedAt);

        if (expiryElapsed <= PkBlinkDurationMilliseconds)
        {
            return;
        }

        _pkBlinkActive = false;
        _pkBlinkStartedAt = 0;
        GetButton(MainHudActionButtonId.Button47).SetCurrentFrame(MainHudActionButtonState.NormalFrame);
    }

    public void AdvanceOrganiseBeforeDraw(Func<uint> readTickCount)
    {
        ArgumentNullException.ThrowIfNull(readTickCount);
        if (!_organiseBlinkActive)
        {
            return;
        }

        if (!_organiseBlinkClockInitialized)
        {
            _organiseBlinkClockInitialized = true;
            _organiseBlinkStartedAt = readTickCount();
        }

        uint now = readTickCount();
        uint elapsed = unchecked(now - _organiseBlinkStartedAt);
        int frame = MainHudActionButtonState.PressedFrame + (int)((elapsed / BlinkIntervalMilliseconds) & 1);
        GetButton(MainHudActionButtonId.Main3OrganiseBtn).SetCurrentFrame(frame);
    }

    private static Dictionary<MainHudActionButtonId, MainHudActionButtonState> CreateButtonStates()
    {
        Dictionary<MainHudActionButtonId, MainHudActionButtonState> states = new();

        foreach (MainHudActionButtonDefinition definition in MainHudActionButtonDefinitions.NativeDrawOrder)
        {
            states.Add(definition.Id, new MainHudActionButtonState());
        }

        return states;
    }
}
