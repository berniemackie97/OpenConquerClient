using OpenConquer.Client.UI.Login;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Login;

public sealed class LoginServerIconLayoutTests
{
    [Theory]
    [InlineData(1024, 768)]
    [InlineData(800, 600)]
    public void EachIconFitsWithinExistingServerCellsWithoutChangingGridGeometry(int width, int height)
    {
        LoginVisualLayout layout = LoginVisualLayout.Create(new LogicalRenderSize(width, height));

        foreach (LoginSpritePlacement cell in layout.ServerCells)
        {
            foreach ((int imageWidth, int imageHeight) in new[] { (156, 56), (164, 85), (154, 54) })
            {
                LoginSpritePlacement image = LoginServerIconLayout.Fit(cell, imageWidth, imageHeight);
                Assert.True(image.Width > 0 && image.Height > 0);
                Assert.True(image.X >= cell.X && image.Y >= cell.Y);
                Assert.True((long)image.X + image.Width <= (long)cell.X + cell.Width);
                Assert.True((long)image.Y + image.Height <= (long)cell.Y + cell.Height);
                Assert.True(image.Width * (long)imageHeight <= image.Height * (long)imageWidth + imageWidth);
            }
        }
    }

    [Fact]
    public void NamedIconIdentityIsNotInferredFromServerIdOrSlot()
    {
        LoginServer server = new("arbitrary-uuid", "Some Server", "ProtocolName", "g02s13");
        Assert.Equal("g02s13", server.IconId);
        Assert.Null(new LoginServer("arbitrary", "Other Server", "OtherProtocol").IconId);
        Assert.Throws<ArgumentException>(() => new LoginServer("id", "Display", "Protocol", "../../etc"));
    }
}
