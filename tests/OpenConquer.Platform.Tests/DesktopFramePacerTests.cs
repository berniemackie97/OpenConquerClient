using OpenConquer.Platform.Internal;

namespace OpenConquer.Platform.Tests;

public sealed class DesktopFramePacerTests
{
    private static readonly TimeSpan s_frameInterval = TimeSpan.FromMilliseconds(25);

    private static readonly TimeSpan s_maximumSleepInterval = TimeSpan.FromMilliseconds(int.MaxValue);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_ThrowsArgumentOutOfRangeExceptionWhenFrameIntervalIsNotPositive(
        int frameIntervalMilliseconds
    )
    {
        TimeSpan frameInterval = TimeSpan.FromMilliseconds(frameIntervalMilliseconds);

        Assert.Throws<ArgumentOutOfRangeException>(() => new DesktopFramePacer(frameInterval));
    }

    [Fact]
    public void Constructor_ThrowsArgumentOutOfRangeExceptionWhenFrameIntervalExceedsThreadSleepMaximum()
    {
        TimeSpan frameInterval = s_maximumSleepInterval + TimeSpan.FromMilliseconds(1);

        Assert.Throws<ArgumentOutOfRangeException>(() => new DesktopFramePacer(frameInterval));
    }

    [Fact]
    public void Constructor_SucceedsWhenFrameIntervalEqualsThreadSleepMaximum()
    {
        Exception? exception = Record.Exception(() => new DesktopFramePacer(s_maximumSleepInterval));

        Assert.Null(exception);
    }

    [Fact]
    public void Constructor_SucceedsWhenFrameIntervalCarriesAFractionAboveThreadSleepMaximum()
    {
        TimeSpan frameInterval = s_maximumSleepInterval + TimeSpan.FromTicks(1);

        Exception? exception = Record.Exception(() => new DesktopFramePacer(frameInterval));

        Assert.Null(exception);
    }

    [Fact]
    public void WaitForNextFrame_ThrowsInvalidOperationExceptionBeforeStart()
    {
        DesktopFramePacer framePacer = new(s_frameInterval);

        Assert.Throws<InvalidOperationException>(framePacer.WaitForNextFrame);
    }

    [Fact]
    public void WaitForNextFrame_WaitsForTheFullIntervalImmediatelyAfterStart()
    {
        ManualTimeProvider timeProvider = new();
        List<TimeSpan> sleepDurations = [];

        DesktopFramePacer framePacer = CreateFramePacer(timeProvider, sleepDurations);

        framePacer.Start();
        framePacer.WaitForNextFrame();

        Assert.Equal([s_frameInterval], sleepDurations);

        Assert.Equal(s_frameInterval, timeProvider.Elapsed);
    }

    [Fact]
    public void WaitForNextFrame_WaitsOnlyForTheRemainingInterval()
    {
        ManualTimeProvider timeProvider = new();
        List<TimeSpan> sleepDurations = [];

        DesktopFramePacer framePacer = CreateFramePacer(timeProvider, sleepDurations);

        framePacer.Start();

        timeProvider.Advance(TimeSpan.FromMilliseconds(10));

        framePacer.WaitForNextFrame();

        Assert.Equal([TimeSpan.FromMilliseconds(15)], sleepDurations);

        Assert.Equal(s_frameInterval, timeProvider.Elapsed);
    }

    [Fact]
    public void WaitForNextFrame_DoesNotSleepAfterAnOverrun()
    {
        ManualTimeProvider timeProvider = new();
        List<TimeSpan> sleepDurations = [];

        DesktopFramePacer framePacer = CreateFramePacer(timeProvider, sleepDurations);

        framePacer.Start();

        timeProvider.Advance(TimeSpan.FromMilliseconds(40));

        framePacer.WaitForNextFrame();

        Assert.Empty(sleepDurations);
    }

    [Fact]
    public void WaitForNextFrame_DoesNotCatchUpMissedFramesAfterAnOverrun()
    {
        ManualTimeProvider timeProvider = new();
        List<TimeSpan> sleepDurations = [];

        DesktopFramePacer framePacer = CreateFramePacer(timeProvider, sleepDurations);

        framePacer.Start();

        timeProvider.Advance(TimeSpan.FromMilliseconds(60));

        framePacer.WaitForNextFrame();

        timeProvider.Advance(TimeSpan.FromMilliseconds(5));

        framePacer.WaitForNextFrame();

        Assert.Equal([TimeSpan.FromMilliseconds(20)], sleepDurations);
    }

    [Fact]
    public void WaitForNextFrame_AnchorsTheNextFrameToTheActualWakeTime()
    {
        ManualTimeProvider timeProvider = new();
        List<TimeSpan> sleepDurations = [];

        DesktopFramePacer framePacer = new(
            s_frameInterval,
            timeProvider,
            duration =>
            {
                sleepDurations.Add(duration);

                timeProvider.Advance(duration + TimeSpan.FromMilliseconds(7));
            }
        );

        framePacer.Start();
        framePacer.WaitForNextFrame();

        timeProvider.Advance(TimeSpan.FromMilliseconds(5));

        framePacer.WaitForNextFrame();

        Assert.Equal(
            [TimeSpan.FromMilliseconds(25), TimeSpan.FromMilliseconds(20)],
            sleepDurations
        );
    }

    private static DesktopFramePacer CreateFramePacer(
        ManualTimeProvider timeProvider,
        List<TimeSpan> sleepDurations
    )
    {
        return new DesktopFramePacer(
            s_frameInterval,
            timeProvider,
            duration =>
            {
                sleepDurations.Add(duration);
                timeProvider.Advance(duration);
            }
        );
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _timestamp;

        public TimeSpan Elapsed => TimeSpan.FromTicks(_timestamp);

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp()
        {
            return _timestamp;
        }

        public void Advance(TimeSpan duration)
        {
            if (duration < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(duration),
                    duration,
                    "Duration must not be negative."
                );
            }

            _timestamp = checked(_timestamp + duration.Ticks);
        }
    }
}
