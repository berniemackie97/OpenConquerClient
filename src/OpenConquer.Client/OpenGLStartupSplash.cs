using OpenConquer.Content.Startup;
using OpenConquer.Platform;
using OpenConquer.Rendering.OpenGL;

namespace OpenConquer.Client;

internal sealed class OpenGLStartupSplash : IStartupSplash
{
    private static readonly TimeSpan s_maximumRefreshWait = TimeSpan.FromMilliseconds(16);
    private static readonly PixelSize s_verifiedRetailDialogSize = new(width: 250, height: 188);

    private readonly StartupLogo _logo;
    private readonly TimeSpan _minimumVisibleDuration;
    private readonly StartupWindow _window;

    private OpenGLGraphicsDevice? _graphicsDevice;
    private OpenGLStartupLogoRenderer? _renderer;
    private long? _shownTimestamp;
    private bool _disposed;

    public OpenGLStartupSplash(StartupLogo logo, TimeSpan minimumVisibleDuration)
    {
        ArgumentNullException.ThrowIfNull(logo);

        if (minimumVisibleDuration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumVisibleDuration), minimumVisibleDuration, "The minimum visible duration cannot be negative.");
        }

        _logo = logo;
        _minimumVisibleDuration = minimumVisibleDuration;
        _window = new StartupWindow(s_verifiedRetailDialogSize);
        _window.Rendering += OnRendering;
        _window.OpenGLContextReady += OnOpenGLContextReady;
        _window.OpenGLContextReleasing += OnOpenGLContextReleasing;
    }

    public void Show()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _shownTimestamp = TimeProvider.System.GetTimestamp();
        _window.ShowAndRender();
    }

    public void Complete()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        long shownTimestamp = _shownTimestamp ?? throw new InvalidOperationException("The startup splash must be shown before it can be completed.");

        while (true)
        {
            _window.Redraw();

            TimeSpan remaining = _minimumVisibleDuration - TimeProvider.System.GetElapsedTime(shownTimestamp);

            if (remaining <= TimeSpan.Zero)
            {
                return;
            }

            Thread.Sleep(remaining < s_maximumRefreshWait ? remaining : s_maximumRefreshWait);
        }
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
            _window.Rendering -= OnRendering;
            _window.OpenGLContextReady -= OnOpenGLContextReady;
            _window.OpenGLContextReleasing -= OnOpenGLContextReleasing;
            _disposed = true;
        }
    }

    private void OnOpenGLContextReady(IOpenGLContext context)
    {
        if (_graphicsDevice is not null || _renderer is not null)
        {
            throw new InvalidOperationException("Startup logo rendering has already been initialized.");
        }

        OpenGLGraphicsDevice graphicsDevice = new(context.GetProcAddress);
        OpenGLStartupLogoRenderer? renderer = null;

        try
        {
            renderer = graphicsDevice.CreateStartupLogoRenderer(
                _logo.Image.Width,
                _logo.Image.Height,
                _logo.Image.Pixels.Span
            );

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

    private void OnRendering(PixelSize framebufferSize)
    {
        OpenGLStartupLogoRenderer renderer = _renderer ?? throw new InvalidOperationException("The startup logo renderer is not initialized.");
        renderer.Render(framebufferSize.Width, framebufferSize.Height);
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
