using OpenConquer.Client.UI.Login.Credentials;

namespace OpenConquer.Client.Tests;

public sealed class ClientFrameClockTests
{
    [Fact]
    public void OneMonotonicSampleProvidesElapsedTimeAndWrappingMilliseconds()
    {
        const long BeforeWrap = ((long)uint.MaxValue * 10) + 9;
        SampleClock source = new(BeforeWrap);
        ClientFrameClock clock = new(source);

        Assert.Equal((TimeSpan.Zero, uint.MaxValue), clock.Next());

        source.Timestamp += 12;
        Assert.Equal((TimeSpan.FromTicks(12_000), 1u), clock.Next());

        Assert.Equal((TimeSpan.Zero, 1u), clock.Next());
        Assert.Equal(3, source.Reads);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(529, true)]
    [InlineData(530, false)]
    [InlineData(999, false)]
    [InlineData(1_000, true)]
    public void CaretBlinkPreservesItsPhaseBoundaries(int elapsedMilliseconds, bool visible)
    {
        Assert.Equal(visible, LoginCredentialPresentation.CaretVisible(TimeSpan.FromMilliseconds(elapsedMilliseconds)));
    }

    private sealed class SampleClock(long timestamp) : TimeProvider
    {
        internal long Timestamp
        {
            get;
            set;
        } = timestamp;
        internal int Reads
        {
            get;
            private set;
        }

        public override long TimestampFrequency => 10_000;

        public override long GetTimestamp()
        {
            Reads++;
            return Timestamp;
        }

        public override DateTimeOffset GetUtcNow() => throw new InvalidOperationException("Frame time must be monotonic.");
    }
}
