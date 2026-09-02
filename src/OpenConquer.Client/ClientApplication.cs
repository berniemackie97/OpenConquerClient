using OpenConquer.Content;
using OpenConquer.Content.Configuration;
using OpenConquer.Platform;
using OpenConquer.Rendering;
using OpenConquer.Rendering.OpenGl;

namespace OpenConquer.Client;

internal sealed class ClientApplication : IDisposable
{
    private readonly DesktopWindow _window;
    private readonly LogicalRenderSize _logicalRenderSize;

    private OpenGlGraphicsDevice? _graphicsDevice;
    private OpenGlRenderer? _renderer;
    private bool _disposed;

    public ClientApplication(string clientContentRootPath)
    {
        ClientContentRoot contentRoot = new(clientContentRootPath);
        GameSetupConfiguration gameSetup = GameSetupConfiguration.Load(contentRoot);

        _logicalRenderSize = new LogicalRenderSize(gameSetup.LogicalWidthPixels, gameSetup.LogicalHeightPixels);

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

            _renderer = graphicsDevice.CreateRenderer(_logicalRenderSize, framebufferSize.Width, framebufferSize.Height);

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
