using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Content.Login;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Client.UI.Login.Controls;

internal static class LoginModalMask
{
    internal static SpriteColor At(int frame)
    {
        return new SpriteColor(0, 0, 0, (byte)(LoginAccountArtwork.MaskAlpha(frame) * 255 / 256));
    }

    internal static void DrawAround(OpenGLRenderer renderer, LoginRect band, LoginRect picture, SpriteColor mask)
    {
        ArgumentNullException.ThrowIfNull(renderer);

        renderer.DrawSolidRectangle(band.X, band.Y, band.Width, picture.Y - band.Y, mask);
        renderer.DrawSolidRectangle(band.X, picture.Y + picture.Height, band.Width, band.Y + band.Height - picture.Y - picture.Height, mask);
        renderer.DrawSolidRectangle(band.X, picture.Y, picture.X - band.X, picture.Height, mask);
        renderer.DrawSolidRectangle(picture.X + picture.Width, picture.Y, band.X + band.Width - picture.X - picture.Width, picture.Height, mask);
    }
}
