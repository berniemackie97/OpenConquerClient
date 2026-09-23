using System.Numerics;
using System.Runtime.ExceptionServices;
using OpenConquer.Platform.Geometry;
using OpenConquer.Platform.OpenGL;
using Silk.NET.Core.Contexts;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace OpenConquer.Platform.Windowing.Desktop;

public sealed class DesktopWindow : IDisposable
{
    private static readonly Vector2D<int> s_defaultWindowSize = new(1280, 720);

    private readonly IWindow _window;
    private readonly DesktopFramePacer _framePacer;

    private IInputContext? _inputContext;
    private IMouse? _mouse;
    private SilkOpenGLContext? _openGLContext;
    private bool _runStarted;
    private bool _openGLContextReleaseStarted;
    private bool _disposed;

    /// <summary>
    /// The initial desktop size used when no launcher window-size preference is supplied.
    /// </summary>
    public static PixelSize DefaultWindowSize => new(s_defaultWindowSize.X, s_defaultWindowSize.Y);

    /// <summary>
    /// Creates the default resizable desktop host.
    /// </summary>
    public DesktopWindow(TimeSpan frameInterval) : this(DefaultWindowSize, DesktopWindowMode.Resizable, frameInterval)
    {
    }

    /// <summary>
    /// Creates a desktop host using the requested window size and selected window mode.
    /// </summary>
    public DesktopWindow(PixelSize windowSize, DesktopWindowMode windowMode, TimeSpan frameInterval)
    {
        WindowOptions options = CreateOptions(windowSize, windowMode);

        _framePacer = new DesktopFramePacer(frameInterval);
        _window = Window.Create(options);

        _window.Load += OnLoad;
        _window.FramebufferResize += OnFramebufferResize;
        _window.Render += OnRender;
    }

    public event Action<PixelSize>? FramebufferResized;
    public event Action<PixelPoint>? PointerMoved;
    public event Action<double>? Rendering;
    public event Action<IOpenGLContext>? OpenGLContextReady;
    public event Action? OpenGLContextReleasing;

    public PixelSize FramebufferSize
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            Vector2D<int> size = _window.FramebufferSize;
            return new PixelSize(size.X, size.Y);
        }
    }

    /// <summary>
    /// Converts a top-left window-client position to the corresponding host-framebuffer pixel.
    /// Positions outside the client area remain outside the framebuffer rather than being clamped.
    /// </summary>
    public PixelPoint PointToFramebuffer(float windowX, float windowY)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!float.IsFinite(windowX))
        {
            throw new ArgumentOutOfRangeException(nameof(windowX), windowX, "Pointer coordinate must be finite.");
        }

        if (!float.IsFinite(windowY))
        {
            throw new ArgumentOutOfRangeException(nameof(windowY), windowY, "Pointer coordinate must be finite.");
        }

        Vector2D<int> windowSize = _window.Size;
        Vector2D<int> framebufferSize = _window.FramebufferSize;

        if (windowSize.X <= 0 || windowSize.Y <= 0 || framebufferSize.X <= 0 || framebufferSize.Y <= 0)
        {
            throw new InvalidOperationException("Pointer coordinates cannot be mapped while the window or framebuffer has no drawable area.");
        }

        if (!TryMapWindowPointToFramebuffer(windowX, windowY, windowSize.X, windowSize.Y, framebufferSize.X, framebufferSize.Y, out PixelPoint framebufferPoint))
        {
            throw new ArgumentOutOfRangeException(null, "The mapped framebuffer position is outside the supported coordinate range.");
        }

        return framebufferPoint;
    }

    public void Run()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_runStarted)
        {
            throw new InvalidOperationException("The desktop window has already been run.");
        }

        _runStarted = true;

        try
        {
            _window.Initialize();
            InitializeInput();
            _framePacer.Start();
            _window.Run(RunFrame);
            _window.DoEvents();
        }
        catch
        {
            try
            {
                ReleaseRuntimeResources();
            }
            catch
            {
                // Preserve the original window-loop or initialization failure.
            }

            throw;
        }

        ReleaseRuntimeResources();
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
            ReleaseRuntimeResources();
        }
        catch (Exception exception)
        {
            firstFailure = ExceptionDispatchInfo.Capture(exception);
        }

        _window.Load -= OnLoad;
        _window.FramebufferResize -= OnFramebufferResize;
        _window.Render -= OnRender;

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
            _mouse = null;
            _inputContext = null;
            _openGLContext = null;
            _disposed = true;
        }

        firstFailure?.Throw();
    }

    internal static WindowOptions CreateOptions(PixelSize windowSize, DesktopWindowMode windowMode)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowSize.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowSize.Height);

        (WindowState windowState, WindowBorder windowBorder) = windowMode switch
        {
            DesktopWindowMode.Resizable => (WindowState.Normal, WindowBorder.Resizable),
            DesktopWindowMode.Fixed => (WindowState.Normal, WindowBorder.Fixed),
            DesktopWindowMode.Fullscreen => (WindowState.Fullscreen, WindowBorder.Hidden),
            _ => throw new ArgumentOutOfRangeException(nameof(windowMode), windowMode, "Unsupported desktop window mode."),
        };

        Vector2D<int> hostSize = new(windowSize.Width, windowSize.Height);

        return WindowOptions.Default with
        {
            Title = "OpenConquer Client",
            Size = hostSize,
            WindowState = windowState,
            WindowBorder = windowBorder,
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

    internal static bool TryMapWindowPointToFramebuffer(float windowX, float windowY, int windowWidth, int windowHeight, int framebufferWidth, int framebufferHeight, out PixelPoint framebufferPoint)
    {
        framebufferPoint = default;

        if (!float.IsFinite(windowX) || !float.IsFinite(windowY) ||
            windowWidth <= 0 || windowHeight <= 0 || framebufferWidth <= 0 || framebufferHeight <= 0)
        {
            return false;
        }

        double framebufferX = Math.Floor((double)windowX * framebufferWidth / windowWidth);
        double framebufferY = Math.Floor((double)windowY * framebufferHeight / windowHeight);

        if (framebufferX < int.MinValue || framebufferX > int.MaxValue ||
            framebufferY < int.MinValue || framebufferY > int.MaxValue)
        {
            return false;
        }

        framebufferPoint = new PixelPoint((int)framebufferX, (int)framebufferY);
        return true;
    }

    private void InitializeInput()
    {
        if (_inputContext is not null || _mouse is not null)
        {
            throw new InvalidOperationException("Desktop input has already been initialized.");
        }

        IInputContext inputContext = _window.CreateInput();

        try
        {
            if (inputContext.Mice.Count == 0)
            {
                throw new NotSupportedException("The desktop input backend did not provide a mouse device.");
            }

            IMouse mouse = inputContext.Mice[0];
            mouse.MouseMove += OnMouseMove;

            _mouse = mouse;
            _inputContext = inputContext;
        }
        catch
        {
            try
            {
                inputContext.Dispose();
            }
            catch
            {
                // Preserve the input-initialization failure.
            }

            throw;
        }
    }

    private void RunFrame()
    {
        _window.DoEvents();

        if (_window.IsClosing)
        {
            return;
        }

        _framePacer.WaitForNextFrame();

        if (_window.IsClosing)
        {
            return;
        }

        _window.DoUpdate();

        if (_window.IsClosing)
        {
            return;
        }

        _window.DoRender();
    }

    private void OnLoad()
    {
        if (_openGLContext is not null)
        {
            throw new InvalidOperationException("The OpenGL context has already been initialized.");
        }

        IGLContext context = _window.GLContext ?? throw new InvalidOperationException("The OpenGL context was not created.");
        SilkOpenGLContext openGLContext = new(context);

        if (!openGLContext.IsCurrent)
        {
            openGLContext.MakeCurrent();
        }

        _openGLContext = openGLContext;
        OpenGLContextReady?.Invoke(openGLContext);
    }

    private void OnMouseMove(IMouse _, Vector2 position)
    {
        Vector2D<int> windowSize = _window.Size;
        Vector2D<int> framebufferSize = _window.FramebufferSize;

        if (!TryMapWindowPointToFramebuffer(position.X, position.Y, windowSize.X, windowSize.Y, framebufferSize.X, framebufferSize.Y, out PixelPoint framebufferPoint))
        {
            return;
        }

        PointerMoved?.Invoke(framebufferPoint);
    }

    private void OnFramebufferResize(Vector2D<int> size)
    {
        FramebufferResized?.Invoke(new PixelSize(size.X, size.Y));
    }

    private void OnRender(double deltaSeconds)
    {
        SilkOpenGLContext openGLContext = _openGLContext ?? throw new InvalidOperationException("Rendering cannot begin before the OpenGL context is initialized.");

        if (!openGLContext.IsCurrent)
        {
            openGLContext.MakeCurrent();
        }

        Rendering?.Invoke(deltaSeconds);
    }

    private void ReleaseRuntimeResources()
    {
        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            ReleaseInput();
        }
        catch (Exception exception)
        {
            firstFailure = ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            ReleaseOpenGLContext();
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        firstFailure?.Throw();
    }

    private void ReleaseInput()
    {
        IMouse? mouse = _mouse;
        IInputContext? inputContext = _inputContext;

        _mouse = null;
        _inputContext = null;

        ExceptionDispatchInfo? firstFailure = null;

        if (mouse is not null)
        {
            try
            {
                mouse.MouseMove -= OnMouseMove;
            }
            catch (Exception exception)
            {
                firstFailure = ExceptionDispatchInfo.Capture(exception);
            }
        }

        if (inputContext is not null)
        {
            try
            {
                inputContext.Dispose();
            }
            catch (Exception exception)
            {
                firstFailure ??= ExceptionDispatchInfo.Capture(exception);
            }
        }

        firstFailure?.Throw();
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
