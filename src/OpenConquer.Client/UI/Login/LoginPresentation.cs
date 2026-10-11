using System.Runtime.ExceptionServices;
using OpenConquer.Content.Images;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.OpenGL.Resources;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.UI.Login;

/// <summary>Owns pre-world login textures within the active OpenGL context.</summary>
internal sealed class LoginPresentation : IDisposable
{
    private readonly OpenGLTexture2D _backdrop;
    private readonly OpenGLTexture2D _topDragon;
    private readonly OpenGLTexture2D _serverSlot;
    private readonly LoginVisualLayout _layout;
    private bool _disposed;

    public LoginPresentation(OpenGLGraphicsDevice graphicsDevice, LogicalRenderSize size, RgbaImage backdrop, RgbaImage topDragon, RgbaImage serverSlot)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentNullException.ThrowIfNull(backdrop);
        ArgumentNullException.ThrowIfNull(topDragon);
        ArgumentNullException.ThrowIfNull(serverSlot);

        LoginVisualLayout layout = LoginVisualLayout.Create(size);
        if (backdrop.Width != 1024 || backdrop.Height != 768 || topDragon.Width != 494 || topDragon.Height != 140 ||
            serverSlot.Width != 158 || serverSlot.Height != 58)
        {
            throw new InvalidDataException("Login visual asset dimensions do not match the validated reference composition.");
        }

        OpenGLTexture2D? background = null;
        OpenGLTexture2D? dragon = null;
        OpenGLTexture2D? slot = null;

        try
        {
            background = graphicsDevice.CreateTexture2D(backdrop.Width, backdrop.Height, backdrop.Pixels.Span);
            dragon = graphicsDevice.CreateTexture2D(topDragon.Width, topDragon.Height, topDragon.Pixels.Span);
            slot = graphicsDevice.CreateTexture2D(serverSlot.Width, serverSlot.Height, serverSlot.Pixels.Span);
        }
        catch
        {
            try
            {
                slot?.Dispose();
            }
            catch { }
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
        _serverSlot = slot;
        _layout = layout;
    }

    public void Draw(OpenGLRenderer renderer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(renderer);

        renderer.DrawSprite(_backdrop, 0, 0, _layout.Width, _layout.Height);
        DrawLayer(renderer, _topDragon, _layout.TopDragon);
        DrawLayer(renderer, _serverSlot, _layout.RegionSlot);
        DrawLayer(renderer, _serverSlot, _layout.ServerSlot);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            _serverSlot.Dispose();
        }
        catch (Exception exception) { firstFailure = ExceptionDispatchInfo.Capture(exception); }
        try
        {
            _topDragon.Dispose();
        }
        catch (Exception exception) { firstFailure ??= ExceptionDispatchInfo.Capture(exception); }
        try
        {
            _backdrop.Dispose();
        }
        catch (Exception exception) { firstFailure ??= ExceptionDispatchInfo.Capture(exception); }

        firstFailure?.Throw();
    }

    private static void DrawLayer(OpenGLRenderer renderer, OpenGLTexture2D texture, LoginSpritePlacement placement) =>
        renderer.DrawSprite(texture, placement.X, placement.Y, placement.Width, placement.Height);
}
