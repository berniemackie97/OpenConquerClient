namespace OpenConquer.Client.UI.Login.Stage;

/// <summary>
/// The login stage's 30 fps timeline. Every login animation reads its frame from this clock, which advances once
/// per rendered frame and starts with the first one.
/// </summary>
internal sealed class LoginStageClock
{
    public const int FramesPerSecond = 30;

    private static readonly TimeSpan s_frameDuration = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / FramesPerSecond);
    // A stall is not replayed in full: at most a quarter second is caught up.
    private static readonly TimeSpan s_maximumStep = TimeSpan.FromMilliseconds(250);
    private TimeSpan _pending;
    private bool _started;

    /// <summary>Stage frames elapsed since the first rendered frame.</summary>
    public long Frame
    {
        get; private set;
    }

    /// <summary>Wall-clock time covered by the latest <see cref="Advance"/>, after the stall limit.</summary>
    public TimeSpan Step
    {
        get; private set;
    }

    /// <summary>
    /// Advances by one rendered frame's elapsed time and returns the whole stage frames that passed. The first
    /// call only starts the clock, so time spent before the first frame (content and GPU setup) is not played.
    /// </summary>
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
