using OpenConquer.Content;
using OpenConquer.Content.Configuration;
using OpenConquer.Platform;
using OpenConquer.Rendering;
using OpenConquer.Rendering.OpenGL;

namespace OpenConquer.Client;

internal sealed class ClientApplication : IDisposable
{
    private static readonly TimeSpan s_retailFrameInterval = TimeSpan.FromMilliseconds(25);

    private readonly DesktopWindow _window;
    private readonly LogicalRenderSize _logicalRenderSize;
    private readonly PresentationPolicy _presentationPolicy;

    private OpenGLGraphicsDevice? _graphicsDevice;
    private OpenGLRenderer? _renderer;
    private bool _disposed;

    public ClientApplication(string clientContentRootPath, PresentationPolicy presentationPolicy = PresentationPolicy.Fit)
    {
        _presentationPolicy = presentationPolicy;

        ClientContentRoot contentRoot = new(clientContentRootPath);
        GameSetupConfiguration gameSetup = GameSetupConfiguration.Load(contentRoot);

        _logicalRenderSize = new LogicalRenderSize(gameSetup.LogicalWidthPixels, gameSetup.LogicalHeightPixels);

        _window = new DesktopWindow(s_retailFrameInterval);

        _window.FramebufferResized += OnFramebufferResized;
        _window.Rendering += OnRendering;
        _window.OpenGLContextReady += OnOpenGLContextReady;
        _window.OpenGLContextReleasing += OnOpenGLContextReleasing;
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

    private void OnOpenGLContextReady(IOpenGLContext context)
    {
        if (_graphicsDevice is not null || _renderer is not null)
        {
            throw new InvalidOperationException("OpenGL rendering has already been initialized.");
        }

        OpenGLGraphicsDevice graphicsDevice = new(context.GetProcAddress);

        try
        {
            PixelSize framebufferSize = _window.FramebufferSize;

            _renderer = graphicsDevice.CreateRenderer(_logicalRenderSize, framebufferSize.Width, framebufferSize.Height, _presentationPolicy);

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

    private void OnOpenGLContextReleasing()
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
