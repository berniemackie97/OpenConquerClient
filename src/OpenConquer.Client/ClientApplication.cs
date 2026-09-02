using OpenConquer.Platform;
using OpenConquer.Rendering;
using OpenConquer.Rendering.OpenGl;

namespace OpenConquer.Client;

internal sealed class ClientApplication : IDisposable
{
    private static readonly LogicalRenderSize InitialLogicalRenderSize = new(width: 1024, height: 768);

    private readonly DesktopWindow _window;

    private OpenGlGraphicsDevice? _graphicsDevice;
    private OpenGlRenderer? _renderer;
    private bool _disposed;

    public ClientApplication()
    {
        _window = new DesktopWindow();

        _window.FramebufferResized += OnFramebufferResized;
        _window.Rendering += OnRendering;
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

        try
        {
            _window.Dispose();
        }
        finally
        {
            _disposed = true;
        }
    }

    private void OnOpenGlContextReady(IOpenGlContext context)
    {
        if (_graphicsDevice is not null || _renderer is not null)
        {
            throw new InvalidOperationException("OpenGL rendering has already been initialized.");
        }

        OpenGlGraphicsDevice graphicsDevice = new(context.GetProcAddress);

        try
        {
            PixelSize framebufferSize = _window.FramebufferSize;

            _renderer = graphicsDevice.CreateRenderer(InitialLogicalRenderSize, framebufferSize.Width, framebufferSize.Height);
            _graphicsDevice = graphicsDevice;
        }
        catch
        {
            graphicsDevice.Dispose();
            throw;
        }
    }

    private void OnFramebufferResized(PixelSize size)
    {
        _renderer?.ResizeHostFramebuffer(size.Width, size.Height);
    }

    private void OnRendering(double _)
    {
        _renderer?.RenderFrame();
    }

    private void OnOpenGlContextReleasing()
    {
        try
        {
            _renderer?.Dispose();
        }
        finally
        {
            _renderer = null;

            try
            {
                _graphicsDevice?.Dispose();
            }
            finally
            {
                _graphicsDevice = null;
            }
        }
    }
}
