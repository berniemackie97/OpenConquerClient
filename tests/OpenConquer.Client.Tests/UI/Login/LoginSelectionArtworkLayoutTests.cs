using OpenConquer.Client.UI.Login;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Login;

public sealed class LoginSelectionArtworkLayoutTests
{
    [Theory]
    [InlineData(1024, 768, 57, 194, 150, 50, 410, 197, 200, 100)]
    [InlineData(800, 600, 44, 152, 117, 39, 320, 153, 156, 78)]
    public void SelectedArtwork_AnchorsToTheGridWithoutStretching(int width, int height, int regionX, int regionY, int regionWidth,
        int regionHeight, int serverX, int serverY, int serverWidth, int serverHeight)
    {
        LoginVisualLayout layout = LoginVisualLayout.Create(new LogicalRenderSize(width, height));
        Assert.Equal(new LoginSpritePlacement(regionX, regionY, regionWidth, regionHeight), LoginSelectionArtworkLayout.SelectedRegion(layout, 0));
        Assert.Equal(new LoginSpritePlacement(serverX, serverY, serverWidth, serverHeight), LoginSelectionArtworkLayout.SelectedServer(layout, 0));
    }

    [Theory]
    [InlineData(1024, 768)]
    [InlineData(800, 600)]
    public void AllSelectionSlots_FitWithinTheirStageAndRemainCentered(int width, int height)
    {
        LoginVisualLayout layout = LoginVisualLayout.Create(new LogicalRenderSize(width, height));
        for (int i = 0; i < layout.RegionCells.Length; i++)
        {
            LoginSpritePlacement image = LoginSelectionArtworkLayout.SelectedRegion(layout, i);
            AssertFits(image, layout);
            AssertCentered(image, layout.RegionCells[i]);
        }

        for (int i = 0; i < layout.ServerCells.Length; i++)
        {
            LoginSpritePlacement image = LoginSelectionArtworkLayout.SelectedServer(layout, i);
            AssertFits(image, layout);
            AssertCentered(image, layout.ServerCells[i]);
        }
    }

    [Fact]
    public void OutOfRangeSlots_FailWithoutRenderingAnUntrustedLocation()
    {
        LoginVisualLayout layout = LoginVisualLayout.Create(new LogicalRenderSize(1024, 768));
        Assert.Throws<ArgumentOutOfRangeException>(() => LoginSelectionArtworkLayout.SelectedRegion(layout, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => LoginSelectionArtworkLayout.SelectedRegion(layout, 16));
        Assert.Throws<ArgumentOutOfRangeException>(() => LoginSelectionArtworkLayout.SelectedServer(layout, 12));
    }

    private static void AssertCentered(LoginSpritePlacement image, LoginSpritePlacement cell)
    {
        Assert.InRange(Math.Abs((long)image.X * 2 + image.Width - ((long)cell.X * 2 + cell.Width)), 0L, 1L);
        Assert.InRange(Math.Abs((long)image.Y * 2 + image.Height - ((long)cell.Y * 2 + cell.Height)), 0L, 1L);
    }

    private static void AssertFits(LoginSpritePlacement rect, LoginVisualLayout layout)
    {
        Assert.True(rect.X >= 0 && rect.Y >= 0 && rect.Width > 0 && rect.Height > 0);
        Assert.True(rect.X + rect.Width <= layout.Width && rect.Y + rect.Height <= layout.Height);
    }
}
