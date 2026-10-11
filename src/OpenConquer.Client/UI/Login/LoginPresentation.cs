using System.Runtime.ExceptionServices;
using OpenConquer.Content.Images;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.UI.Login;

/// <summary>Owns the pre-world login textures within the active OpenGL context.</summary>
internal sealed class LoginPresentation : IDisposable
{
    private readonly OpenGLTexture2D _backdrop;
    private readonly OpenGLTexture2D _topDragon;
    private readonly LoginVisualLayout _layout;
    private bool _disposed;

    public LoginPresentation(OpenGLGraphicsDevice graphicsDevice, LogicalRenderSize size, RgbaImage backdrop, RgbaImage topDragon)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(backdrop);
        ArgumentNullException.ThrowIfNull(topDragon);

        LoginVisualLayout layout = LoginVisualLayout.Create(size);

        if (backdrop.Width != 1024 || backdrop.Height != 768 || topDragon.Width != 494 || topDragon.Height != 140)
        {
            throw new InvalidDataException("Login visual asset dimensions do not match the validated reference composition.");
        }

        OpenGLTexture2D? background = null;
        OpenGLTexture2D? dragon = null;

        try
        {
            background = graphicsDevice.CreateTexture2D(backdrop.Width, backdrop.Height, backdrop.Pixels.Span);
            dragon = graphicsDevice.CreateTexture2D(topDragon.Width, topDragon.Height, topDragon.Pixels.Span);
        }
        catch
        {
            try
            {
                dragon?.Dispose();
            }
            catch { }

            try
            {
                background?.Dispose();
            }
            catch { }

            throw;
        }

        _backdrop = background;
        _topDragon = dragon;
        _layout = layout;
    }

    public void Draw(OpenGLRenderer renderer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);

        renderer.DrawSprite(_backdrop, 0, 0, _layout.Width, _layout.Height);
        LoginSpritePlacement dragon = _layout.TopDragon;
        renderer.DrawSprite(_topDragon, dragon.X, dragon.Y, dragon.Width, dragon.Height);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            _topDragon.Dispose();
        }
        catch (Exception exception)
        {
            firstFailure = ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            _backdrop.Dispose();
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        firstFailure?.Throw();
    }
}
