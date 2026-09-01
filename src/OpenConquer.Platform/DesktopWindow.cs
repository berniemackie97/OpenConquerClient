using OpenConquer.Platform.Internal;
using Silk.NET.Core.Contexts;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace OpenConquer.Platform;

public sealed class DesktopWindow : IDisposable
{
    private static readonly Vector2D<int> InitialSize = new(1280, 720);

    private readonly IWindow _window;
    private SilkOpenGlContext? _openGlContext;
    private bool _openGlContextReleased;
    private bool _disposed;

    public DesktopWindow()
    {
        WindowOptions options = WindowOptions.Default with
        {
            Title = "OpenConquer",
            Size = InitialSize,
            WindowState = WindowState.Normal,
            WindowBorder = WindowBorder.Resizable,
            API = new GraphicsAPI(
                ContextAPI.OpenGL,
                ContextProfile.Core,
                ContextFlags.ForwardCompatible,
                new APIVersion(3, 3)),
            PreferredDepthBufferBits = 24,
            PreferredStencilBufferBits = 8
        };

        _window = Window.Create(options);

        _window.Load += OnLoad;
        _window.Closing += OnClosing;
    }

    public event Action<IOpenGlContext>? OpenGlContextReady;

    public event Action? OpenGlContextReleasing;

    public void Run()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _window.Run();
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
            _window.Closing -= OnClosing;

            _window.Dispose();

            _openGlContext = null;
            _disposed = true;
        }
    }

    private void OnLoad()
    {
        if (_openGlContext is not null)
        {
            throw new InvalidOperationException("The OpenGL context has already been initialized.");
        }

        IGLContext context = _window.GLContext
            ?? throw new InvalidOperationException("The OpenGL context was not created.");

        SilkOpenGlContext openGlContext = new(context);

        if (!openGlContext.IsCurrent)
        {
            openGlContext.MakeCurrent();
        }

        _openGlContext = openGlContext;

        OpenGlContextReady?.Invoke(openGlContext);
    }

    private void OnClosing()
    {
        ReleaseOpenGlContext();
    }

    private void ReleaseOpenGlContext()
    {
        if (_openGlContext is null || _openGlContextReleased)
        {
            return;
        }

        if (!_openGlContext.IsCurrent)
        {
            _openGlContext.MakeCurrent();
        }

        _openGlContextReleased = true;

        OpenGlContextReleasing?.Invoke();
    }
}
