using OpenConquer.Client.UI.Login;
using OpenConquer.Content.Startup;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Login;

public sealed class LoginRetailFlashLayoutTests
{
    [Fact]
    public void SourceCoordinatesMatchOriginalStartDatAtFullResolution()
    {
        LoginRetailFlashLayout layout = LoginRetailFlashLayout.Create(LoginVisualLayout.Create(new LogicalRenderSize(1024, 768)));
        Assert.Equal(new LoginSpritePlacement(776, 2, 248, 78), layout.Logo);
        Assert.Equal(new LoginSpritePlacement(26, 713, 82, 21), layout.ButtonAt(LoginRetailFlashArtwork.Button.Register));
        Assert.Equal(new LoginSpritePlacement(138, 713, 109, 21), layout.ButtonAt(LoginRetailFlashArtwork.Button.Purchase));
        Assert.Equal(new LoginSpritePlacement(256, 713, 57, 23), layout.ButtonAt(LoginRetailFlashArtwork.Button.Help));
        Assert.Equal(new LoginSpritePlacement(597, 646, 17, 15), layout.ButtonAt(LoginRetailFlashArtwork.Button.Accounts));
        Assert.Equal(new LoginSpritePlacement(625, 647, 22, 19), layout.ButtonAt(LoginRetailFlashArtwork.Button.Save));
        Assert.Equal(new LoginSpritePlacement(656, 646, 20, 21), layout.ButtonAt(LoginRetailFlashArtwork.Button.Clear));
    }

    [Theory]
    [InlineData(1024, 768)]
    [InlineData(800, 600)]
    public void AllSourceControlsFitStageAndDoNotCoverCredentialFields(int width, int height)
    {
        LoginVisualLayout visual = LoginVisualLayout.Create(new LogicalRenderSize(width, height));
        LoginRetailFlashLayout layout = LoginRetailFlashLayout.Create(visual);
        LoginCredentialLayout fields = LoginCredentialLayout.Create(visual);
        foreach (LoginRetailFlashArtwork.Button button in Enum.GetValues<LoginRetailFlashArtwork.Button>())
        {
            LoginSpritePlacement bounds = layout.ButtonAt(button);
            Assert.True(bounds.Width > 0 && bounds.Height > 0);
            Assert.True(bounds.X >= 0 && bounds.Y >= 0);
            Assert.True(bounds.X + bounds.Width <= width && bounds.Y + bounds.Height <= height);
            Assert.False(Overlaps(bounds, fields.AccountField));
            Assert.False(Overlaps(bounds, fields.PasswordField));
        }
        Assert.Equal(width, layout.Logo.X + layout.Logo.Width);
    }

    private static bool Overlaps(LoginSpritePlacement a, LoginSpritePlacement b) =>
        a.X < b.X + b.Width && b.X < a.X + a.Width &&
        a.Y < b.Y + b.Height && b.Y < a.Y + a.Height;
}
