using OpenConquer.Platform.Windowing.Desktop;

namespace OpenConquer.Platform.Tests;

public sealed class DesktopKeyRepeatTests
{
    [Fact]
    public void RepeatableKey_EmitsOnlyAfterDelayAndAtMostOncePerPoll()
    {
        DesktopKeyRepeat repeat = new();
        long start = 10;
        repeat.Press(DesktopKey.Backspace, start);

        Assert.False(repeat.TryRepeat(start + DesktopKeyRepeat.InitialDelayTicks - 1, out _));
        Assert.True(repeat.TryRepeat(start + DesktopKeyRepeat.InitialDelayTicks, out DesktopKey key));
        Assert.Equal(DesktopKey.Backspace, key);
        Assert.False(repeat.TryRepeat(start + DesktopKeyRepeat.InitialDelayTicks, out _));

        long afterPause = start + DesktopKeyRepeat.InitialDelayTicks + (DesktopKeyRepeat.IntervalTicks * 100);
        Assert.True(repeat.TryRepeat(afterPause, out key));
        Assert.Equal(DesktopKey.Backspace, key);
        Assert.False(repeat.TryRepeat(afterPause, out _));
        Assert.True(repeat.TryRepeat(afterPause + DesktopKeyRepeat.IntervalTicks, out key));
    }

    [Fact]
    public void NewNonRepeatableCommand_StopsHeldEditingKeyRepeat()
    {
        DesktopKeyRepeat repeat = new();
        repeat.Press(DesktopKey.Delete, 0);
        repeat.Press(DesktopKey.Enter, 1);

        Assert.False(repeat.TryRepeat(DesktopKeyRepeat.InitialDelayTicks, out _));
    }

    [Fact]
    public void NewRepeatableKey_ReplacesPreviousKeyAndRestartsDelay()
    {
        DesktopKeyRepeat repeat = new();
        repeat.Press(DesktopKey.Left, 0);
        repeat.Press(DesktopKey.Right, 100);
        repeat.Release(DesktopKey.Left);

        Assert.False(repeat.TryRepeat(DesktopKeyRepeat.InitialDelayTicks, out _));
        Assert.True(repeat.TryRepeat(100 + DesktopKeyRepeat.InitialDelayTicks, out DesktopKey key));
        Assert.Equal(DesktopKey.Right, key);
    }

    [Fact]
    public void ReleaseAndFocusReset_StopRepeats()
    {
        DesktopKeyRepeat repeat = new();
        repeat.Press(DesktopKey.Backspace, 0);
        repeat.Release(DesktopKey.Backspace);
        Assert.False(repeat.TryRepeat(DesktopKeyRepeat.InitialDelayTicks * 2, out _));

        repeat.Press(DesktopKey.Delete, 0);
        repeat.Reset();
        Assert.False(repeat.TryRepeat(DesktopKeyRepeat.InitialDelayTicks * 2, out _));
    }
}
