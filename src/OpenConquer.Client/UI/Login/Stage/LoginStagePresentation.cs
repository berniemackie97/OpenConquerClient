using OpenConquer.Content.Images;
using OpenConquer.Content.Login;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;

namespace OpenConquer.Client.UI.Login.Stage;

/// <summary>The fixed stage layers: backdrop, border ornament and account panel, for one OpenGL context.</summary>
internal sealed class LoginStagePresentation : IDisposable
{
    private readonly ResourceOwnership _ownership = new();
    private readonly LoginStageLayout _layout;
    private readonly OpenGLTexture2D _backdrop;
    private readonly OpenGLTexture2D _border;
    private readonly OpenGLTexture2D _accountPanel;

    public LoginStagePresentation(OpenGLGraphicsDevice graphicsDevice, LoginStageLayout layout)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));

        try
        {
            _backdrop = Create(graphicsDevice, LoginStageArtwork.Layer.Backdrop);
            _border = Create(graphicsDevice, LoginStageArtwork.Layer.Border);
            _accountPanel = Create(graphicsDevice, LoginStageArtwork.Layer.AccountPanel);
        }
        catch
        {
            _ownership.DisposeAfterInitializationFailure();
            throw;
        }
    }

    /// <summary>Root depth 1: shape 11.</summary>
    public void DrawBackdrop(OpenGLRenderer renderer) => Draw(renderer, _backdrop, _layout.Backdrop);

    /// <summary>Root depth 216: shape 400, above both selection panels and below <c>enterMC</c>.</summary>
    public void DrawBorder(OpenGLRenderer renderer) => Draw(renderer, _border, _layout.StageBorder);

    /// <summary>Root depth 221 <c>enterMC</c>: shape 328.</summary>
    public void DrawAccountPanel(OpenGLRenderer renderer) => Draw(renderer, _accountPanel, _layout.AccountPanel);

    public void Dispose() => _ownership.Dispose();

    private static void Draw(OpenGLRenderer renderer, OpenGLTexture2D texture, LoginRect bounds)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        renderer.DrawSprite(texture, bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    private OpenGLTexture2D Create(OpenGLGraphicsDevice graphicsDevice, LoginStageArtwork.Layer layer)
    {
        RgbaImage image = LoginStageArtwork.Load(layer);
        return _ownership.Track(graphicsDevice.CreateTexture2D(image.Width, image.Height, image.Pixels.Span));
    }
}
