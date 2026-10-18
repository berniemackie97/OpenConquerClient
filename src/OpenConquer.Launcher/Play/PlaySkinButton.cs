using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Rendering;

namespace OpenConquer.Launcher.Play;

/// <summary>
/// The <c>CSkinButton</c> of <c>play.exe</c>: bitmap states drawn at their native size, shaped to the opaque
/// pixels of the up state. Down shows while a press stays over the button; a click needs the release over it.
/// </summary>
internal sealed class PlaySkinButton : Control, ICustomHitTest, IDisposable
{
    private static readonly Pen s_focusPen = new(Brushes.Black, 1, new DashStyle([1, 1], 0));

    private readonly PlayButton _button;
    private readonly PlayBitmap[] _states = new PlayBitmap[3];
    private readonly PlayBitmap[]? _fadedStates;
    private bool _faded;
    private bool _hovered;
    private bool _pressed;
    private bool _focusVisible;

    public PlaySkinButton(PlayButton button)
    {
        _button = button;
        bool hasFadedArt = button <= PlayButton.Low;
        _fadedStates = hasFadedArt ? new PlayBitmap[3] : null;

        try
        {
            foreach (PlayButtonState state in Enum.GetValues<PlayButtonState>())
            {
                _states[(int)state] = PlayBitmap.Load(PlayArtwork.Button(button, state));
                if (_fadedStates is not null)
                {
                    _fadedStates[(int)state] = PlayBitmap.Load(PlayArtwork.Button(button, state, faded: true));
                }
            }
        }
        catch
        {
            ReleaseBitmaps();
            throw;
        }

        Width = _states[0].Width;
        Height = _states[0].Height;
        Focusable = true;
    }

    /// <summary>Raised when a press is released over the button, or Enter or Space is pressed while it has focus.</summary>
    public event EventHandler? Click;

    /// <summary>The start buttons' variant 1 art; the most-used start mode shows variant 0.</summary>
    public bool Faded
    {
        get => _faded;
        set
        {
            if (value && _fadedStates is null)
            {
                throw new InvalidOperationException($"{_button} has no faded artwork.");
            }

            if (_faded != value)
            {
                _faded = value;
                InvalidateVisual();
            }
        }
    }

    public bool HitTest(Point point) => _states[(int)PlayButtonState.Up].IsOpaqueAt(point);

    public override void Render(DrawingContext context)
    {
        PlayButtonState state = !IsEffectivelyEnabled ? PlayButtonState.Up
            : _pressed && _hovered ? PlayButtonState.Down
            : _hovered ? PlayButtonState.Over
            : PlayButtonState.Up;
        PlayBitmap bitmap = (_faded ? _fadedStates! : _states)[(int)state];
        context.DrawImage(bitmap.Bitmap, new Rect(0, 0, bitmap.Width, bitmap.Height));

        if (_focusVisible && IsFocused)
        {
            context.DrawRectangle(null, s_focusPen, new Rect(3.5, 3.5, bitmap.Width - 7, bitmap.Height - 7));
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        SetHovered(HitTest(e.GetPosition(this)));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        SetHovered(false);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || !HitTest(e.GetPosition(this)))
        {
            return;
        }

        e.Pointer.Capture(this);
        e.Handled = true;
        _pressed = true;
        _focusVisible = false;
        Focus();
        SetHovered(true);
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_pressed)
        {
            return;
        }

        bool over = HitTest(e.GetPosition(this));
        _pressed = false;
        e.Pointer.Capture(null);
        e.Handled = true;
        SetHovered(over);
        InvalidateVisual();
        if (over && IsEffectivelyEnabled)
        {
            Click?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_pressed)
        {
            _pressed = false;
            InvalidateVisual();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key is Key.Enter or Key.Space && IsEffectivelyEnabled)
        {
            e.Handled = true;
            Click?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusVisible = e.NavigationMethod == NavigationMethod.Tab;
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusVisible = false;
        InvalidateVisual();
    }

    public void Dispose() => ReleaseBitmaps();

    private void SetHovered(bool hovered)
    {
        if (_hovered != hovered)
        {
            _hovered = hovered;
            InvalidateVisual();
        }
    }

    private void ReleaseBitmaps()
    {
        foreach (PlayBitmap? bitmap in _states.Concat(_fadedStates ?? []))
        {
            bitmap?.Dispose();
        }

        Array.Clear(_states);
        if (_fadedStates is not null)
        {
            Array.Clear(_fadedStates);
        }
    }
}
