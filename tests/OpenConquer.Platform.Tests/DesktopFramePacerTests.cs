using OpenConquer.Platform.Internal;

namespace OpenConquer.Platform.Tests;

public sealed class DesktopFramePacerTests
{
    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(25);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ConstructorWhenFrameIntervalIsNotPositiveThrowsArgumentOutOfRangeException(
        int frameIntervalMilliseconds
    )
    {
        TimeSpan frameInterval = TimeSpan.FromMilliseconds(frameIntervalMilliseconds);

        Assert.Throws<ArgumentOutOfRangeException>(() => new DesktopFramePacer(frameInterval));
    }

    [Fact]
    public void WaitForNextFrameBeforeStartThrowsInvalidOperationException()
    {
        DesktopFramePacer framePacer = new(FrameInterval);

        Assert.Throws<InvalidOperationException>(framePacer.WaitForNextFrame);
    }

    [Fact]
    public void WaitForNextFrameImmediatelyAfterStartWaitsForFullInterval()
    {
        ManualTimeProvider timeProvider = new();
        List<TimeSpan> sleepDurations = [];

        DesktopFramePacer framePacer = CreateFramePacer(timeProvider, sleepDurations);

        framePacer.Start();
        framePacer.WaitForNextFrame();

        Assert.Equal([FrameInterval], sleepDurations);

        Assert.Equal(FrameInterval, timeProvider.Elapsed);
    }

    [Fact]
    public void WaitForNextFrameWaitsOnlyForRemainingInterval()
    {
        ManualTimeProvider timeProvider = new();
        List<TimeSpan> sleepDurations = [];

        DesktopFramePacer framePacer = CreateFramePacer(timeProvider, sleepDurations);

        framePacer.Start();

        timeProvider.Advance(TimeSpan.FromMilliseconds(10));

        framePacer.WaitForNextFrame();

        Assert.Equal([TimeSpan.FromMilliseconds(15)], sleepDurations);

        Assert.Equal(FrameInterval, timeProvider.Elapsed);
    }

    [Fact]
    public void WaitForNextFrameAfterOverrunDoesNotSleep()
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
    public void WaitForNextFrameAfterOverrunDoesNotCatchUpMissedFrames()
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
    public void WaitForNextFrameAnchorsNextFrameToActualWakeTime()
    {
        ManualTimeProvider timeProvider = new();
        List<TimeSpan> sleepDurations = [];

        DesktopFramePacer framePacer = new(
            FrameInterval,
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
            FrameInterval,
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
