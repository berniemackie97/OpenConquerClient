using OpenConquer.Client.UI.Login;
using OpenConquer.Content.Images;
using OpenConquer.Content.Startup;
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
        Assert.Equal(new LoginSpritePlacement(0, 642, 1024, 140), layout.Footer);
        Assert.Equal(new LoginSpritePlacement(258, 637, 508, 98), layout.AccountPanel);
        Assert.Equal(new LoginSpritePlacement(267, 26, 494, 140), layout.TopDragon);
        Assert.True(layout.AccountPanel.Y < layout.Footer.Y);
        Assert.True(layout.AccountPanel.Y + layout.AccountPanel.Height > layout.Footer.Y);
    }

    [Fact]
    public void ReferenceStage_SelectionChromeUsesBoundedDistinctSlots()
    {
        LoginVisualLayout layout = LoginVisualLayout.Create(new LogicalRenderSize(1024, 768));
        Assert.Equal(16, layout.RegionCells.Length);
        Assert.Equal(12, layout.ServerCells.Length);
        Assert.Equal(new LoginSpritePlacement(55, 194, 154, 50), layout.RegionCells[0]);
        Assert.Equal(new LoginSpritePlacement(216, 194, 154, 50), layout.RegionCells[1]);
        Assert.Equal(new LoginSpritePlacement(216, 544, 154, 50), layout.RegionCells[^1]);
        Assert.Equal(new LoginSpritePlacement(431, 218, 158, 58), layout.ServerCells[0]);
        Assert.Equal(new LoginSpritePlacement(767, 488, 158, 58), layout.ServerCells[^1]);
        AssertSelectionFits(layout);
    }

    [Fact]
    public void CompactStage_ScalesAllLayersProportionally()
    {
        LoginVisualLayout layout = LoginVisualLayout.Create(new LogicalRenderSize(800, 600));

        Assert.Equal(new LoginSpritePlacement(0, 31, 800, 498), layout.Backdrop);
        Assert.Equal(new LoginSpritePlacement(0, 0, 800, 91), layout.Header);
        Assert.Equal(new LoginSpritePlacement(0, 502, 800, 109), layout.Footer);
        Assert.Equal(new LoginSpritePlacement(202, 498, 397, 77), layout.AccountPanel);
        Assert.Equal(new LoginSpritePlacement(209, 20, 386, 109), layout.TopDragon);
        Assert.Equal(new LoginSpritePlacement(43, 152, 120, 39), layout.RegionCells[0]);
        Assert.Equal(new LoginSpritePlacement(169, 425, 120, 39), layout.RegionCells[^1]);
        Assert.Equal(new LoginSpritePlacement(337, 170, 123, 45), layout.ServerCells[0]);
        Assert.Equal(new LoginSpritePlacement(599, 381, 123, 45), layout.ServerCells[^1]);
        AssertSelectionFits(layout);
    }

    [Theory]
    [InlineData(1024, 768)]
    [InlineData(800, 600)]
    public void Footer_IsOpaqueBeforeBackdropEnds_AndContinuesPastBottomEdge(int width, int height)
    {
        LoginVisualLayout layout = LoginVisualLayout.Create(new LogicalRenderSize(width, height));
        RgbaImage footer = LoginArtwork.LoadFooter();
        int firstOpaqueRow = 0;

        while (firstOpaqueRow < footer.Height)
        {
            bool opaque = true;
            for (int x = 0; x < footer.Width; x++)
            {
                if (footer.Pixels.Span[checked((firstOpaqueRow * footer.Width + x) * 4 + 3)] != byte.MaxValue)
                {
                    opaque = false;
                    break;
                }
            }

            if (opaque)
            {
                break;
            }

            firstOpaqueRow++;
        }

        Assert.Equal(32, firstOpaqueRow);
        for (int y = firstOpaqueRow; y < footer.Height; y++)
        {
            for (int x = 0; x < footer.Width; x++)
            {
                Assert.Equal(byte.MaxValue, footer.Pixels.Span[checked((y * footer.Width + x) * 4 + 3)]);
            }
        }

        int scaledOpaqueRow = checked((int)(((long)firstOpaqueRow * height + 384) / 768));
        Assert.True(layout.Footer.Y + scaledOpaqueRow <= layout.Backdrop.Y + layout.Backdrop.Height);
        Assert.True(layout.Footer.Y + layout.Footer.Height >= layout.Height);
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

    private static void AssertSelectionFits(LoginVisualLayout layout)
    {
        foreach (LoginSpritePlacement placement in layout.RegionCells)
        {
            Assert.True(placement.X >= 0 && placement.Y >= 0 && placement.X + placement.Width <= layout.Width && placement.Y + placement.Height <= layout.Height);
        }

        foreach (LoginSpritePlacement placement in layout.ServerCells)
        {
            Assert.True(placement.X >= 0 && placement.Y >= 0 && placement.X + placement.Width <= layout.Width && placement.Y + placement.Height <= layout.Height);
        }
    }
}
