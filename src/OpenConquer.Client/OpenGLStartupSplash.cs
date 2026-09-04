using System.Runtime.ExceptionServices;
using OpenConquer.Content.Images;
using OpenConquer.Content.Startup;
using OpenConquer.Platform;
using OpenConquer.Rendering.OpenGL;

namespace OpenConquer.Client;

/// <summary>
/// Presents the retail startup logo on a short-lived OpenGL window when the selected bitmap is available.
/// </summary>
internal sealed class OpenGLStartupSplash : IStartupSplash
{
    private readonly RgbaImage? _image;
    private readonly StartupWindow? _window;

    private OpenGLGraphicsDevice? _graphicsDevice;
    private OpenGLStartupSurfaceRenderer? _renderer;
    private bool _disposed;

    public OpenGLStartupSplash(StartupLogo logo)
    {
        ArgumentNullException.ThrowIfNull(logo);

        if (logo.Image is not { } image)
        {
            return;
        }

        _image = image;

        StartupWindow window = new(new PixelSize(image.Width, image.Height));

        _window = window;

        window.Rendering += OnRendering;
        window.OpenGLContextReady += OnOpenGLContextReady;
        window.OpenGLContextReleasing += OnOpenGLContextReleasing;
    }

    public void Show()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _window?.ShowAndRender();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        StartupWindow? window = _window;

        if (window is null)
        {
            _disposed = true;
            return;
        }

        window.Rendering -= OnRendering;
        window.OpenGLContextReady -= OnOpenGLContextReady;

        try
        {
            window.Dispose();
        }
        finally
        {
            window.OpenGLContextReleasing -= OnOpenGLContextReleasing;

            _renderer = null;
            _graphicsDevice = null;
            _disposed = true;
        }
    }

    private void OnOpenGLContextReady(IOpenGLContext context)
    {
        if (_graphicsDevice is not null || _renderer is not null)
        {
            throw new InvalidOperationException("Startup logo rendering has already been initialized.");
        }

        RgbaImage image = _image ?? throw new InvalidOperationException("Startup rendering cannot initialize without a logo image.");

        OpenGLGraphicsDevice graphicsDevice = new(context.GetProcAddress);

        try
        {
            OpenGLStartupSurfaceRenderer renderer = graphicsDevice.CreateStartupSurfaceRenderer(image.Width, image.Height, image.Pixels.Span);

            _renderer = renderer;
            _graphicsDevice = graphicsDevice;
        }
        catch
        {
            try
            {
                graphicsDevice.Dispose();
            }
            catch
            {
                // Preserve the startup-renderer creation failure.
            }

            throw;
        }
    }

    private void OnRendering(StartupSurfaceMetrics metrics)
    {
        OpenGLStartupSurfaceRenderer renderer = _renderer ?? throw new InvalidOperationException("Startup rendering cannot begin before the renderer is initialized.");

        renderer.Render(metrics.FramebufferSize.Width, metrics.FramebufferSize.Height, metrics.LogicalSize.Width, metrics.LogicalSize.Height);
    }

    private void OnOpenGLContextReleasing()
    {
        ReleaseRenderingResources();
    }

    private void ReleaseRenderingResources()
    {
        OpenGLStartupSurfaceRenderer? renderer = _renderer;
        OpenGLGraphicsDevice? graphicsDevice = _graphicsDevice;

        _renderer = null;
        _graphicsDevice = null;

        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            renderer?.Dispose();
        }
        catch (Exception exception)
        {
            firstFailure = ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            graphicsDevice?.Dispose();
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        firstFailure?.Throw();
    }
}
