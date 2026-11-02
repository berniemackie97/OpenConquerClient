using OpenConquer.Client.UI.Login.Stage;

namespace OpenConquer.Client.Tests.UI.Login.Stage;

public sealed class LoginLoadingSequenceTests
{
    [Fact]
    public void StartStopsAtEveryEventGateAndFinishesAtClose()
    {
        LoginLoadingSequence sequence = new();
        sequence.Start();

        Advance(sequence, 30);
        Assert.Equal(17, sequence.Frame);
        Assert.True(sequence.ReadyForAuthentication);
        Assert.True(sequence.Visible);
        Assert.False(sequence.CanEnterWorld);

        sequence.AccountReady();
        Advance(sequence, 30);
        Assert.Equal(36, sequence.Frame);
        Assert.False(sequence.ReadyForAuthentication);
        Assert.False(sequence.CanEnterWorld);

        long nested = sequence.InnerFrames;
        Advance(sequence, 21);
        Assert.Equal(36, sequence.Frame);
        Assert.True(sequence.InnerFrames > nested);

        sequence.WorldReady();
        Advance(sequence, 90);
        Assert.Equal(99, sequence.Frame);
        Assert.True(sequence.CanEnterWorld);
    }

    [Fact]
    public void EarlyEventsDoNotSkipTheAuthoredFrames()
    {
        LoginLoadingSequence sequence = new();
        sequence.Start();
        sequence.AccountReady();
        sequence.WorldReady();

        sequence.Advance(TimeSpan.FromMilliseconds(40));

        Assert.Equal(3, sequence.Frame);
        Advance(sequence, 200);
        Assert.Equal(99, sequence.Frame);
    }

    [Fact]
    public void ResetDiscardsProgressAndNestedAnimationPhase()
    {
        LoginLoadingSequence sequence = new();
        sequence.Start();
        Advance(sequence, 30);
        sequence.AccountReady();
        Advance(sequence, 30);

        sequence.Reset();
        Assert.Equal(1, sequence.Frame);
        Assert.Equal(0, sequence.InnerFrames);
        Assert.False(sequence.Visible);

        sequence.Start();
        Advance(sequence, 30);
        Assert.Equal(17, sequence.Frame);
    }

    [Fact]
    public void CatchUpIsBoundedAndNegativeTimeRejected()
    {
        LoginLoadingSequence sequence = new();
        sequence.Start();
        sequence.Advance(TimeSpan.FromHours(1));
        Assert.InRange(sequence.Frame, 2, 9);
        Assert.Throws<ArgumentOutOfRangeException>(() => sequence.Advance(TimeSpan.FromTicks(-1)));
    }

    private static void Advance(LoginLoadingSequence sequence, int steps)
    {
        for (int i = 0; i < steps; i++)
        {
            sequence.Advance(TimeSpan.FromMilliseconds(34));
        }
    }
}
