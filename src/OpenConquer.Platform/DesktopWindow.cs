using OpenConquer.Platform.Internal;
using Silk.NET.Core.Contexts;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace OpenConquer.Platform;

public sealed class DesktopWindow : IDisposable
{
    private static readonly Vector2D<int> s_initialHostSize = new(1280, 720);

    private readonly IWindow _window;
    private readonly DesktopFramePacer _framePacer;

    private SilkOpenGLContext? _openGLContext;
    private bool _runStarted;
    private bool _openGLContextReleaseStarted;
    private bool _disposed;

    public DesktopWindow(TimeSpan frameInterval)
    {
        WindowOptions options = WindowOptions.Default with
        {
            Title = "OpenConquer Client",
            Size = s_initialHostSize,
            WindowState = WindowState.Normal,
            WindowBorder = WindowBorder.Resizable,

            FramesPerSecond = 0,
            UpdatesPerSecond = 0,
            VSync = false,

            Samples = 0,
            ShouldSwapAutomatically = true,

            API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.ForwardCompatible, new APIVersion(3, minorVersion: 3)),

            PreferredDepthBufferBits = 0,
            PreferredStencilBufferBits = 0,
        };

        _framePacer = new DesktopFramePacer(frameInterval);
        _window = Window.Create(options);

        _window.Load += OnLoad;
        _window.FramebufferResize += OnFramebufferResize;
        _window.Render += OnRender;
    }

    public event Action<PixelSize>? FramebufferResized;
    public event Action<double>? Rendering;
    public event Action<IOpenGLContext>? OpenGLContextReady;
    public event Action? OpenGLContextReleasing;

    public PixelSize FramebufferSize
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, instance: this);

            Vector2D<int> size = _window.FramebufferSize;

            return new PixelSize(width: size.X, height: size.Y);
        }
    }

    public void Run()
    {
        ObjectDisposedException.ThrowIf(_disposed, instance: this);

        if (_runStarted)
        {
            throw new InvalidOperationException("The desktop window has already been run.");
        }

        _runStarted = true;

        try
        {
            _window.Initialize();

            _framePacer.Start();

            _window.Run(RunFrame);
            _window.DoEvents();
        }
        catch
        {
            try
            {
                ReleaseOpenGLContext();
            }
            catch
            {
                // Preserve the original window-loop failure.
            }

            throw;
        }

        ReleaseOpenGLContext();
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
            _window.FramebufferResize -= OnFramebufferResize;
            _window.Render -= OnRender;

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

    private void OnFramebufferResize(Vector2D<int> size)
    {
        FramebufferResized?.Invoke(new PixelSize(width: size.X, height: size.Y));
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
