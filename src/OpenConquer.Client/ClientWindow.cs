using Silk.NET.Windowing;

namespace OpenConquer.Client;

internal sealed class ClientWindow : IDisposable
{
    private readonly IWindow _window;

    public ClientWindow()
    {
        WindowOptions options = WindowOptions.Default;
        options.Title = "Open Conquer";

        _window = Window.Create(options);
    }

    public void Run()
    {
        _window.Run();
    }

    public void Dispose()
    {
        _window.Dispose();
    }
}
