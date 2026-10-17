using OpenConquer.Content.Startup;

namespace OpenConquer.Content.Tests.Startup;

public sealed class LoginRetailPanelArtworkTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void EveryAuthoredDialogFrameHasVerifiedFullStageDimensions(int dialog)
    {
        for (int frame = 2; frame <= 9; frame++)
        {
            var image = LoginRetailPanelArtwork.LoadDialog(dialog, frame);
            Assert.Equal(1024, image.Width);
            Assert.Equal(768, image.Height);
            Assert.Equal(1024 * 768 * 4, image.Pixels.Length);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OriginalDynamicHistoryRowsHaveVerifiableArtwork(bool highlighted)
    {
        var image = LoginRetailPanelArtwork.LoadRow(highlighted);
        Assert.Equal(166, image.Width);
        Assert.Equal(25, image.Height);
        Assert.Equal(166 * 25 * 4, image.Pixels.Length);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(0, 2)]
    [InlineData(1, 1)]
    [InlineData(1, 2)]
    public void OriginalDialogButtonStatesAreVerified(int button, int state)
    {
        var image = LoginRetailPanelArtwork.LoadDialogButton(button, state);
        Assert.Equal(96, image.Width);
        Assert.Equal(26, image.Height);
    }

    [Fact]
    public void InvalidResourceIndicesFailClosed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LoginRetailPanelArtwork.LoadDialog(2, 6));
        Assert.Throws<ArgumentOutOfRangeException>(() => LoginRetailPanelArtwork.LoadDialog(0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => LoginRetailPanelArtwork.LoadDialogButton(-1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => LoginRetailPanelArtwork.LoadDialogButton(0, 0));
    }
}
