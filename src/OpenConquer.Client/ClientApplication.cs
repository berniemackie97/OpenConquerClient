using OpenConquer.Platform;
using OpenConquer.Rendering.OpenGl;

namespace OpenConquer.Client;

internal sealed class ClientApplication : IDisposable
{
    private readonly DesktopWindow _window;
    private OpenGlGraphicsDevice? _graphicsDevice;
    private bool _disposed;

    public ClientApplication()
    {
        _window = new DesktopWindow();

        _window.OpenGlContextReady += OnOpenGlContextReady;
        _window.OpenGlContextReleasing += OnOpenGlContextReleasing;
    }

    public int Run()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _window.Run();

        return 0;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _window.Dispose();
        _disposed = true;
    }

    private void OnOpenGlContextReady(IOpenGlContext context)
    {
        if (_graphicsDevice is not null)
        {
            throw new InvalidOperationException(
                "The OpenGL graphics device has already been initialized.");
        }

        _graphicsDevice = new OpenGlGraphicsDevice(context.GetProcAddress);
    }

    private void OnOpenGlContextReleasing()
    {
        _graphicsDevice?.Dispose();
        _graphicsDevice = null;
    }
}
