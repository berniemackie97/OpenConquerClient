using OpenConquer.Client.UI.Login;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Login;

public sealed class LoginVisualLayoutTests
{
    [Fact]
    public void ReferenceStage_PlacesTheRetailRootDisplayList()
    {
        LoginVisualLayout layout = LoginVisualLayout.Create(new LogicalRenderSize(1024, 768));

        Assert.Equal(new LoginSpritePlacement(0, 41, 1024, 637), layout.Backdrop);
        Assert.Equal(new LoginSpritePlacement(0, 0, 1024, 768), layout.StageBorder);
        Assert.Equal(new LoginSpritePlacement(43, 162, 340, 434), layout.RegionPanel);
        Assert.Equal(new LoginSpritePlacement(256, 606, 508, 98), layout.AccountPanel);
        Assert.Equal(new LoginSpritePlacement(18, 15, 190, 18), layout.VersionLabel);
        Assert.Equal(new LoginSpritePlacement(137, 603, 38, 34), layout.RegionPagePrevious);
        Assert.Equal(new LoginSpritePlacement(249, 603, 40, 34), layout.RegionPageNext);
        Assert.Equal(new LoginSpritePlacement(178, 609, 70, 22), layout.RegionPageText);
    }

    [Fact]
    public void ReferenceStage_RegionHoldersRunDownEachColumnAtTheSettledSpringPositions()
    {
        LoginVisualLayout layout = LoginVisualLayout.Create(new LogicalRenderSize(1024, 768));

        Assert.Equal(16, layout.RegionHolders.Length);
        Assert.Equal(new LoginStagePoint(59, 176), layout.RegionHolders[0]);
        Assert.Equal(new LoginStagePoint(59, 525), layout.RegionHolders[7]);
        Assert.Equal(new LoginStagePoint(214, 176), layout.RegionHolders[8]);
        Assert.Equal(new LoginStagePoint(214, 525), layout.RegionHolders[15]);
        Assert.Equal(new LoginSpritePlacement(59, 175, 151, 53), layout.RegionTiles[0]);
        Assert.Equal(new LoginSpritePlacement(60, 178, 149, 49), layout.RegionHitAreas[0]);
        Assert.Equal(new LoginSpritePlacement(215, 527, 149, 49), layout.RegionHitAreas[15]);
    }

    [Fact]
    public void ReferenceStage_ServerHoldersFollowSprite180PositionData()
    {
        LoginVisualLayout layout = LoginVisualLayout.Create(new LogicalRenderSize(1024, 768));

        Assert.Equal(12, layout.ServerHolders.Length);
        Assert.Equal(new LoginStagePoint(426, 194), layout.ServerHolders[0]);
        // ss3 is authored one pixel below the rest of its row.
        Assert.Equal(new LoginStagePoint(762, 195), layout.ServerHolders[2]);
        Assert.Equal(new LoginStagePoint(762, 467), layout.ServerHolders[11]);
        Assert.Equal(new LoginSpritePlacement(432, 195, 150, 83), layout.ServerHitAreas[0]);
        Assert.Equal(new LoginSpritePlacement(456, 259, 120, 15), layout.ServerLabels[0]);
    }

    [Fact]
    public void CompactStage_ScalesEveryLayerAndHolder()
    {
        LoginVisualLayout layout = LoginVisualLayout.Create(new LogicalRenderSize(800, 600));

        Assert.Equal(new LoginSpritePlacement(0, 32, 800, 498), layout.Backdrop);
        Assert.Equal(new LoginSpritePlacement(0, 0, 800, 600), layout.StageBorder);
        Assert.Equal(new LoginSpritePlacement(34, 127, 266, 339), layout.RegionPanel);
        Assert.Equal(new LoginSpritePlacement(200, 473, 397, 77), layout.AccountPanel);
        Assert.Equal(new LoginStagePoint(46, 138), layout.RegionHolders[0]);
        Assert.Equal(new LoginSpritePlacement(46, 137, 118, 41), layout.RegionTiles[0]);
        Assert.Equal(new LoginStagePoint(333, 151), layout.ServerHolders[0]);
        Assert.Equal(new LoginSpritePlacement(338, 153, 117, 64), layout.ServerHitAreas[0]);
    }

    [Theory]
    [InlineData(1024, 768)]
    [InlineData(800, 600)]
    public void HitAreas_AreDisjointAndInsideTheStage(int width, int height)
    {
        LoginVisualLayout layout = LoginVisualLayout.Create(new LogicalRenderSize(width, height));

        AssertDisjointAndInside(layout.RegionHitAreas, layout);
        AssertDisjointAndInside(layout.ServerHitAreas, layout);
    }

    [Fact]
    public void Scale_RoundsNegativeOffsetsLikePositiveOnes()
    {
        LoginVisualLayout layout = LoginVisualLayout.Create(new LogicalRenderSize(800, 600));

        Assert.Equal(-1, layout.Scale(-1));
        Assert.Equal(1, layout.Scale(1));
        Assert.Equal(-8, layout.Scale(-10));
        Assert.Equal(8, layout.Scale(10));
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

    private static void AssertDisjointAndInside(ReadOnlySpan<LoginSpritePlacement> areas, LoginVisualLayout layout)
    {
        for (int index = 0; index < areas.Length; index++)
        {
            LoginSpritePlacement area = areas[index];
            Assert.True(area.X >= 0 && area.Y >= 0 && area.Width > 0 && area.Height > 0);
            Assert.True(area.X + area.Width <= layout.Width && area.Y + area.Height <= layout.Height);

            for (int other = index + 1; other < areas.Length; other++)
            {
                LoginSpritePlacement next = areas[other];
                Assert.False(area.X < next.X + next.Width && next.X < area.X + area.Width &&
                    area.Y < next.Y + next.Height && next.Y < area.Y + area.Height);
            }
        }
    }
}
