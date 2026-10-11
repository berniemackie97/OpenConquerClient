using OpenConquer.Content.Images;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.UI.Login;

/// <summary>
/// Owns the pre-world login backdrop texture for the lifetime of the active OpenGL context.
/// </summary>
internal sealed class LoginPresentation : IDisposable
{
    private readonly OpenGLTexture2D _backdrop;
    private readonly LogicalRenderSize _size;
    private bool _disposed;

    public LoginPresentation(OpenGLGraphicsDevice graphicsDevice, LogicalRenderSize size, RgbaImage backdrop)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(backdrop);

        if (size.Width <= 0 || size.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "The login logical size must be positive.");
        }

        _backdrop = graphicsDevice.CreateTexture2D(backdrop.Width, backdrop.Height, backdrop.Pixels.Span);
        _size = size;
    }

    public void Draw(OpenGLRenderer renderer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);

        renderer.DrawSprite(_backdrop, 0, 0, _size.Width, _size.Height);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _backdrop.Dispose();
        }
        finally
        {
            _disposed = true;
        }
    }
}
