using OpenConquer.Platform.Internal;
using Silk.NET.Core.Contexts;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace OpenConquer.Platform;

/// <summary>
/// Owns the short-lived native window and OpenGL context used during client initialization.
/// </summary>
public sealed class StartupWindow : IDisposable
{
    private readonly IWindow _window;

    private SilkOpenGLContext? _openGLContext;
    private bool _showStarted;
    private bool _openGLContextReleaseStarted;
    private bool _disposed;

    public StartupWindow(PixelSize size)
    {
        if (size.Width == 0 || size.Height == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, "Startup window dimensions must be positive.");
        }

        WindowOptions options = WindowOptions.Default with
        {
            Title = "OpenConquer Client",
            Size = new Vector2D<int>(size.Width, size.Height),
            WindowState = WindowState.Normal,
            WindowBorder = WindowBorder.Fixed,
            IsVisible = false,

            FramesPerSecond = 0,
            UpdatesPerSecond = 0,
            VSync = false,

            Samples = 0,
            ShouldSwapAutomatically = true,

            API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.ForwardCompatible, new APIVersion(3, minorVersion: 3)),

            PreferredDepthBufferBits = 0,
            PreferredStencilBufferBits = 0,
        };

        _window = Window.Create(options);
        _window.Load += OnLoad;
        _window.Render += OnRender;
    }

    public event Action<PixelSize>? Rendering;
    public event Action<IOpenGLContext>? OpenGLContextReady;
    public event Action? OpenGLContextReleasing;

    /// <summary>
    /// Initializes, centers, shows, and presents the startup window once.
    /// </summary>
    public void ShowAndRender()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_showStarted)
        {
            throw new InvalidOperationException("The startup window has already been shown.");
        }

        _showStarted = true;

        try
        {
            _window.Initialize();
            _window.IsVisible = true;
            CenterOnAvailableMonitor();
            Redraw();
        }
        catch
        {
            try
            {
                ReleaseOpenGLContext();
            }
            catch
            {
                // Preserve the original startup-window failure.
            }

            throw;
        }
    }

    /// <summary>
    /// Keeps the startup surface responsive while later initialization stages run.
    /// </summary>
    public void Redraw()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_window.IsInitialized)
        {
            throw new InvalidOperationException("The startup window must be initialized before it can be redrawn.");
        }

        _window.DoEvents();
        _window.DoRender();
        _window.DoEvents();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            ReleaseOpenGLContext();
        }
        finally
        {
            _window.Load -= OnLoad;
            _window.Render -= OnRender;

            try
            {
                if (_window.IsInitialized)
                {
                    _window.IsVisible = false;
                    _window.DoEvents();
                }
            }
            finally
            {
                try
                {
                    _window.Dispose();
                }
                finally
                {
                    _openGLContext = null;
                    _disposed = true;
                }
            }
        }
    }

    private void OnLoad()
    {
        if (_openGLContext is not null)
        {
            throw new InvalidOperationException("The startup OpenGL context has already been initialized.");
        }

        IGLContext context = _window.GLContext ?? throw new InvalidOperationException("The startup OpenGL context was not created.");
        SilkOpenGLContext openGLContext = new(context);

        if (!openGLContext.IsCurrent)
        {
            openGLContext.MakeCurrent();
        }

        _openGLContext = openGLContext;
        OpenGLContextReady?.Invoke(openGLContext);
    }

    private void CenterOnAvailableMonitor()
    {
        IMonitor? monitor = _window.Monitor;

        if (monitor is not null)
        {
            WindowExtensions.Center(_window, monitor);
        }
    }

    private void OnRender(double _)
    {
        SilkOpenGLContext openGLContext = _openGLContext ?? throw new InvalidOperationException("Startup rendering cannot begin before the OpenGL context is initialized.");

        if (!openGLContext.IsCurrent)
        {
            openGLContext.MakeCurrent();
        }

        Vector2D<int> framebufferSize = _window.FramebufferSize;
        Rendering?.Invoke(new PixelSize(framebufferSize.X, framebufferSize.Y));
    }

    private void ReleaseOpenGLContext()
    {
        SilkOpenGLContext? openGLContext = _openGLContext;

        if (openGLContext is null || _openGLContextReleaseStarted)
        {
            return;
        }

        if (!openGLContext.IsCurrent)
        {
            openGLContext.MakeCurrent();
        }

        _openGLContextReleaseStarted = true;

        try
        {
            OpenGLContextReleasing?.Invoke();
        }
        finally
        {
            _openGLContext = null;
        }
    }
}
