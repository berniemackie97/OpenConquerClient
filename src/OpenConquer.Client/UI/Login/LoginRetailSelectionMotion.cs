namespace OpenConquer.Client.UI.Login;

/// <summary>
/// Deterministic 30-fps selection and rollover timelines; never mutates catalog authority.
/// A selection survives ordinary animation ticks but resets on different authenticated entities.
/// </summary>
internal sealed class LoginRetailSelectionMotion
{
    private const double FrameSeconds = 1d / 30d;
    private readonly LoginVisualLayout _layout;
    private LoginRegion? _selectedRegion;
    private LoginServer? _selectedServer;
    private LoginRegion? _hoverRegion;
    private LoginServer? _hoverServer;
    private bool _mapped;
    private int _pointerX;
    private int _pointerY;
    private double _fractional;
    private int _regionTick;
    private int _regionFadeTick;
    private int _serverTick;
    private int _regionHoverTick;
    private int _serverHoverTick;
    private int _lastRegionHoverSlot = -1;
    private bool _regionRollingOut;

    public int HoveredRegionSlot { get; private set; } = -1;
    public int HoveredServerSlot { get; private set; } = -1;
    public int RegionFlameFrame => _selectedRegion is null ? -1 : _regionTick % 25;
    /// <summary>Elapsed frames of defaultR sprite 61, which stops after its five-frame fade-in.</summary>
    public int RegionFlameFadeStep => _selectedRegion is null ? -1 : _regionFadeTick;
    public int ServerSelectedFrame => _selectedServer is null ? -1 :
        _serverTick < 47 ? _serverTick : 22 + (_serverTick - 22) % 25;
    public int ServerHoverFrame => _hoverServer is null ? -1 : Math.Min(_serverHoverTick, 33);
    public int RegionBorderSlot => HoveredRegionSlot >= 0 ? HoveredRegionSlot :
        _regionRollingOut ? _lastRegionHoverSlot : -1;
    public int RegionBorderFrame => _regionRollingOut ? 7 + Math.Min(_regionHoverTick, 2) : Math.Min(_regionHoverTick, 6);

    public LoginRetailSelectionMotion(LoginVisualLayout layout) => _layout = layout ?? throw new ArgumentNullException(nameof(layout));

    public void PointerMoved(bool mapped, int x, int y)
    {
        _mapped = mapped;
        _pointerX = x;
        _pointerY = y;
    }

    public void ClearPointer() => _mapped = false;

    /// <summary>Advances at most eight catch-up frames so long stalls never allocate work unboundedly.</summary>
    public void Advance(double elapsedSeconds, LoginServerSelection selection, bool interactive)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        }
        LoginRegion? region = selection.SelectedRegion;
        LoginServer? server = selection.SelectedServer;
        if (!ReferenceEquals(_selectedRegion, region))
        {
            _selectedRegion = region;
            _regionTick = 0;
            _regionFadeTick = 0;
        }
        if (!ReferenceEquals(_selectedServer, server))
        {
            _selectedServer = server;
            _serverTick = 0;
        }

        // `sr` rollover ignores the selected holder; it stays on its frame-13 selected look.
        int regionSlot = interactive && _mapped ? LoginVisualLayout.HitSlot(_layout.RegionHitAreas, _pointerX, _pointerY) : -1;
        LoginRegion? hoveredRegion = regionSlot >= 0 && regionSlot != selection.SelectedRegionSlot ? selection.RegionAtSlot(regionSlot) : null;
        if (hoveredRegion is null)
            regionSlot = -1;
        if (!ReferenceEquals(hoveredRegion, _hoverRegion))
        {
            if (hoveredRegion is null && _hoverRegion is not null && HoveredRegionSlot >= 0)
            {
                _lastRegionHoverSlot = HoveredRegionSlot;
                _regionRollingOut = true;
            }
            else
            {
                _lastRegionHoverSlot = -1;
                _regionRollingOut = false;
            }
            _hoverRegion = hoveredRegion;
            _regionHoverTick = 0;
        }
        HoveredRegionSlot = regionSlot;

        int serverSlot = interactive && _mapped && region is not null ? LoginVisualLayout.HitSlot(_layout.ServerHitAreas, _pointerX, _pointerY) : -1;
        LoginServer? hoveredServer = serverSlot >= 0 ? selection.ServerAtSlot(serverSlot) : null;
        if (hoveredServer is null)
            serverSlot = -1;
        if (!ReferenceEquals(hoveredServer, _hoverServer))
        {
            _hoverServer = hoveredServer;
            _serverHoverTick = 0;
        }
        HoveredServerSlot = serverSlot;

        _fractional += Math.Min(elapsedSeconds, 0.25);
        int ticks = Math.Min(8, (int)((_fractional + 1e-10) / FrameSeconds));
        _fractional -= ticks * FrameSeconds;
        if (ticks == 0)
            return;
        _regionTick = AdvanceLoop(_regionTick, ticks, 25);
        _regionFadeTick = Math.Min(_regionFadeTick + ticks, 5);
        // The selected server begins at frame 1, then loops the frames from the AVM1 "aa" label (23–47).
        _serverTick = _serverTick < 47 ? Math.Min(47, _serverTick + ticks) : 47 + ((_serverTick - 47 + ticks) % 25);
        _regionHoverTick = Math.Min(_regionHoverTick + ticks, 8);
        _serverHoverTick = Math.Min(_serverHoverTick + ticks, 33);
        if (_regionRollingOut && _regionHoverTick >= 3)
        {
            _regionRollingOut = false;
            _lastRegionHoverSlot = -1;
        }
    }

    private static int AdvanceLoop(int current, int ticks, int length) => (current + ticks) % length;
}
