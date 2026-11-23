using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Content.Login;

namespace OpenConquer.Client.UI.Login.Selection;

internal sealed class LoginSelectionAnimation(LoginStageLayout layout)
{
    private const int RegionRolloverHeldFrame = 6;
    private const int RegionRolloutFirstFrame = 7;
    private const int RegionRolloutFrameCount = 3;
    private const int RegionFlameFadeFrames = 5;
    private const int ServerHighlightFadeFrames = 4;

    private readonly LoginStageLayout _layout = layout ?? throw new ArgumentNullException(nameof(layout));
    private LoginRegion? _selectedRegion;
    private LoginServer? _selectedServer;
    private LoginRegion? _hoveredRegion;
    private LoginServer? _hoveredServer;
    private bool _pointerMapped;
    private int _pointerX;
    private int _pointerY;
    private int _regionRolloverFrame;
    private int _rollingOutSlot = -1;
    private int _serverRolloverFrames;

    public int HoveredRegionSlot { get; private set; } = -1;

    public int HoveredServerSlot { get; private set; } = -1;

    public int RegionRolloverSlot => HoveredRegionSlot >= 0 ? HoveredRegionSlot : _rollingOutSlot;

    public int RegionRolloverFrame => _rollingOutSlot >= 0 ? RegionRolloutFirstFrame + _regionRolloverFrame : _regionRolloverFrame;

    public int RegionFlameFrame
    {
        get; private set;
    }

    public int RegionFlameFadeFrame
    {
        get; private set;
    }

    public int ServerSelectedFrame
    {
        get; private set;
    }

    public int ServerHighlightStep => Math.Min(_serverRolloverFrames, ServerHighlightFadeFrames);

    public int ServerHoverFrame => _serverRolloverFrames < ServerHighlightFadeFrames ? -1 : _serverRolloverFrames - ServerHighlightFadeFrames;

    public void PointerMoved(bool mapped, int x, int y)
    {
        _pointerMapped = mapped;
        _pointerX = x;
        _pointerY = y;
    }

    public void ClearPointer()
    {
        _pointerMapped = false;
    }

    public void Advance(int frames, LoginServerSelection selection, bool interactive)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frames);
        ArgumentNullException.ThrowIfNull(selection);

        TrackSelection(selection);
        TrackRegionRollover(selection, interactive);
        TrackServerRollover(selection, interactive);

        if (frames == 0)
        {
            return;
        }

        RegionFlameFrame = (RegionFlameFrame + frames) % LoginRegionArtwork.FlameFrameCount;
        RegionFlameFadeFrame = Math.Min(RegionFlameFadeFrame + frames, RegionFlameFadeFrames);
        ServerSelectedFrame = AdvanceSelectedServer(ServerSelectedFrame, frames);
        _serverRolloverFrames += frames;
        if (_serverRolloverFrames >= ServerHighlightFadeFrames)
        {
            _serverRolloverFrames = ServerHighlightFadeFrames + (_serverRolloverFrames - ServerHighlightFadeFrames) % LoginServerArtwork.HoverFrameCount;
        }

        if (_rollingOutSlot >= 0)
        {
            _regionRolloverFrame += frames;
            if (_regionRolloverFrame < RegionRolloutFrameCount)
            {
                return;
            }

            _rollingOutSlot = -1;
            _regionRolloverFrame = 0;
        }
        else
        {
            _regionRolloverFrame = Math.Min(_regionRolloverFrame + frames, RegionRolloverHeldFrame);
        }
    }

    private static int AdvanceSelectedServer(int frame, int frames)
    {
        int next = frame + frames;
        const int loopLength = LoginServerArtwork.SelectedFrameCount - LoginServerArtwork.SelectedLoopStart;
        return next < LoginServerArtwork.SelectedFrameCount
            ? next
            : LoginServerArtwork.SelectedLoopStart + (next - LoginServerArtwork.SelectedLoopStart) % loopLength;
    }

    private void TrackSelection(LoginServerSelection selection)
    {
        if (!ReferenceEquals(_selectedRegion, selection.SelectedRegion))
        {
            _selectedRegion = selection.SelectedRegion;
            RegionFlameFrame = 0;
            RegionFlameFadeFrame = 0;
        }

        if (ReferenceEquals(_selectedServer, selection.SelectedServer))
        {
            return;
        }

        _selectedServer = selection.SelectedServer;
        ServerSelectedFrame = 0;
    }

    private void TrackRegionRollover(LoginServerSelection selection, bool interactive)
    {
        int slot = interactive && _pointerMapped ? LoginStageLayout.HitSlot(_layout.RegionHitAreas, _pointerX, _pointerY) : -1;
        LoginRegion? region = slot >= 0 && slot != selection.SelectedRegionSlot ? selection.RegionAtSlot(slot) : null;

        if (!ReferenceEquals(region, _hoveredRegion))
        {
            _rollingOutSlot = region is null && HoveredRegionSlot >= 0 ? HoveredRegionSlot : -1;
            _hoveredRegion = region;
            _regionRolloverFrame = 0;
        }

        HoveredRegionSlot = region is null ? -1 : slot;
    }

    private void TrackServerRollover(LoginServerSelection selection, bool interactive)
    {
        int slot = interactive && _pointerMapped && selection.SelectedRegion is not null
            ? LoginStageLayout.HitSlot(_layout.ServerHitAreas, _pointerX, _pointerY)
            : -1;
        LoginServer? server = slot >= 0 ? selection.ServerAtSlot(slot) : null;

        if (!ReferenceEquals(server, _hoveredServer))
        {
            _hoveredServer = server;
            _serverRolloverFrames = 0;
        }

        HoveredServerSlot = server is null ? -1 : slot;
    }
}
