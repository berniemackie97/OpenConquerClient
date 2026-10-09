using OpenConquer.Content.Configuration;

namespace OpenConquer.Content.Tests.Configuration;

public sealed class TryWrapTipConfigurationTests
{
    [Theory]
    [InlineData(-5, -75)]
    [InlineData(0, 0)]
    [InlineData(12, 34)]
    public void Load_ResolvesVerifiedOffsets(int x, int y)
    {
        using TemporaryContentDirectory directory = new();
        directory.WriteFile(TryWrapTipConfiguration.RelativePath,
            $"[TryWrapTip]\nOffsetX={x}\nOffsetY={y}\nTimeOffsetX=999\nTimeOffsetY=999\nFontSize=99\n");

        TryWrapTipConfiguration configuration = TryWrapTipConfiguration.Load(new ClientContentRoot(directory.RootPath));

        Assert.Equal(x, configuration.OffsetX);
        Assert.Equal(y, configuration.OffsetY);
    }

    [Fact]
    public void Load_UsesNativeFirstNumericPrefix()
    {
        using TemporaryContentDirectory directory = new();
        directory.WriteFile(TryWrapTipConfiguration.RelativePath,
            "[TryWrapTip]\nOffsetX= -5pixels\nOffsetY= +75tail\n");

        TryWrapTipConfiguration configuration = TryWrapTipConfiguration.Load(new ClientContentRoot(directory.RootPath));

        Assert.Equal(-5, configuration.OffsetX);
        Assert.Equal(75, configuration.OffsetY);
    }

    [Fact]
    public void Load_MissingConfigurationUsesNativeZeroOffsets()
    {
        using TemporaryContentDirectory directory = new();

        TryWrapTipConfiguration configuration = TryWrapTipConfiguration.Load(new ClientContentRoot(directory.RootPath));

        Assert.Equal(0, configuration.OffsetX);
        Assert.Equal(0, configuration.OffsetY);
    }

    [Fact]
    public void Load_UsesOnlyTheRelevantNativeKeys()
    {
        using TemporaryContentDirectory directory = new();
        directory.WriteFile(TryWrapTipConfiguration.RelativePath,
            "[TryWrapTip]\nTimeOffsetX=-90\nTimeOffsetY=-90\nFontSize=30\n");

        TryWrapTipConfiguration configuration = TryWrapTipConfiguration.Load(new ClientContentRoot(directory.RootPath));

        Assert.Equal(0, configuration.OffsetX);
        Assert.Equal(0, configuration.OffsetY);
    }
}
