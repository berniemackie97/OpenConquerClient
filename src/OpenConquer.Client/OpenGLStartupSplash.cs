using OpenConquer.Content.Startup;
using OpenConquer.Platform;
using OpenConquer.Rendering.OpenGL;

namespace OpenConquer.Client;

/// <summary>
/// Presents the retail startup logo on a short-lived OpenGL window.
/// </summary>
/// <remarks>
/// The window is sized to the logo bitmap so the image occupies its natural logical size, matching
/// the native pattern-brush presentation. When no bitmap could be loaded the window still appears,
/// sized from the dialog template, because retail also shows an empty dialog in that case.
/// </remarks>
internal sealed class OpenGLStartupSplash : IStartupSplash
{
    private readonly StartupLogo _logo;
    private readonly StartupWindow _window;

    private OpenGLGraphicsDevice? _graphicsDevice;
    private OpenGLStartupSurfaceRenderer? _renderer;
    private bool _disposed;

    public OpenGLStartupSplash(StartupLogo logo)
    {
        ArgumentNullException.ThrowIfNull(logo);

        _logo = logo;
        _window = new StartupWindow(ResolveSurfaceSize(logo));
        _window.Rendering += OnRendering;
        _window.OpenGLContextReady += OnOpenGLContextReady;
        _window.OpenGLContextReleasing += OnOpenGLContextReleasing;
    }

    public void Show()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _window.ShowAndRender();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // Unsubscribe first so a callback raised during teardown cannot reach a released renderer.
        _window.Rendering -= OnRendering;
        _window.OpenGLContextReady -= OnOpenGLContextReady;

        try
        {
            _window.Dispose();
        }
        finally
        {
            _window.OpenGLContextReleasing -= OnOpenGLContextReleasing;
            ReleaseRenderingResources();
            _disposed = true;
        }
    }

    /// <summary>
    /// Chooses the startup window's logical size.
    /// </summary>
    /// <remarks>
    /// The bitmap's own dimensions are authoritative whenever it loaded, because retail paints it
    /// unscaled into a dialog whose client area the retail artwork was authored against. Only the
    /// no-bitmap case falls back to the dialog template's derived size, which is documented on
    /// <see cref="StartupLogoDialogTemplate"/> as inferred from font metrics rather than proven.
    /// </remarks>
    private static PixelSize ResolveSurfaceSize(StartupLogo logo)
    {
        if (logo.Image is not null)
        {
            return new PixelSize(logo.Image.Width, logo.Image.Height);
        }

        (int width, int height) = StartupLogoDialogTemplate.DeriveReferenceClientSize();

        return new PixelSize(width, height);
    }

    private void OnOpenGLContextReady(IOpenGLContext context)
    {
        if (_graphicsDevice is not null || _renderer is not null)
        {
            throw new InvalidOperationException("Startup logo rendering has already been initialized.");
        }

        OpenGLGraphicsDevice graphicsDevice = new(context.GetProcAddress);
        OpenGLStartupSurfaceRenderer? renderer = null;

        try
        {
            renderer = _logo.Image is { } image
                ? graphicsDevice.CreateStartupSurfaceRenderer(image.Width, image.Height, image.Pixels.Span)
                : graphicsDevice.CreateStartupSurfaceRenderer();

            _renderer = renderer;
            _graphicsDevice = graphicsDevice;
            renderer = null;
        }
        catch
        {
            try
            {
                renderer?.Dispose();
            }
            finally
            {
                graphicsDevice.Dispose();
            }

            throw;
        }
    }

    private void OnRendering(StartupSurfaceMetrics metrics)
    {
        _renderer?.Render(
            metrics.FramebufferSize.Width,
            metrics.FramebufferSize.Height,
            metrics.LogicalSize.Width,
            metrics.LogicalSize.Height
        );
    }

    private void OnOpenGLContextReleasing()
    {
        ReleaseRenderingResources();
    }

    private void ReleaseRenderingResources()
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
