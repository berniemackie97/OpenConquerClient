using OpenConquer.Content.Images;
using OpenConquer.Content.Login;

namespace OpenConquer.Content.Tests.Login;

public sealed class LoginRegistrationArtworkTests
{
    [Fact]
    public void ClearDialogBody_IsOneRepeatedRowBetweenItsDividers()
    {
        RgbaImage clear = LoginAccountArtwork.LoadDialog(LoginAccountDialog.Clear);
        ReadOnlySpan<byte> pixels = clear.Pixels.Span;
        int stride = clear.Width * 4;
        ReadOnlySpan<byte> body = pixels.Slice(29 * stride, stride);

        for (int row = 29; row < 145; row++)
        {
            Assert.True(body.SequenceEqual(pixels.Slice(row * stride, stride)), $"Row {row} differs from the body row.");
        }

        Assert.False(body.SequenceEqual(pixels.Slice(145 * stride, stride)));
    }

    [Fact]
    public void Dialog_LengthensClearDialogBodyAndKeepsItsHeaderAndFooter()
    {
        RgbaImage clear = LoginAccountArtwork.LoadDialog(LoginAccountDialog.Clear);
        RgbaImage dialog = LoginRegistrationArtwork.LoadDialog();
        int stride = clear.Width * 4;
        int extension = LoginRegistrationArtwork.BodyExtension;

        Assert.Equal((LoginRegistrationArtwork.DialogWidth, LoginRegistrationArtwork.DialogHeight), (dialog.Width, dialog.Height));
        Assert.Equal(clear.Height + extension, dialog.Height);
        ReadOnlySpan<byte> source = clear.Pixels.Span;
        ReadOnlySpan<byte> result = dialog.Pixels.Span;
        Assert.True(source[..(145 * stride)].SequenceEqual(result[..(145 * stride)]));
        for (int row = 145; row < 145 + extension; row++)
        {
            Assert.True(source.Slice(29 * stride, stride).SequenceEqual(result.Slice(row * stride, stride)));
        }

        Assert.True(source[(145 * stride)..].SequenceEqual(result[((145 + extension) * stride)..]));
    }

    [Fact]
    public void Dialog_KeepsClearDialogCentre()
    {
        (int clearX, int clearY) = LoginAccountArtwork.DialogPosition(LoginAccountDialog.Clear);
        (int x, int y) = LoginRegistrationArtwork.DialogPosition;

        Assert.Equal((361, 239), (x, y));
        Assert.Equal(clearX, x);
        Assert.Equal(clearY + 192 / 2, y + LoginRegistrationArtwork.DialogHeight / 2);
    }

    [Fact]
    public void Field_IsThePasswordBoxOfTheAccountPanel()
    {
        RgbaImage panel = LoginStageArtwork.Load(LoginStageArtwork.Layer.AccountPanel);
        RgbaImage field = LoginRegistrationArtwork.LoadField();

        Assert.Equal((LoginRegistrationArtwork.FieldWidth, LoginRegistrationArtwork.FieldHeight), (field.Width, field.Height));
        for (int row = 0; row < field.Height; row++)
        {
            ReadOnlySpan<byte> expected = panel.Pixels.Span.Slice(((65 + row) * panel.Width + 210) * 4, field.Width * 4);
            Assert.True(expected.SequenceEqual(field.Pixels.Span.Slice(row * field.Width * 4, field.Width * 4)));
        }
    }
}
