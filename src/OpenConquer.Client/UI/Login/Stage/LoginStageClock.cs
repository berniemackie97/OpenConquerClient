namespace OpenConquer.Client.UI.Login.Stage;

internal sealed class LoginStageClock
{
    public const int FramesPerSecond = 30;

    private static readonly TimeSpan s_frameDuration = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / FramesPerSecond);
    private static readonly TimeSpan s_maximumStep = TimeSpan.FromMilliseconds(250);
    private TimeSpan _pending;
    private bool _started;

    public long Frame
    {
        get; private set;
    }

    public TimeSpan Step
    {
        get; private set;
    }

    public int Advance(TimeSpan elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(elapsed, TimeSpan.Zero);

        if (!_started)
        {
            _started = true;
            Step = TimeSpan.Zero;
            return 0;
        }

        Step = elapsed < s_maximumStep ? elapsed : s_maximumStep;
        _pending += Step;
        int frames = (int)(_pending.Ticks / s_frameDuration.Ticks);
        _pending -= s_frameDuration * frames;
        Frame += frames;
        return frames;
    }
}
