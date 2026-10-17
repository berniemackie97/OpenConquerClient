using OpenConquer.Content.Images;
using OpenConquer.Content.Login;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;

namespace OpenConquer.Client.UI.Login.Stage;

/// <summary>Root depth 274: logo.swf, looping from the stage frame it is loaded on.</summary>
internal sealed class LoginLogoPresentation : IDisposable
{
    private readonly ResourceOwnership _ownership = new();
    private readonly LoginStageLayout _layout;
    private readonly OpenGLTexture2D[] _pictures = new OpenGLTexture2D[LoginLogoArtwork.PictureCount];

    public LoginLogoPresentation(OpenGLGraphicsDevice graphicsDevice, LoginStageLayout layout)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));

        try
        {
            for (int picture = 0; picture < _pictures.Length; picture++)
            {
                RgbaImage image = LoginLogoArtwork.LoadPicture(picture);
                _pictures[picture] = _ownership.Track(graphicsDevice.CreateTexture2D(image.Width, image.Height, image.Pixels.Span));
            }
        }
        catch
        {
            _ownership.DisposeAfterInitializationFailure();
            throw;
        }
    }

    /// <param name="renderer">The active renderer.</param>
    /// <param name="frame">Stage frames since the logo was loaded.</param>
    public void Draw(OpenGLRenderer renderer, long frame)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentOutOfRangeException.ThrowIfNegative(frame);

        OpenGLTexture2D picture = _pictures[LoginLogoArtwork.PictureAt((int)(frame % LoginLogoArtwork.FrameCount))];
        LoginRect bounds = _layout.Logo;
        renderer.DrawSprite(picture, bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    public void Dispose() => _ownership.Dispose();
}
