using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Content.Login;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Client.UI.Login.Controls;

/// <summary>maskMC, the black band beneath every login dialog (sprite 262, shape 261).</summary>
internal static class LoginModalMask
{
    /// <summary>The band's colour on a one-based dialog frame.</summary>
    internal static SpriteColor At(int frame) => new(0, 0, 0, (byte)(LoginAccountArtwork.MaskAlpha(frame) * 255 / 256));

    /// <summary>
    /// Fills the band except <paramref name="picture"/>: a held dialog picture already carries the mask beneath
    /// itself.
    /// </summary>
    internal static void DrawAround(OpenGLRenderer renderer, LoginRect band, LoginRect picture, SpriteColor mask)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        renderer.DrawSolidRectangle(band.X, band.Y, band.Width, picture.Y - band.Y, mask);
        renderer.DrawSolidRectangle(band.X, picture.Y + picture.Height, band.Width, band.Y + band.Height - picture.Y - picture.Height, mask);
        renderer.DrawSolidRectangle(band.X, picture.Y, picture.X - band.X, picture.Height, mask);
        renderer.DrawSolidRectangle(picture.X + picture.Width, picture.Y, band.X + band.Width - picture.X - picture.Width, picture.Height, mask);
    }
}
