using OpenConquer.Client.UI.Login;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Login;

public sealed class LoginVisualLayoutTests
{
    [Fact]
    public void ReferenceStage_PreservesIndependentChromeAndAccountAnchors()
    {
        LoginVisualLayout layout = LoginVisualLayout.Create(new LogicalRenderSize(1024, 768));

        Assert.Equal(new LoginSpritePlacement(0, 40, 1024, 637), layout.Backdrop);
        Assert.Equal(new LoginSpritePlacement(0, 0, 1024, 116), layout.Header);
        Assert.Equal(new LoginSpritePlacement(0, 660, 1024, 140), layout.Footer);
        Assert.Equal(new LoginSpritePlacement(258, 615, 508, 98), layout.AccountPanel);
        Assert.Equal(new LoginSpritePlacement(267, 26, 494, 140), layout.TopDragon);
        Assert.True(layout.AccountPanel.Y < layout.Footer.Y);
        Assert.True(layout.AccountPanel.Y + layout.AccountPanel.Height > layout.Footer.Y);
    }

    [Fact]
    public void CompactStage_ScalesAllLayersProportionally()
    {
        LoginVisualLayout layout = LoginVisualLayout.Create(new LogicalRenderSize(800, 600));

        Assert.Equal(new LoginSpritePlacement(0, 31, 800, 498), layout.Backdrop);
        Assert.Equal(new LoginSpritePlacement(0, 0, 800, 91), layout.Header);
        Assert.Equal(new LoginSpritePlacement(0, 516, 800, 109), layout.Footer);
        Assert.Equal(new LoginSpritePlacement(202, 480, 397, 77), layout.AccountPanel);
        Assert.Equal(new LoginSpritePlacement(209, 20, 386, 109), layout.TopDragon);
    }

    [Theory]
    [InlineData(0, 600)]
    [InlineData(800, 0)]
    [InlineData(1024, 600)]
    [InlineData(800, 768)]
    public void UnsupportedLogicalAspectRatio_FailsClosed(int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LoginVisualLayout.Create(new LogicalRenderSize(width, height)));
    }
}
