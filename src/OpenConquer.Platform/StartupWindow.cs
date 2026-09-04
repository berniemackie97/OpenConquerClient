using System.Runtime.ExceptionServices;
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
        _window = Window.Create(CreateOptions(size));

        _window.Load += OnLoad;
        _window.Render += OnRender;
    }

    public event Action<StartupSurfaceMetrics>? Rendering;
    public event Action<IOpenGLContext>? OpenGLContextReady;
    public event Action? OpenGLContextReleasing;

    internal static WindowOptions CreateOptions(PixelSize size)
    {
        if (size.Width == 0 || size.Height == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, "Startup window dimensions must be positive.");
        }

        return WindowOptions.Default with
        {
            Title = "OpenConquer Client",
            Size = new Vector2D<int>(size.Width, size.Height),
            WindowState = WindowState.Normal,
            WindowBorder = WindowBorder.Hidden,
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
    }

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
            PresentOnce();
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

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            ReleaseOpenGLContext();
        }
        catch (Exception exception)
        {
            firstFailure = ExceptionDispatchInfo.Capture(exception);
        }

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
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            _window.Dispose();
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }
        finally
        {
            _openGLContext = null;
            _disposed = true;
        }

        firstFailure?.Throw();
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

    /// <summary>
    /// Drains pending window messages, draws one frame, and drains again so the surface is on screen before the caller continues.
    /// </summary>
    private void PresentOnce()
    {
        _window.DoEvents();
        _window.DoRender();
        _window.DoEvents();
    }

    private void OnRender(double _)
    {
        SilkOpenGLContext openGLContext = _openGLContext ?? throw new InvalidOperationException("Startup rendering cannot begin before the OpenGL context is initialized.");

        if (!openGLContext.IsCurrent)
        {
            openGLContext.MakeCurrent();
        }

        Vector2D<int> framebufferSize = _window.FramebufferSize;
        Vector2D<int> logicalSize = _window.Size;

        Rendering?.Invoke(new StartupSurfaceMetrics(new PixelSize(framebufferSize.X, framebufferSize.Y), new PixelSize(logicalSize.X, logicalSize.Y)));
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
