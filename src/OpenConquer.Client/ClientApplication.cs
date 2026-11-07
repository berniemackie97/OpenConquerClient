using OpenConquer.Client.Startup;
using OpenConquer.Client.UI;
using OpenConquer.Content;
using OpenConquer.Content.Configuration;
using OpenConquer.Content.Images;
using OpenConquer.Content.Login;
using OpenConquer.Content.Startup;
using OpenConquer.Content.Wdf;
using OpenConquer.Networking.Game;
using OpenConquer.Platform.Geometry;
using OpenConquer.Platform.OpenGL;
using OpenConquer.Platform.Windowing.Desktop;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client;

/// <summary>Hosts startup, the desktop window, graphics context and frame loop for one client session.</summary>
internal sealed class ClientApplication : IDisposable
{
    private static readonly TimeSpan s_frameInterval = TimeSpan.FromMilliseconds(25);
    private readonly string _contentRootPath;
    private readonly TimeProvider _clock;
    private readonly PresentationPolicy _presentationPolicy;
    private readonly DesktopWindowMode _windowMode;
    private readonly PixelSize _windowSize;
    private DesktopWindow? _window;
    private LogicalRenderSize _logicalRenderSize;
    private DesktopCursorAnimation? _cursor;
    private ClientSession? _session;
    private ResourceOwnership? _graphics;
    private OpenGLGraphicsDevice? _graphicsDevice;
    private OpenGLRenderer? _renderer;
    private HostLifetime _lifetime;

    private enum HostLifetime
    {
        Created,
        Started,
        Disposed,
    }

    public ClientApplication(string contentRootPath, PresentationPolicy presentationPolicy = PresentationPolicy.Fit, DesktopWindowMode windowMode = DesktopWindowMode.Resizable, PixelSize? windowSize = null, TimeProvider? clock = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);

        if (!Enum.IsDefined(windowMode))
        {
            throw new ArgumentOutOfRangeException(nameof(windowMode), windowMode, "Unsupported desktop window mode.");
        }

        _contentRootPath = contentRootPath;
        _clock = clock ?? TimeProvider.System;
        _presentationPolicy = presentationPolicy;
        _windowMode = windowMode;

        PixelSize configuredWindowSize = windowSize ?? DesktopWindow.DefaultWindowSize;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(configuredWindowSize.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(configuredWindowSize.Height);
        _windowSize = configuredWindowSize;
    }

    public int Run()
    {
        ObjectDisposedException.ThrowIf(_lifetime == HostLifetime.Disposed, this);

        if (_lifetime != HostLifetime.Created)
        {
            throw new InvalidOperationException("The client application has already been run.");
        }

        _lifetime = HostLifetime.Started;

        PackagedClientContentSource contentSource = PackagedClientContentSource.Open(_contentRootPath);
        ReportPackageRegistrationWarnings(contentSource);

        StartupLogo startupLogo = StartupLogo.Load(contentSource, GameClientClock.TimestampMilliseconds(_clock, _clock.GetTimestamp()));
        if (startupLogo.UnavailableReason is { } unavailableReason)
        {
            Console.Error.WriteLine($"OpenConquer: startup logo unavailable; {unavailableReason}");
        }

        DesktopWindow window = StartupWindowSequence.CreateMainAfterStartup(new OpenGLStartupSplash(startupLogo),
            () => InitializeSession(contentSource),
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

        (_session ?? throw new InvalidOperationException("The client session is not initialized.")).StartServices();

        window.Run();
        return 0;
    }

    public void Dispose()
    {
        if (_lifetime == HostLifetime.Disposed)
        {
            return;
        }
        _lifetime = HostLifetime.Disposed;
        ClientShutdown cleanup = new();
        if (_session is { } session)
        {
            cleanup.Run(session.Dispose);
        }
        else
        {
            cleanup.Run(() => _window?.Dispose());
        }
        _session = null;
        _window = null;
        _cursor = null;
        cleanup.ThrowIfFailed();
    }

    private void InitializeSession(IClientContentSource contentSource)
    {
        GameSetupConfiguration gameSetup = GameSetupConfiguration.Load(contentSource);
        _logicalRenderSize = new LogicalRenderSize(gameSetup.LogicalWidthPixels, gameSetup.LogicalHeightPixels);
        _session = new ClientSession(_contentRootPath, contentSource, _logicalRenderSize, () => _window?.RequestClose(), () => _window?.Dispose(), _clock);
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
        ClientSession session = _session ?? throw new InvalidOperationException("The client session has not been initialized.");
        ResourceOwnership graphics = new();
        try
        {
            OpenGLGraphicsDevice device = graphics.Track(new OpenGLGraphicsDevice(context.GetProcAddress));
            PixelSize framebuffer = window.FramebufferSize;
            // The login and create-role screens are 8-bit-per-channel art; a 16-bit target would band it (retail drew them
            // as 32-bit Flash windows, only the Direct3D world used RGB565).
            _renderer = graphics.Track(device.CreateRenderer(_logicalRenderSize, framebuffer.Width, framebuffer.Height, _presentationPolicy,
                RenderColorFormat.Rgba8));
            session.CreateGraphics(device, graphics);
            _graphicsDevice = device;
            _graphics = graphics;
        }
        catch
        {
            // Preserve construction failure after releasing partial context resources.
            _renderer = null;
            _graphicsDevice = null;
            ClientShutdown cleanup = new();
            cleanup.Run(session.ClearGraphicsBindings);
            cleanup.Run(graphics.DisposeAfterInitializationFailure);
            throw;
        }
    }

    private void OnOpenGLContextReleasing()
    {
        ResourceOwnership? graphics = _graphics;
        _graphics = null;
        _renderer = null;
        ClientShutdown cleanup = new();
        cleanup.Run(() => _session?.ReleaseGraphics());
        _graphicsDevice = null;
        cleanup.Run(() => graphics?.Dispose());
        cleanup.ThrowIfFailed();
    }

    private void OnRendering(double _)
    {
        (_session ?? throw new InvalidOperationException("The client session is not initialized.")).RenderFrame(
            _window ?? throw new InvalidOperationException("The desktop window is unavailable."),
            _graphicsDevice ?? throw new InvalidOperationException("The graphics device is unavailable."),
            _renderer ?? throw new InvalidOperationException("The renderer is not initialized."));
    }

    private void OnFramebufferResized(PixelSize size)
    {
        _renderer?.ResizeHostFramebuffer(size.Width, size.Height);
        _session?.ResizeFramebuffer(size);
    }

    private void OnPointerMoved(PixelPoint point)
    {
        if (_renderer is { } renderer)
        {
            _session?.PointerMoved(point, renderer);
        }
    }

    private void OnPointerPressed(PixelPoint point)
    {
        if (_renderer is { } renderer)
        {
            bool extend = ((_window?.KeyboardModifiers ?? DesktopKeyModifiers.None) & DesktopKeyModifiers.Shift) != 0;
            _session?.PointerPressed(point, renderer, extend);
        }
    }

    private void OnPointerReleased(PixelPoint point)
    {
        if (_renderer is { } renderer)
        {
            _session?.PointerReleased(point, renderer);
        }
    }

    private void OnKeyPressed(DesktopKeyEvent key)
    {
        if (_window is { } window)
        {
            _session?.KeyPressed(key, window.ReadClipboardText, window.WriteClipboardText);
        }
    }

    private void OnTextEntered(char character) => _session?.TextEntered(character);
    private void OnFocusChanged(bool focused) => _session?.SetFocused(focused);

    private static void ReportPackageRegistrationWarnings(PackagedClientContentSource contentSource)
    {
        foreach (WdfPackageRegistration registration in contentSource.PackageRegistrations)
        {
            if (registration.Outcome is WdfPackageRegistrationOutcome.DuplicatePrefix or WdfPackageRegistrationOutcome.ArchiveUnavailable)
            {
                Console.Error.WriteLine($"OpenConquer: declared package '{registration.DeclaredName}' (prefix '{registration.Prefix}') resolved as {registration.Outcome}.");
            }
        }
    }
}
