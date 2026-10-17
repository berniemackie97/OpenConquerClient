using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Login.Stage;

public sealed class LoginStageLayoutTests
{
    [Fact]
    public void ReferenceStage_PlacesTheRetailRootDisplayList()
    {
        LoginStageLayout layout = LoginStageLayout.Create(new LogicalRenderSize(1024, 768));

        Assert.Equal(new LoginRect(0, 41, 1024, 637), layout.Backdrop);
        Assert.Equal(new LoginRect(0, 0, 1024, 768), layout.StageBorder);
        Assert.Equal(new LoginRect(43, 162, 340, 434), layout.RegionPanel);
        Assert.Equal(new LoginRect(256, 606, 508, 98), layout.AccountPanel);
        Assert.Equal(new LoginRect(18, 15, 190, 18), layout.VersionLabel);
        Assert.Equal(new LoginRect(137, 603, 38, 34), layout.RegionPagePrevious);
        Assert.Equal(new LoginRect(249, 603, 40, 34), layout.RegionPageNext);
        Assert.Equal(new LoginRect(178, 609, 70, 22), layout.RegionPageText);
    }

    [Fact]
    public void ReferenceStage_RegionOriginsRunDownEachColumnAtTheSettledSpringPositions()
    {
        LoginStageLayout layout = LoginStageLayout.Create(new LogicalRenderSize(1024, 768));

        Assert.Equal(16, layout.RegionOrigins.Length);
        Assert.Equal(new LoginPoint(59, 176), layout.RegionOrigins[0]);
        Assert.Equal(new LoginPoint(59, 525), layout.RegionOrigins[7]);
        Assert.Equal(new LoginPoint(214, 176), layout.RegionOrigins[8]);
        Assert.Equal(new LoginPoint(214, 525), layout.RegionOrigins[15]);
        Assert.Equal(new LoginRect(59, 175, 151, 53), layout.RegionTiles[0]);
        Assert.Equal(new LoginRect(60, 178, 149, 49), layout.RegionHitAreas[0]);
        Assert.Equal(new LoginRect(215, 527, 149, 49), layout.RegionHitAreas[15]);
    }

    [Fact]
    public void ReferenceStage_ServerOriginsFollowSprite180PositionData()
    {
        LoginStageLayout layout = LoginStageLayout.Create(new LogicalRenderSize(1024, 768));

        Assert.Equal(12, layout.ServerOrigins.Length);
        Assert.Equal(new LoginPoint(426, 194), layout.ServerOrigins[0]);
        // ss3 is authored one pixel below the rest of its row.
        Assert.Equal(new LoginPoint(762, 195), layout.ServerOrigins[2]);
        Assert.Equal(new LoginPoint(762, 467), layout.ServerOrigins[11]);
        Assert.Equal(new LoginRect(432, 195, 150, 83), layout.ServerHitAreas[0]);
        Assert.Equal(new LoginRect(456, 259, 120, 15), layout.ServerLabels[0]);
    }

    [Fact]
    public void CompactStage_ScalesEveryLayerAndHolder()
    {
        LoginStageLayout layout = LoginStageLayout.Create(new LogicalRenderSize(800, 600));

        Assert.Equal(new LoginRect(0, 32, 800, 498), layout.Backdrop);
        Assert.Equal(new LoginRect(0, 0, 800, 600), layout.StageBorder);
        Assert.Equal(new LoginRect(34, 127, 266, 339), layout.RegionPanel);
        Assert.Equal(new LoginRect(200, 473, 397, 77), layout.AccountPanel);
        Assert.Equal(new LoginPoint(46, 138), layout.RegionOrigins[0]);
        Assert.Equal(new LoginRect(46, 137, 118, 41), layout.RegionTiles[0]);
        Assert.Equal(new LoginPoint(333, 151), layout.ServerOrigins[0]);
        Assert.Equal(new LoginRect(338, 153, 117, 64), layout.ServerHitAreas[0]);
    }

    [Theory]
    [InlineData(1024, 768)]
    [InlineData(800, 600)]
    public void HitAreas_AreDisjointAndInsideTheStage(int width, int height)
    {
        LoginStageLayout layout = LoginStageLayout.Create(new LogicalRenderSize(width, height));

        AssertDisjointAndInside(layout.RegionHitAreas, layout);
        AssertDisjointAndInside(layout.ServerHitAreas, layout);
    }

    [Fact]
    public void Scale_RoundsNegativeOffsetsLikePositiveOnes()
    {
        LoginStageLayout layout = LoginStageLayout.Create(new LogicalRenderSize(800, 600));

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
        Assert.Throws<ArgumentOutOfRangeException>(() => LoginStageLayout.Create(new LogicalRenderSize(width, height)));
    }

    private static void AssertDisjointAndInside(ReadOnlySpan<LoginRect> areas, LoginStageLayout layout)
    {
        for (int index = 0; index < areas.Length; index++)
        {
            LoginRect area = areas[index];
            Assert.True(area.X >= 0 && area.Y >= 0 && area.Width > 0 && area.Height > 0);
            Assert.True(area.X + area.Width <= layout.Width && area.Y + area.Height <= layout.Height);

            for (int other = index + 1; other < areas.Length; other++)
            {
                LoginRect next = areas[other];
                Assert.False(area.X < next.X + next.Width && next.X < area.X + area.Width &&
                    area.Y < next.Y + next.Height && next.Y < area.Y + area.Height);
            }
        }
    }
}
