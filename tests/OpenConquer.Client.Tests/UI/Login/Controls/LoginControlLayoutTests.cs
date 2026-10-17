using OpenConquer.Client.UI.Login.Controls;
using OpenConquer.Client.UI.Login.Credentials;
using OpenConquer.Client.UI.Login.Selection;
using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Content.Login;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Login.Controls;

public sealed class LoginControlLayoutTests
{
    [Theory]
    [InlineData(LoginButton.Register, 26, 713, 82, 21)]
    [InlineData(LoginButton.Purchase, 138, 713, 109, 21)]
    [InlineData(LoginButton.Help, 256, 713, 57, 23)]
    [InlineData(LoginButton.Accounts, 597, 646, 17, 15)]
    [InlineData(LoginButton.Save, 625, 647, 22, 19)]
    [InlineData(LoginButton.Clear, 656, 646, 20, 21)]
    [InlineData(LoginButton.Enter, 650, 704, 132, 40)]
    [InlineData(LoginButton.Exit, 859, 704, 132, 40)]
    public void ReferenceStage_UsesTheEnterMcPlacements(LoginButton button, int x, int y, int width, int height)
    {
        LoginButtonSet<LoginButton> buttons = LoginControlLayout.CreateButtons(LoginStageLayout.Create(new LogicalRenderSize(1024, 768)));

        Assert.Equal(new LoginRect(x, y, width, height), buttons.BoundsOf(button));
    }

    [Fact]
    public void CompactStage_ScalesEnterAndExit()
    {
        LoginButtonSet<LoginButton> buttons = LoginControlLayout.CreateButtons(LoginStageLayout.Create(new LogicalRenderSize(800, 600)));

        Assert.Equal(new LoginRect(508, 550, 103, 31), buttons.BoundsOf(LoginButton.Enter));
        Assert.Equal(671, buttons.BoundsOf(LoginButton.Exit).X);
    }

    [Theory]
    [InlineData(1024, 768)]
    [InlineData(800, 600)]
    public void Buttons_FitTheStageAndCoverNoFieldOrTile(int width, int height)
    {
        LoginStageLayout layout = LoginStageLayout.Create(new LogicalRenderSize(width, height));
        LoginButtonSet<LoginButton> buttons = LoginControlLayout.CreateButtons(layout);
        LoginCredentialLayout fields = LoginCredentialLayout.Create(layout);
        LoginRect[] tiles = [.. layout.RegionHitAreas.ToArray(), .. layout.ServerHitAreas.ToArray(), LoginDirectoryStatus.Bounds(layout)];

        foreach (LoginButton button in LoginControlLayout.Buttons)
        {
            LoginRect bounds = buttons.BoundsOf(button);
            Assert.True(bounds.X >= 0 && bounds.Y >= 0 && bounds.X + bounds.Width <= width && bounds.Y + bounds.Height <= height);
            Assert.False(Overlaps(bounds, fields.AccountField));
            Assert.False(Overlaps(bounds, fields.PasswordField));
            Assert.All(tiles, tile => Assert.False(Overlaps(bounds, tile)));
        }

        Assert.Equal(width, layout.Logo.X + layout.Logo.Width);
    }

    private static bool Overlaps(LoginRect left, LoginRect right) =>
        left.X < right.X + right.Width && right.X < left.X + left.Width && left.Y < right.Y + right.Height && right.Y < left.Y + left.Height;
}
