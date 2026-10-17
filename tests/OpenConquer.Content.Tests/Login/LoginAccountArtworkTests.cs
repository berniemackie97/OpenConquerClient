using OpenConquer.Content.Images;
using OpenConquer.Content.Login;

namespace OpenConquer.Content.Tests.Login;

public sealed class LoginAccountArtworkTests
{
    [Theory]
    [InlineData(LoginAccountDialog.Save, 344, 192, 344, 259)]
    [InlineData(LoginAccountDialog.Clear, 296, 192, 361, 259)]
    public void Dialogs_AreCroppedToTheirHeldPicture(LoginAccountDialog dialog, int width, int height, int x, int y)
    {
        RgbaImage image = LoginAccountArtwork.LoadDialog(dialog);

        Assert.Equal((width, height), (image.Width, image.Height));
        Assert.Equal((x, y), LoginAccountArtwork.DialogPosition(dialog));
    }

    [Fact]
    public void Mask_FadesInToTheHeldFrameAndOutAgain()
    {
        int[] alphas = [.. Enumerable.Range(1, LoginAccountArtwork.DialogFrameCount).Select(LoginAccountArtwork.MaskAlpha)];

        Assert.Equal([0, 0, 51, 103, 154, 205, 154, 103, 51, 0], alphas);
        Assert.Equal(205, LoginAccountArtwork.MaskAlpha(LoginAccountArtwork.DialogHeldFrame));
        Assert.Throws<ArgumentOutOfRangeException>(() => LoginAccountArtwork.MaskAlpha(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => LoginAccountArtwork.MaskAlpha(11));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Rows_HaveTheItemButtonSize(bool highlighted)
    {
        RgbaImage row = LoginAccountArtwork.LoadRow(highlighted);

        Assert.Equal((166, 25), (row.Width, row.Height));
    }

    [Theory]
    [InlineData(LoginDialogButton.Ok, LoginButtonState.Over)]
    [InlineData(LoginDialogButton.Ok, LoginButtonState.Down)]
    [InlineData(LoginDialogButton.Cancel, LoginButtonState.Over)]
    [InlineData(LoginDialogButton.Cancel, LoginButtonState.Down)]
    public void DialogButtons_HaveTheirOverAndDownStates(LoginDialogButton button, LoginButtonState state)
    {
        RgbaImage image = LoginAccountArtwork.LoadDialogButton(button, state);

        Assert.Equal((96, 26), (image.Width, image.Height));
    }

    [Fact]
    public void DialogButtonUpStates_ArePartOfTheDialogPicture()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LoginAccountArtwork.LoadDialogButton(LoginDialogButton.Ok, LoginButtonState.Up));
        Assert.Throws<ArgumentOutOfRangeException>(() => LoginAccountArtwork.LoadDialog((LoginAccountDialog)5));
    }
}
