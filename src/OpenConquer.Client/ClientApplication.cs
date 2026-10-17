using System.Runtime.ExceptionServices;
using OpenConquer.Client.Startup;
using OpenConquer.Client.UI;
using OpenConquer.Client.UI.Login;
using OpenConquer.Client.UI.Login.Account;
using OpenConquer.Client.UI.Login.Audio;
using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Content;
using OpenConquer.Content.Configuration;
using OpenConquer.Content.Images;
using OpenConquer.Content.Login;
using OpenConquer.Content.Startup;
using OpenConquer.Content.Wdf;
using OpenConquer.Platform.Geometry;
using OpenConquer.Platform.OpenGL;
using OpenConquer.Platform.Windowing.Desktop;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client;

/// <summary>
/// Hosts the client: shows the startup logo, opens the desktop window, connects the login screen to the
/// window's input and OpenGL context, and keeps the realm catalog up to date.
/// </summary>
internal sealed class ClientApplication : IDisposable
{
    private static readonly TimeSpan s_frameInterval = TimeSpan.FromMilliseconds(25);

    private readonly string _contentRootPath;
    private readonly PresentationPolicy _presentationPolicy;
    private readonly DesktopWindowMode _windowMode;
    private readonly PixelSize _windowSize;

    private DesktopWindow? _window;
    private LogicalRenderSize _logicalRenderSize;
    private DesktopCursorAnimation? _cursor;
    private LoginScreen? _login;
    private LoginCatalogBinding? _catalog;
    private LoginAudioPlayer? _audio;
    private ResourceOwnership? _graphics;
    private OpenGLRenderer? _renderer;
    private LoginScreenRenderer? _loginRenderer;
    private PixelPoint _lastPointer;
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
            () => InitializeLogin(contentSource),
            () => new DesktopWindow(_windowSize, _windowMode, s_frameInterval));

        _window = window;
        window.ConfigureCursor(_cursor ?? throw new InvalidOperationException("The retail cursor animation was not loaded."));
        window.FramebufferResized += OnFramebufferResized;
        window.PrimaryPointerPressed += OnPointerPressed;
        window.PrimaryPointerReleased += OnPointerReleased;
        window.PointerMoved += OnPointerMoved;
        window.KeyPressed += OnKeyPressed;
        window.TextEntered += OnTextEntered;
        window.FocusChanged += OnFocusChanged;
        window.Rendering += OnRendering;
        window.OpenGLContextReady += OnOpenGLContextReady;
        window.OpenGLContextReleasing += OnOpenGLContextReleasing;

        _catalog = LoginCatalogBinding.ForEmbeddedAuthority();
        _audio = LoginAudioPlayer.TryCreate();

        window.Run();
        return 0;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _catalog?.Cancel();

        try
        {
            _window?.Dispose();
        }
        finally
        {
            try
            {
                _catalog?.Dispose();
                _audio?.Dispose();
            }
            finally
            {
                _login?.Dispose();
                _catalog = null;
                _audio = null;
                _login = null;
                _window = null;
            }
        }
    }

    private void InitializeLogin(IClientContentSource contentSource)
    {
        GameSetupConfiguration gameSetup = GameSetupConfiguration.Load(contentSource);
        _logicalRenderSize = new LogicalRenderSize(gameSetup.LogicalWidthPixels, gameSetup.LogicalHeightPixels);

        _login = new LoginScreen(LoginStageLayout.Create(_logicalRenderSize), LoginAccountHistory.ForCurrentUser(),
            ClientVersionLabel.Load(contentSource), cue => _audio?.Play(cue));
        _login.ExitRequested += () => _window?.RequestClose();

        IReadOnlyList<RgbaImage> cursorFrames = LoginCursorArtwork.Load();
        _cursor = new DesktopCursorAnimation(cursorFrames.Select(static image => image.Pixels), cursorFrames[0].Width,
            cursorFrames[0].Height, LoginCursorArtwork.HotspotX, LoginCursorArtwork.HotspotY, LoginCursorArtwork.FrameDuration);
    }

    private void OnOpenGLContextReady(IOpenGLContext context)
    {
        if (_graphics is not null)
        {
            throw new InvalidOperationException("OpenGL rendering has already been initialized.");
        }

        DesktopWindow window = _window ?? throw new InvalidOperationException("The desktop window has not been created.");
        LoginScreen login = _login ?? throw new InvalidOperationException("The login screen has not been initialized.");
        ResourceOwnership graphics = new();

        try
        {
            OpenGLGraphicsDevice device = graphics.Track(new OpenGLGraphicsDevice(context.GetProcAddress));
            PixelSize framebuffer = window.FramebufferSize;
            _renderer = graphics.Track(device.CreateRenderer(_logicalRenderSize, framebuffer.Width, framebuffer.Height, _presentationPolicy));
            _loginRenderer = graphics.Track(new LoginScreenRenderer(device, login));
            login.CaretLocator = _loginRenderer.CaretLocator;
            _graphics = graphics;
        }
        catch
        {
            _renderer = null;
            _loginRenderer = null;
            graphics.DisposeAfterInitializationFailure();
            throw;
        }
    }

    private void OnOpenGLContextReleasing()
    {
        _catalog?.Cancel();
        _login?.FocusLost();
        if (_login is not null)
        {
            _login.CaretLocator = null;
        }

        ResourceOwnership? graphics = _graphics;
        _graphics = null;
        _renderer = null;
        _loginRenderer = null;
        graphics?.Dispose();
    }

    private void OnRendering(double elapsedSeconds)
    {
        LoginScreen login = _login ?? throw new InvalidOperationException("The login screen is not initialized.");
        OpenGLRenderer renderer = _renderer ?? throw new InvalidOperationException("The renderer is not initialized.");
        LoginScreenRenderer loginRenderer = _loginRenderer ?? throw new InvalidOperationException("The login renderer is not initialized.");

        _catalog?.Update(login, DateTimeOffset.UtcNow);
        login.Advance(double.IsFinite(elapsedSeconds) && elapsedSeconds > 0 ? TimeSpan.FromSeconds(elapsedSeconds) : TimeSpan.Zero);
        ContinueCredentialDrag();

        renderer.BeginFrame();
        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            loginRenderer.Draw(renderer, login, _catalog?.Availability(login.Selection) ?? default);
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

    private void OnFramebufferResized(PixelSize size) => _renderer?.ResizeHostFramebuffer(size.Width, size.Height);

    private void OnPointerMoved(PixelPoint point)
    {
        _lastPointer = point;
        if (TryMapPointer(point, out bool mapped, out int x, out int y))
        {
            _login?.PointerMoved(mapped, x, y);
            ContinueCredentialDrag();
        }
    }

    private void OnPointerPressed(PixelPoint point)
    {
        _lastPointer = point;
        if (TryMapPointer(point, out bool mapped, out int x, out int y))
        {
            bool extend = ((_window?.KeyboardModifiers ?? DesktopKeyModifiers.None) & DesktopKeyModifiers.Shift) != 0;
            _login?.PointerPressed(mapped, x, y, extend);
        }
    }

    private void OnPointerReleased(PixelPoint point)
    {
        if (TryMapPointer(point, out bool mapped, out int x, out int y))
        {
            _login?.PointerReleased(mapped, x, y);
        }
    }

    private void OnKeyPressed(DesktopKeyEvent key)
    {
        if (_window is { } window)
        {
            _login?.KeyPressed(key, window.ReadClipboardText, window.WriteClipboardText);
        }
    }

    private void OnTextEntered(char character) => _login?.TextEntered(character);

    private void OnFocusChanged(bool focused)
    {
        if (!focused)
        {
            _login?.FocusLost();
        }

        _audio?.SetFocused(focused);
    }

    /// <summary>A held credential selection keeps tracking the pointer, clamped to the stage, beyond the field.</summary>
    private void ContinueCredentialDrag()
    {
        if (_login is { Credentials.IsSelecting: true } login && _renderer is { Viewport.IsEmpty: false } renderer)
        {
            login.PointerDragged(renderer.Viewport.MapPointerXClamped(_lastPointer.X));
        }
    }

    /// <summary>Maps a framebuffer point to the logical stage; false before a renderer exists.</summary>
    private bool TryMapPointer(PixelPoint point, out bool mapped, out int x, out int y)
    {
        if (_renderer is not { } renderer)
        {
            mapped = false;
            x = y = 0;
            return false;
        }

        mapped = renderer.Viewport.TryMapPointerToLogical(point.X, point.Y, out x, out y);
        return true;
    }

    private static void ReportPackageRegistrationWarnings(PackagedClientContentSource contentSource)
    {
        foreach (WdfPackageRegistration registration in contentSource.PackageRegistrations)
        {
            if (registration.Outcome != WdfPackageRegistrationOutcome.Registered)
            {
                Console.Error.WriteLine($"OpenConquer: declared package '{registration.DeclaredName}' (prefix '{registration.Prefix}') resolved as {registration.Outcome}.");
            }
        }
    }
}
