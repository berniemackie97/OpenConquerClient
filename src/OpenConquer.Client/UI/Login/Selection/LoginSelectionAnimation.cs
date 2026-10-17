using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Content.Login;

namespace OpenConquer.Client.UI.Login.Selection;

/// <summary>
/// Rollover and selection timelines of the region and server tiles, advanced in stage frames. A timeline restarts
/// whenever its region or server changes, so it never outlives the catalog entry it belongs to.
/// </summary>
internal sealed class LoginSelectionAnimation
{
    // defaultR: rollover plays timeline frames 2-8 and holds; rollout plays 10-12. Indices into LoginRegionArtwork.Hover.
    private const int RegionRolloverHeldFrame = 6;
    private const int RegionRolloutFirstFrame = 7;
    private const int RegionRolloutFrameCount = 3;
    // defaultR sprite 61 fades its flame in over five frames, then stops.
    private const int RegionFlameFadeFrames = 5;
    // Sprite 179: rollover plays frames 2-5, fading sprite 40 in, and stops on frame 6, where sprite 83 starts.
    private const int ServerHighlightFadeFrames = 4;

    private readonly LoginStageLayout _layout;
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

    public LoginSelectionAnimation(LoginStageLayout layout) => _layout = layout ?? throw new ArgumentNullException(nameof(layout));

    public int HoveredRegionSlot { get; private set; } = -1;

    public int HoveredServerSlot { get; private set; } = -1;

    /// <summary>The slot showing a rollover or rollout frame, or -1.</summary>
    public int RegionRolloverSlot => HoveredRegionSlot >= 0 ? HoveredRegionSlot : _rollingOutSlot;

    /// <summary>Frame of <see cref="LoginRegionArtwork.Layer.Hover"/> for <see cref="RegionRolloverSlot"/>.</summary>
    public int RegionRolloverFrame => _rollingOutSlot >= 0 ? RegionRolloutFirstFrame + _regionRolloverFrame : _regionRolloverFrame;

    /// <summary>Frame of <see cref="LoginRegionArtwork.Layer.Flame"/> for the selected region.</summary>
    public int RegionFlameFrame
    {
        get; private set;
    }

    /// <summary>Elapsed frames of the flame's fade-in; at five or more it is fully opaque.</summary>
    public int RegionFlameFadeFrame
    {
        get; private set;
    }

    /// <summary>Frame of <see cref="LoginServerArtwork.Layer.Selected"/> for the selected server.</summary>
    public int ServerSelectedFrame
    {
        get; private set;
    }

    /// <summary>Sprite 179 frames 2-6 as 0-4: how far <see cref="LoginServerArtwork.Layer.Highlight"/> has faded in.</summary>
    public int ServerHighlightStep => Math.Min(_serverRolloverFrames, ServerHighlightFadeFrames);

    /// <summary>Frame of <see cref="LoginServerArtwork.Layer.Hover"/> for <see cref="HoveredServerSlot"/>, or -1 before frame 6.</summary>
    public int ServerHoverFrame => _serverRolloverFrames < ServerHighlightFadeFrames ? -1 : _serverRolloverFrames - ServerHighlightFadeFrames;

    public void PointerMoved(bool mapped, int x, int y)
    {
        _pointerMapped = mapped;
        _pointerX = x;
        _pointerY = y;
    }

    public void ClearPointer() => _pointerMapped = false;

    /// <param name="frames">Stage frames elapsed since the previous call.</param>
    /// <param name="selection">The current selection.</param>
    /// <param name="interactive">False while the opening, a dialog or the account list holds the pointer.</param>
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
        // Sprite 83 has no stop action: it loops while its tile holds frame 6.
        _serverRolloverFrames += frames;
        if (_serverRolloverFrames >= ServerHighlightFadeFrames)
        {
            _serverRolloverFrames = ServerHighlightFadeFrames + (_serverRolloverFrames - ServerHighlightFadeFrames) % LoginServerArtwork.HoverFrameCount;
        }

        if (_rollingOutSlot >= 0)
        {
            _regionRolloverFrame += frames;
            if (_regionRolloverFrame >= RegionRolloutFrameCount)
            {
                _rollingOutSlot = -1;
                _regionRolloverFrame = 0;
            }
        }
        else
        {
            _regionRolloverFrame = Math.Min(_regionRolloverFrame + frames, RegionRolloverHeldFrame);
        }
    }

    // Sprite 178 plays frames 1-47 once, then loops from its "aa" label at frame 23.
    private static int AdvanceSelectedServer(int frame, int frames)
    {
        int next = frame + frames;
        int loopLength = LoginServerArtwork.SelectedFrameCount - LoginServerArtwork.SelectedLoopStart;
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

        if (!ReferenceEquals(_selectedServer, selection.SelectedServer))
        {
            _selectedServer = selection.SelectedServer;
            ServerSelectedFrame = 0;
        }
    }

    // The selected region keeps its selected look; only the others roll over.
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
