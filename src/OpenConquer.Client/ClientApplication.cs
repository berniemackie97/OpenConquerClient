using OpenConquer.Content;
using OpenConquer.Content.Configuration;
using OpenConquer.Content.Startup;
using OpenConquer.Platform;
using OpenConquer.Rendering;
using OpenConquer.Rendering.OpenGL;

namespace OpenConquer.Client;

internal sealed class ClientApplication : IDisposable
{
    private static readonly TimeSpan s_retailFrameInterval = TimeSpan.FromMilliseconds(25);
    private static readonly TimeSpan s_minimumStartupLogoDuration = TimeSpan.FromMilliseconds(500);

    private readonly string _clientContentRootPath;
    private readonly PresentationPolicy _presentationPolicy;

    private OpenGLGraphicsDevice? _graphicsDevice;
    private OpenGLRenderer? _renderer;
    private DesktopWindow? _window;
    private LogicalRenderSize? _logicalRenderSize;
    private bool _runStarted;
    private bool _disposed;

    public ClientApplication(string clientContentRootPath, PresentationPolicy presentationPolicy = PresentationPolicy.Fit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientContentRootPath);

        _clientContentRootPath = clientContentRootPath;
        _presentationPolicy = presentationPolicy;
    }

    public int Run()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_runStarted)
        {
            throw new InvalidOperationException("The client application has already been run.");
        }

        _runStarted = true;

        RetailClientContentSource contentSource = RetailClientContentSource.Open(_clientContentRootPath);
        StartupLogo startupLogo = StartupLogo.Load(contentSource, Environment.TickCount64);

        DesktopWindow window = ClientWindowCreationSequence.CreateMainAfterStartup(
            new OpenGLStartupSplash(startupLogo, s_minimumStartupLogoDuration),
            () => InitializeRuntimeConfiguration(contentSource),
            () => new DesktopWindow(s_retailFrameInterval)
        );

        _window = window;
        window.FramebufferResized += OnFramebufferResized;
        window.Rendering += OnRendering;
        window.OpenGLContextReady += OnOpenGLContextReady;
        window.OpenGLContextReleasing += OnOpenGLContextReleasing;
        window.Run();

        return 0;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _window?.Dispose();
        }
        finally
        {
            _disposed = true;
        }
    }

    private void OnOpenGLContextReady(IOpenGLContext context)
    {
        if (_graphicsDevice is not null || _renderer is not null)
        {
            throw new InvalidOperationException("OpenGL rendering has already been initialized.");
        }

        OpenGLGraphicsDevice graphicsDevice = new(context.GetProcAddress);
        OpenGLRenderer? renderer = null;

        try
        {
            DesktopWindow window = _window ?? throw new InvalidOperationException("The desktop window has not been created.");
            LogicalRenderSize logicalRenderSize = _logicalRenderSize ?? throw new InvalidOperationException("The logical render size has not been initialized.");
            PixelSize framebufferSize = window.FramebufferSize;

            renderer = graphicsDevice.CreateRenderer(
                logicalRenderSize,
                framebufferSize.Width,
                framebufferSize.Height,
                _presentationPolicy
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

    private void OnFramebufferResized(PixelSize size)
    {
        _renderer?.ResizeHostFramebuffer(size.Width, size.Height);
    }

    private void InitializeRuntimeConfiguration(IClientContentSource contentSource)
    {
        ArgumentNullException.ThrowIfNull(contentSource);

        GameSetupConfiguration gameSetup = GameSetupConfiguration.Load(contentSource);
        _logicalRenderSize = new LogicalRenderSize(gameSetup.LogicalWidthPixels, gameSetup.LogicalHeightPixels);
    }

    private void OnRendering(double _)
    {
        _renderer?.RenderFrame();
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
