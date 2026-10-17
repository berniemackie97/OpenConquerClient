using OpenConquer.Client.UI.Login;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.Tests.UI.Login;

public sealed class LoginRetailCredentialLayoutTests
{
    [Fact]
    public void ReferenceStage_UsesTheNativeEditTextBoxes()
    {
        LoginVisualLayout layout = LoginVisualLayout.Create(new LogicalRenderSize(1024, 768));
        LoginCredentialLayout fields = LoginCredentialLayout.Create(layout);

        // enterMC fields 363 (470.5, 643.4, 122x22.1) and 364 (470.15, 674.45, 140.7x22.1).
        Assert.Equal(new LoginSpritePlacement(471, 643, 122, 23), fields.AccountField);
        Assert.Equal(new LoginSpritePlacement(470, 674, 141, 23), fields.PasswordField);
        Assert.Equal(256, layout.AccountPanel.X);
        Assert.Equal(606, layout.AccountPanel.Y);
    }

    [Fact]
    public void CompactStage_ScalesTheSameNativeEditTextBoxes()
    {
        LoginVisualLayout layout = LoginVisualLayout.Create(new LogicalRenderSize(800, 600));
        LoginCredentialLayout fields = LoginCredentialLayout.Create(layout);

        Assert.Equal(new LoginSpritePlacement(368, 503, 95, 17), fields.AccountField);
        Assert.Equal(new LoginSpritePlacement(367, 527, 110, 17), fields.PasswordField);
        Assert.Equal(473, layout.AccountPanel.Y);
    }

    [Theory]
    [InlineData(1024, 768)]
    [InlineData(800, 600)]
    public void BothFieldsStayWithinOriginalPanelAndRemainDisjoint(int width, int height)
    {
        LoginVisualLayout layout = LoginVisualLayout.Create(new LogicalRenderSize(width, height));
        LoginCredentialLayout fields = LoginCredentialLayout.Create(layout);

        foreach (LoginSpritePlacement field in new[] { fields.AccountField, fields.PasswordField })
        {
            Assert.True(field.Width >= 4 && field.Height >= 5);
            Assert.True(field.X > layout.AccountPanel.X && field.Y > layout.AccountPanel.Y);
            Assert.True((long)field.X + field.Width < (long)layout.AccountPanel.X + layout.AccountPanel.Width);
            Assert.True((long)field.Y + field.Height < (long)layout.AccountPanel.Y + layout.AccountPanel.Height);
        }

        Assert.True((long)fields.AccountField.Y + fields.AccountField.Height < fields.PasswordField.Y);
    }

    [Theory]
    [InlineData(1024, 768)]
    [InlineData(800, 600)]
    public void HitTestingTargetsOnlyNativeFieldInteriors(int width, int height)
    {
        LoginVisualLayout visual = LoginVisualLayout.Create(new LogicalRenderSize(width, height));
        LoginCredentialLayout fields = LoginCredentialLayout.Create(visual);
        LoginServerSelection selection = new();
        selection.ReplaceCatalog(new LoginServerCatalog([new LoginRegion("region", "Region", [new LoginServer("server", "Server", "NativeServer")])]));
        Assert.True(selection.SelectRegionSlot(0));
        Assert.True(selection.SelectServerSlot(0));
        using LoginCredentialInput input = new(selection, fields);

        Assert.False(input.PointerPressed(true, visual.AccountPanel.X + 10, visual.AccountPanel.Y + 10));
        Assert.True(input.PointerPressed(true, fields.AccountField.X + 1, fields.AccountField.Y + 1));
        input.TextEntered('A');
        Assert.Equal(LoginCredentialFocus.Account, input.Focus);
        Assert.True(input.PointerPressed(true, fields.PasswordField.X + 1, fields.PasswordField.Y + 1));
        input.TextEntered('P');
        Assert.Equal(LoginCredentialFocus.Password, input.Focus);
        Assert.Equal("*", input.PasswordMask);
        Assert.False(input.PointerPressed(true, fields.PasswordField.X + fields.PasswordField.Width, fields.PasswordField.Y));
        Assert.Equal(0, input.PasswordLength);
        Assert.Equal("A", input.AccountDisplay);
    }
}
