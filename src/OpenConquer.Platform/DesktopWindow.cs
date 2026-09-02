using OpenConquer.Platform.Internal;
using Silk.NET.Core.Contexts;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace OpenConquer.Platform;

public sealed class DesktopWindow : IDisposable
{
    private static readonly Vector2D<int> InitialHostSize = new(1280, 720);

    private readonly IWindow _window;
    private readonly DesktopFramePacer _framePacer;

    private SilkOpenGlContext? _openGlContext;
    private bool _runStarted;
    private bool _openGlContextReleaseStarted;
    private bool _disposed;

    public DesktopWindow(TimeSpan frameInterval)
    {
        WindowOptions options = WindowOptions.Default with
        {
            Title = "OpenConquer Client",
            Size = InitialHostSize,
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
    public event Action<IOpenGlContext>? OpenGlContextReady;
    public event Action? OpenGlContextReleasing;

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
                ReleaseOpenGlContext();
            }
            catch
            {
                // Preserve the original window-loop failure.
            }

            throw;
        }

        ReleaseOpenGlContext();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            ReleaseOpenGlContext();
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
                _openGlContext = null;
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
        if (_openGlContext is not null)
        {
            throw new InvalidOperationException("The OpenGL context has already been initialized.");
        }

        IGLContext context = _window.GLContext ?? throw new InvalidOperationException("The OpenGL context was not created.");

        SilkOpenGlContext openGlContext = new(context);

        if (!openGlContext.IsCurrent)
        {
            openGlContext.MakeCurrent();
        }

        _openGlContext = openGlContext;

        OpenGlContextReady?.Invoke(openGlContext);
    }

    private void OnFramebufferResize(Vector2D<int> size)
    {
        FramebufferResized?.Invoke(new PixelSize(width: size.X, height: size.Y));
    }

    private void OnRender(double deltaSeconds)
    {
        SilkOpenGlContext openGlContext = _openGlContext ?? throw new InvalidOperationException("Rendering cannot begin before the OpenGL context is initialized.");

        if (!openGlContext.IsCurrent)
        {
            openGlContext.MakeCurrent();
        }

        Rendering?.Invoke(deltaSeconds);
    }

    private void ReleaseOpenGlContext()
    {
        SilkOpenGlContext? openGlContext = _openGlContext;

        if (openGlContext is null || _openGlContextReleaseStarted)
        {
            return;
        }

        if (!openGlContext.IsCurrent)
        {
            openGlContext.MakeCurrent();
        }

        _openGlContextReleaseStarted = true;

        try
        {
            OpenGlContextReleasing?.Invoke();
        }
        finally
        {
            _openGlContext = null;
        }
    }
}
