using OpenConquer.Client.UI.Login;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Login;

public sealed class LoginVisualLayoutTests
{
    [Fact]
    public void ReferenceStage_PreservesVerifiedOrnamentAndSelectionAnchors()
    {
        LoginVisualLayout layout = LoginVisualLayout.Create(new LogicalRenderSize(1024, 768));
        Assert.Equal(new LoginSpritePlacement(267, 26, 494, 140), layout.TopDragon);
        Assert.Equal(new LoginSpritePlacement(150, 381, 158, 58), layout.RegionSlot);
        Assert.Equal(new LoginSpritePlacement(685, 373, 158, 58), layout.ServerSlot);
    }

    [Fact]
    public void CompactStage_UsesProportionalIntegerRoundedCoordinates()
    {
        LoginVisualLayout layout = LoginVisualLayout.Create(new LogicalRenderSize(800, 600));
        Assert.Equal(new LoginSpritePlacement(209, 20, 386, 109), layout.TopDragon);
        Assert.Equal(new LoginSpritePlacement(117, 298, 123, 45), layout.RegionSlot);
        Assert.Equal(new LoginSpritePlacement(535, 291, 123, 45), layout.ServerSlot);
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
