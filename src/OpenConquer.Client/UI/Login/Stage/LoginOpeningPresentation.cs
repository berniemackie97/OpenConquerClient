using OpenConquer.Content.Images;
using OpenConquer.Content.Login;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;

namespace OpenConquer.Client.UI.Login.Stage;

/// <summary>
/// The one-shot loginFlash.swf opening, drawn over the whole stage. Its textures are released as soon as the
/// last frame has been shown.
/// </summary>
internal sealed class LoginOpeningPresentation : IDisposable
{
    private readonly ResourceOwnership _ownership = new();
    private readonly LoginStageLayout _layout;
    private readonly OpenGLTexture2D[] _frames = new OpenGLTexture2D[LoginOpeningArtwork.FrameCount];
    private bool _released;

    public LoginOpeningPresentation(OpenGLGraphicsDevice graphicsDevice, LoginStageLayout layout)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));

        try
        {
            for (int frame = 0; frame < _frames.Length; frame++)
            {
                RgbaImage image = LoginOpeningArtwork.LoadFrame(frame);
                _frames[frame] = _ownership.Track(graphicsDevice.CreateTexture2D(image.Width, image.Height, image.Pixels.Span));
            }
        }
        catch
        {
            _ownership.DisposeAfterInitializationFailure();
            throw;
        }
    }

    /// <param name="renderer">The active renderer.</param>
    /// <param name="frame">Stage frame since the opening started.</param>
    public void Draw(OpenGLRenderer renderer, long frame)
    {
        ArgumentNullException.ThrowIfNull(renderer);

        if (_released)
        {
            return;
        }

        if (frame >= LoginOpeningArtwork.FrameCount)
        {
            _released = true;
            _ownership.Dispose();
            return;
        }

        renderer.DrawSprite(_frames[frame], 0, 0, _layout.Width, _layout.Height);
    }

    public void Dispose() => _ownership.Dispose();
}
