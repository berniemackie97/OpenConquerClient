using System.Runtime.ExceptionServices;
using OpenConquer.Client.Startup;
using OpenConquer.Client.UI.Login;
using OpenConquer.Content;
using OpenConquer.Content.Configuration;
using OpenConquer.Content.Startup;
using OpenConquer.Content.Wdf;
using OpenConquer.Platform.Geometry;
using OpenConquer.Platform.OpenGL;
using OpenConquer.Platform.Windowing.Desktop;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client;

internal sealed class ClientApplication : IDisposable
{
    private static readonly TimeSpan s_frameInterval = TimeSpan.FromMilliseconds(25);

    private readonly string _contentRootPath;
    private readonly PresentationPolicy _presentationPolicy;
    private readonly DesktopWindowMode _windowMode;
    private readonly PixelSize _windowSize;

    private DesktopWindow? _window;
    private OpenGLGraphicsDevice? _graphicsDevice;
    private OpenGLRenderer? _renderer;
    private LoginPresentation? _loginPresentation;
    private LogicalRenderSize? _logicalRenderSize;
    private LoginArtworkSet? _loginArtwork;
    private bool _runStarted;
    private bool _disposed;

    public ClientApplication(string contentRootPath, PresentationPolicy presentationPolicy = PresentationPolicy.Fit, DesktopWindowMode windowMode = DesktopWindowMode.Resizable, PixelSize? windowSize = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);

        if (!Enum.IsDefined(windowMode))
        {
            throw new ArgumentOutOfRangeException(nameof(windowMode), windowMode, "Unsupported desktop window mode.");
        }

        _contentRootPath = contentRootPath;
        _presentationPolicy = presentationPolicy;
        _windowMode = windowMode;

        PixelSize configuredWindowSize = windowSize ?? DesktopWindow.DefaultWindowSize;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(configuredWindowSize.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(configuredWindowSize.Height);
        _windowSize = configuredWindowSize;
    }

    public int Run()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_runStarted)
        {
            throw new InvalidOperationException("The client application has already been run.");
        }

        _runStarted = true;

        PackagedClientContentSource contentSource = PackagedClientContentSource.Open(_contentRootPath);
        ReportPackageRegistrationWarnings(contentSource);

        StartupLogo startupLogo = StartupLogo.Load(contentSource, Environment.TickCount64);

        if (startupLogo.UnavailableReason is { } unavailableReason)
        {
            Console.Error.WriteLine($"OpenConquer: startup logo unavailable; {unavailableReason}");
        }

        DesktopWindow window = StartupWindowSequence.CreateMainAfterStartup(new OpenGLStartupSplash(startupLogo),
            () => InitializePreWorldContent(contentSource),
            () => new DesktopWindow(_windowSize, _windowMode, s_frameInterval));

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
            _window = null;
            _loginPresentation = null;
            _renderer = null;
            _graphicsDevice = null;
            _loginArtwork = null;
            _disposed = true;
        }
    }

    private void InitializePreWorldContent(IClientContentSource contentSource)
    {
        ArgumentNullException.ThrowIfNull(contentSource);

        GameSetupConfiguration gameSetup = GameSetupConfiguration.Load(contentSource);
        LoginArtworkSet artwork = LoginArtwork.Load();

        _logicalRenderSize = new LogicalRenderSize(gameSetup.LogicalWidthPixels, gameSetup.LogicalHeightPixels);
        _loginArtwork = artwork;
    }

    private void OnOpenGLContextReady(IOpenGLContext context)
    {
        if (_graphicsDevice is not null || _renderer is not null || _loginPresentation is not null)
        {
            throw new InvalidOperationException("OpenGL rendering has already been initialized.");
        }

        OpenGLGraphicsDevice graphicsDevice = new(context.GetProcAddress);
        OpenGLRenderer? renderer = null;
        LoginPresentation? login = null;

        try
        {
            DesktopWindow window = _window ?? throw new InvalidOperationException("The desktop window has not been created.");
            LogicalRenderSize logicalSize = _logicalRenderSize ?? throw new InvalidOperationException("The logical render size has not been initialized.");
            LoginArtworkSet artwork = _loginArtwork ?? throw new InvalidOperationException("The login artwork has not been initialized.");
            PixelSize framebufferSize = window.FramebufferSize;

            renderer = graphicsDevice.CreateRenderer(logicalSize, framebufferSize.Width, framebufferSize.Height, _presentationPolicy);
            login = new LoginPresentation(graphicsDevice, logicalSize, artwork);

            _graphicsDevice = graphicsDevice;
            _renderer = renderer;
            _loginPresentation = login;
        }
        catch
        {
            try
            {
                login?.Dispose();
            }
            catch { }

            try
            {
                renderer?.Dispose();
            }
            catch { }

            try
            {
                graphicsDevice.Dispose();
            }
            catch { }

            throw;
        }
    }

    private void OnFramebufferResized(PixelSize size) => _renderer?.ResizeHostFramebuffer(size.Width, size.Height);

    private void OnRendering(double elapsedSeconds)
    {
        OpenGLRenderer renderer = _renderer ?? throw new InvalidOperationException("The renderer is not initialized.");
        LoginPresentation login = _loginPresentation ?? throw new InvalidOperationException("The login presentation is not initialized.");

        renderer.BeginFrame();
        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            login.Draw(renderer);
        }
        catch (Exception exception)
        {
            firstFailure = ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            renderer.EndFrame();
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        firstFailure?.Throw();
    }

    private void OnOpenGLContextReleasing() => ReleaseRenderingResources();

    private void ReleaseRenderingResources()
    {
        LoginPresentation? login = _loginPresentation;
        OpenGLRenderer? renderer = _renderer;
        OpenGLGraphicsDevice? graphicsDevice = _graphicsDevice;

        _loginPresentation = null;
        _renderer = null;
        _graphicsDevice = null;

        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            login?.Dispose();
        }
        catch (Exception exception) { firstFailure = ExceptionDispatchInfo.Capture(exception); }

        try
        {
            renderer?.Dispose();
        }
        catch (Exception exception) { firstFailure ??= ExceptionDispatchInfo.Capture(exception); }

        try
        {
            graphicsDevice?.Dispose();
        }
        catch (Exception exception) { firstFailure ??= ExceptionDispatchInfo.Capture(exception); }

        firstFailure?.Throw();
    }

    private static void ReportPackageRegistrationWarnings(PackagedClientContentSource contentSource)
    {
        foreach (WdfPackageRegistration registration in contentSource.PackageRegistrations)
        {
            if (registration.Outcome == WdfPackageRegistrationOutcome.Registered)
            {
                continue;
            }

            Console.Error.WriteLine($"OpenConquer: declared package '{registration.DeclaredName}' (prefix '{registration.Prefix}') resolved as {registration.Outcome}.");
        }
    }
}
