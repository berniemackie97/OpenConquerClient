using Silk.NET.Windowing;

namespace OpenConquer.Platform;

public sealed class DesktopWindow : IDisposable
{
    private readonly IWindow _window;
    private bool _disposed;

    public DesktopWindow()
    {
        WindowOptions options = WindowOptions.Default with
        {
            Title = "OpenConquer",
            API = new GraphicsAPI(
                ContextAPI.OpenGL,
                ContextProfile.Core,
                ContextFlags.ForwardCompatible,
                new APIVersion(3, 3)),
            PreferredDepthBufferBits = 24,
            PreferredStencilBufferBits = 8
        };

        _window = Window.Create(options);
    }

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

        _window.Dispose();
        _disposed = true;
    }
}
