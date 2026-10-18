using Avalonia;
using Avalonia.Input;
using Avalonia.Threading;

namespace OpenConquer.Launcher.Play;

/// <summary>
/// <c>Play/Cursor/Normal.ani</c>, which <c>CPlayDlg_OnSetCursor</c> sets over the whole dialog: five frames with
/// the hotspot at the top left, advanced every ten jiffies.
/// </summary>
internal sealed class PlayCursor : IDisposable
{
    private readonly Cursor[] _frames;
    private readonly DispatcherTimer _timer;
    private readonly InputElement _target;
    private int _frame;

    public PlayCursor(InputElement target)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _frames = new Cursor[PlayArtwork.CursorFrames.Length];
        for (int index = 0; index < _frames.Length; index++)
        {
            using PlayBitmap bitmap = PlayBitmap.Load(PlayArtwork.CursorFrames[index]);
            _frames[index] = new Cursor(bitmap.Bitmap, new PixelPoint(0, 0));
        }

        _timer = new DispatcherTimer(PlayArtwork.CursorFrameDuration, DispatcherPriority.Input, (_, _) => Advance());
        _target.Cursor = _frames[0];
        _timer.Start();
    }

    public void Dispose()
    {
        _timer.Stop();
        _target.Cursor = null;
        foreach (Cursor frame in _frames)
        {
            frame.Dispose();
        }
    }

    private void Advance()
    {
        _frame = (_frame + 1) % _frames.Length;
        _target.Cursor = _frames[_frame];
    }
}
