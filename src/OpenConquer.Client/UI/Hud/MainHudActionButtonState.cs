namespace OpenConquer.Client.UI.Hud;

internal sealed class MainHudActionButtonState
{
    public const int NormalFrame = 0;
    public const int PressedFrame = 1;
    public const int DisabledFrame = 2;
    public const int HoverFrame = 3;

    private int _currentFrame;
    private bool _cursorTrackingActive;
    private bool _cursorInside;
    private bool _hoverFrameEnabled;
    private bool _pointerCaptured;

    public int CurrentFrame => _currentFrame;
    public bool IsEnabled { get; private set; } = true;
    public bool IsCursorInside => _cursorInside;
    public bool IsPointerCaptured => _pointerCaptured;
    public int RenderFrame => _hoverFrameEnabled && _cursorTrackingActive && _currentFrame != PressedFrame && IsEnabled ? HoverFrame : _currentFrame;

    public void SetEnabled(bool enabled)
    {
        IsEnabled = enabled;
        _currentFrame = enabled ? NormalFrame : DisabledFrame;
        if (!enabled)
        {
            _pointerCaptured = false;
        }
    }

    public void SetHoverFrameEnabled(bool enabled) => _hoverFrameEnabled = enabled;

    public void SetCurrentFrame(int frameIndex)
    {
        if ((uint)frameIndex > HoverFrame)
        {
            throw new ArgumentOutOfRangeException(nameof(frameIndex), frameIndex, $"Frame index must be between {NormalFrame} and {HoverFrame}.");
        }

        _currentFrame = frameIndex;
    }

    public bool HandlePointerMoved(int logicalX, int logicalY, MainHudActionButtonBounds bounds)
    {
        bool inside = bounds.Contains(logicalX, logicalY);
        _cursorTrackingActive = inside;
        _cursorInside = inside;
        return inside || _pointerCaptured;
    }

    public bool HandleLeftButtonDown(int logicalX, int logicalY, MainHudActionButtonBounds bounds)
    {
        if (!IsEnabled || !bounds.Contains(logicalX, logicalY))
        {
            return false;
        }

        _currentFrame = PressedFrame;
        _pointerCaptured = true;
        _cursorTrackingActive = true;
        _cursorInside = true;
        return true;
    }

    public bool HandleLeftButtonUp(int logicalX, int logicalY, MainHudActionButtonBounds bounds, out bool activated)
    {
        activated = false;
        if (!_pointerCaptured)
        {
            return false;
        }

        activated = IsEnabled && _currentFrame == PressedFrame && bounds.Contains(logicalX, logicalY);
        _pointerCaptured = false;
        _currentFrame = IsEnabled ? NormalFrame : DisabledFrame;
        HandlePointerMoved(logicalX, logicalY, bounds);
        return true;
    }
}
