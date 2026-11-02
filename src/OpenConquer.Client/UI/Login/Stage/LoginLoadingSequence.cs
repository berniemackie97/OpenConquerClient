namespace OpenConquer.Client.UI.Login.Stage;

internal sealed class LoginLoadingSequence
{
    private static readonly TimeSpan s_frameDuration = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 30);
    private static readonly TimeSpan s_maximumStep = TimeSpan.FromMilliseconds(250);
    private TimeSpan _remainder;
    private bool _accountReady;
    private bool _worldReady;

    internal int Frame { get; private set; } = 1;
    internal long InnerFrames { get; private set; }
    internal bool Visible => Frame > 1;
    internal bool CanEnterWorld => Frame == 99;

    internal void Start()
    {
        Frame = 2;
        InnerFrames = 0;
        _remainder = TimeSpan.Zero;
        _accountReady = false;
        _worldReady = false;
    }

    internal void AccountReady() => _accountReady = true;

    internal void WorldReady() => _worldReady = true;

    internal void Reset()
    {
        Frame = 1;
        InnerFrames = 0;
        _remainder = TimeSpan.Zero;
        _accountReady = false;
        _worldReady = false;
    }

    internal void Advance(TimeSpan elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(elapsed, TimeSpan.Zero);
        if (!Visible || CanEnterWorld)
        {
            return;
        }

        _remainder += elapsed < s_maximumStep ? elapsed : s_maximumStep;
        int steps = (int)(_remainder.Ticks / s_frameDuration.Ticks);
        _remainder -= s_frameDuration * steps;
        for (int i = 0; i < steps; i++)
        {
            if (Frame >= 18)
            {
                InnerFrames++;
            }

            if ((Frame == 17 && !_accountReady) || (Frame == 36 && !_worldReady))
            {
                _remainder = TimeSpan.Zero;
                break;
            }

            Frame++;
            if (CanEnterWorld)
            {
                _remainder = TimeSpan.Zero;
                break;
            }
        }
    }

    internal int HeadFrame => (int)(InnerFrames % 7);
    internal int LoadingTextFrame => (int)((InnerFrames % 40) / 10);
}
