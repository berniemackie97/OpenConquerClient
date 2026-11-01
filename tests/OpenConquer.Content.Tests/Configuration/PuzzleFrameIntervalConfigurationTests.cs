using OpenConquer.Content.Configuration;
using OpenConquer.Tests.Fixtures;

namespace OpenConquer.Content.Tests.Configuration;

public sealed class PuzzleFrameIntervalConfigurationTests
{
    [Fact]
    public void ReadsAuthoredPuzzleAnimationInterval()
    {
        using TemporaryContentDirectory content = new();
        content.WriteFile("ini/info.ini", "[PuzzleBmp]\nFrameIntervalMilliSec=125\n");

        Assert.Equal(125u, PuzzleFrameIntervalConfiguration.Load(new ClientContentRoot(content.RootPath)));
    }

    [Fact]
    public void UsesConfiguredFallbackWhenKeyIsAbsent()
    {
        using TemporaryContentDirectory content = new();
        content.WriteFile("ini/info.ini", "[Other]\nEnabled=1\n");

        Assert.Equal(1000u, PuzzleFrameIntervalConfiguration.Load(new ClientContentRoot(content.RootPath)));
    }

    [Fact]
    public void ZeroIntervalAdvancesOnEachEligibleFrame()
    {
        using TemporaryContentDirectory content = new();
        content.WriteFile("ini/info.ini", "[PuzzleBmp]\nFrameIntervalMilliSec=0\n");

        Assert.Equal(1u, PuzzleFrameIntervalConfiguration.Load(new ClientContentRoot(content.RootPath)));
    }
}
