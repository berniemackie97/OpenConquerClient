using System.Runtime.ExceptionServices;
using OpenConquer.Client.Startup;
using OpenConquer.Client.Startup.LoginFlow;
using OpenConquer.Client.UI;
using OpenConquer.Client.UI.Login;
using OpenConquer.Client.UI.Login.Account;
using OpenConquer.Client.UI.Login.Audio;
using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Client.UI.Login.Status;
using OpenConquer.Client.UI.Role;
using OpenConquer.Client.World;
using OpenConquer.Content;
using OpenConquer.Content.Configuration;
using OpenConquer.Content.Images;
using OpenConquer.Content.Login;
using OpenConquer.Content.Startup;
using OpenConquer.Content.Wdf;
using OpenConquer.Networking.Game.Handshake;
using OpenConquer.Networking.Game.Login;
using OpenConquer.Platform.Geometry;
using OpenConquer.Platform.OpenGL;
using OpenConquer.Platform.Windowing.Desktop;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.Presentation;
using OpenConquer.Rendering.World;

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
    private LoginAttemptCoordinator? _loginFlow;
    private LoginRegistrationCoordinator? _registrationFlow;
    private LoginErrorMessages? _loginErrors;
    private LoginAudioPlayer? _audio;
    private ResourceOwnership? _graphics;
    private OpenGLGraphicsDevice? _graphicsDevice;
    private OpenGLRenderer? _renderer;
    private LoginScreenRenderer? _loginRenderer;
    private LoginLoadingScreen? _loading;
    private ClientRoleCreation? _roleCreation;
    private ClientWorld? _world;
    private WorldMapTextureRenderer? _worldTextures;
    private IClientContentSource? _content;
    private PixelPoint _lastPointer;
    private bool _runStarted;
    private bool _disposed;

    private bool IsWorldVisible => _world is { IsPresentable: true } &&
        (_loading is not { Visible: true } || _loading.CanEnterWorld);

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
            try
            {
                try
                {
                    _roleCreation?.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
                finally
                {
                    _loginFlow?.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
            }
            finally
            {
                try
                {
                    _world?.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
                finally
                {
                    _registrationFlow?.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
            }
        }
        finally
        {
            _loginFlow = null;
            _registrationFlow = null;
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
                    _roleCreation?.ReleaseContent();
                }
                finally
                {
                    _login?.Dispose();
                    _catalog = null;
                    _audio = null;
                    _login = null;
                    _world = null;
                    _roleCreation = null;
                    _window = null;
                }
            }
        }
    }

    private void InitializeLogin(IClientContentSource contentSource)
    {
        GameSetupConfiguration gameSetup = GameSetupConfiguration.Load(contentSource);
        _logicalRenderSize = new LogicalRenderSize(gameSetup.LogicalWidthPixels, gameSetup.LogicalHeightPixels);

        _content = contentSource;
        _roleCreation = new ClientRoleCreation(contentSource, cue => _audio?.Play(cue), () => _window?.RequestClose(),
            () => _world is null, () => _world is { HasScene: true }, AcceptRoleWorld,
            connectionLost => _loginFlow?.CompleteRoleReturn(connectionLost));
        _login = new LoginScreen(LoginStageLayout.Create(_logicalRenderSize), LoginAccountHistory.ForCurrentUser(),
            ClientVersionLabel.Load(contentSource), cue => _audio?.Play(cue));
        _login.ExitRequested += () => _window?.RequestClose();
        _loginFlow = new LoginAttemptCoordinator(_contentRootPath);
        _loginErrors = LoginErrorMessages.Load(contentSource, LoginErrorEncoding.ForCurrentHost());
        _loading = LoginLoadingScreen.LoadIfPresent(contentSource);
        _login.LoginRequested += OnLoginRequested;
        _login.PendingLoginCancelled += () => _loading?.Reset();
        _registrationFlow = new LoginRegistrationCoordinator();
        _login.RegistrationRequested += OnRegistrationRequested;
        _login.RegistrationSubmitRequested += OnRegistrationSubmitRequested;
        _login.RegistrationCancelRequested += OnRegistrationCancelRequested;

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
            // The login and create-role screens are 8-bit-per-channel art; a 16-bit target would band it (retail drew them
            // as 32-bit Flash windows, only the Direct3D world used RGB565).
            _renderer = graphics.Track(device.CreateRenderer(_logicalRenderSize, framebuffer.Width, framebuffer.Height, _presentationPolicy,
                RenderColorFormat.Rgba8));
            _loginRenderer = graphics.Track(new LoginScreenRenderer(device, login,
                _content ?? throw new InvalidOperationException("Login content source is unavailable."), _loading));
            (_roleCreation ?? throw new InvalidOperationException("Role creation was not initialized.")).CreateGraphics(device, graphics);
            _worldTextures = graphics.Track(new WorldMapTextureRenderer(
                _content ?? throw new InvalidOperationException("World content source is missing."), device));
            _roleCreation.BindCaret();

            login.CaretLocator = _loginRenderer.CaretLocator;
            login.RegistrationCaretLocator = _loginRenderer.RegistrationCaretLocator;
            _graphicsDevice = device;
            _graphics = graphics;
        }
        catch
        {
            _renderer = null;
            _graphicsDevice = null;
            _loginRenderer = null;
            _roleCreation?.ReleaseGraphicsBinding();
            _worldTextures = null;
            graphics.DisposeAfterInitializationFailure();
            throw;
        }
    }

    private void OnOpenGLContextReleasing()
    {
        _catalog?.Cancel();
        _login?.FocusLost();
        _roleCreation?.ReleaseGraphicsBinding();

        if (_login is not null)
        {
            _login.CaretLocator = null;
            _login.RegistrationCaretLocator = null;
        }

        ResourceOwnership? graphics = _graphics;
        _graphics = null;
        _renderer = null;
        _loginRenderer = null;
        _worldTextures = null;
        try
        {
            _world?.ReleaseGraphics();
        }
        finally
        {
            _graphicsDevice = null;
            graphics?.Dispose();
        }
    }

    private void OnRendering(double elapsedSeconds)
    {
        LoginScreen login = _login ?? throw new InvalidOperationException("The login screen is not initialized.");
        OpenGLRenderer renderer = _renderer ?? throw new InvalidOperationException("The renderer is not initialized.");
        LoginScreenRenderer loginRenderer = _loginRenderer ?? throw new InvalidOperationException("The login renderer is not initialized.");

        DateTimeOffset now = DateTimeOffset.UtcNow;
        _catalog?.Update(login, now);
        _loginFlow?.Update(_catalog?.CurrentCatalog(now));
        if (login.AwaitingLoginCommand && _loading?.Sequence.ReadyForAuthentication == true)
        {
            login.DispatchPendingLogin();
            if (_loginFlow is { } pending)
            {
                bool started = pending.TryStart(login, _catalog?.CurrentCatalog(now));
                login.ApplyFeedback(pending.Feedback, _loginErrors!);
                if (!started)
                {
                    _loading?.Reset();
                }
            }
        }
        _registrationFlow?.Update(_catalog?.CurrentCatalog(now));
        if (_world is null && _loginFlow is { } existing &&
            existing.TryTakeAdmittedWorld(out GameServerHandshakeSession? acceptedGame,
                out GameWorldEntrySnapshot? acceptedWorld))
        {
            AcceptWorld(acceptedGame!, acceptedWorld!);
        }

        _roleCreation?.ObserveClose();

        if (_world is { } closingWorld && closingWorld.TryObserveClose(out WorldCloseReason closeReason))
        {
            if (closeReason == WorldCloseReason.ConnectionLost)
            {
                _loginFlow?.FailWorldConnection();
            }
            else
            {
                _loginFlow?.FailWorldContent();
            }
        }

        if (_roleCreation is { CanOpen: true } roleCreation &&
            _loginFlow is { Status: LoginAttemptStatus.CharacterCreation } creation &&
            creation.TryTakeAcceptedGame(out GameServerHandshakeSession? accepted, out string accountName))
        {
            if (!roleCreation.TryOpen(accepted!, accountName))
            {
                creation.FailAcceptedGame();
            }
        }

        _roleCreation?.Update(double.IsFinite(elapsedSeconds) && elapsedSeconds > 0 ?
            TimeSpan.FromSeconds(elapsedSeconds) : TimeSpan.Zero);

        _world?.DrainIngress();
        _world?.CompleteLoad(_graphicsDevice);
        if (_world is { IsClosed: true })
        {
            _world = null;
        }

        _world?.RefreshAppearance(_graphicsDevice);

        if (_loginErrors is { } messages)
        {
            if (_loginFlow is { } flow)
            {
                login.ApplyFeedback(flow.Feedback, messages);
            }

            if (_registrationFlow is { } registration)
            {
                login.ApplyRegistrationFeedback(registration.Feedback, messages);
            }
        }
        DesktopWindow window = _window ?? throw new InvalidOperationException("The desktop window is unavailable.");
        _world?.PrepareFirstTextures(_worldTextures, window, _graphicsDevice, renderer);

        _loading?.Update(_world is { IsActive: true } ? LoginAttemptStatus.CharacterReceived : _loginFlow?.Status,
            _world is { IsPresentable: true },
            double.IsFinite(elapsedSeconds) && elapsedSeconds > 0
                ? TimeSpan.FromSeconds(elapsedSeconds) : TimeSpan.Zero);

        bool worldVisible = IsWorldVisible;
        _audio?.SetWorldActive(worldVisible);
        if (!worldVisible)
        {
            login.Advance(double.IsFinite(elapsedSeconds) && elapsedSeconds > 0 ?
                TimeSpan.FromSeconds(elapsedSeconds) : TimeSpan.Zero);
            ContinueCredentialDrag();
        }

        ClientWorld? visibleWorld = worldVisible ? _world : null;
        OpenGLRenderer frameRenderer = visibleWorld?.GetRenderer(window, _graphicsDevice, renderer) ?? renderer;
        visibleWorld?.PrepareFrame(_worldTextures, frameRenderer);

        frameRenderer.BeginFrame();
        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            if (visibleWorld is not null)
            {
                visibleWorld.DrawFrame(_worldTextures, frameRenderer, window);
            }
            else if (_roleCreation is { IsActive: true } role)
            {
                role.Draw(frameRenderer);
            }
            else
            {
                loginRenderer.Draw(frameRenderer, login, _catalog?.Availability(login.Selection) ?? default);
            }
        }
        catch (Exception exception)
        {
            firstFailure = ExceptionDispatchInfo.Capture(exception);
        }

        try
        {
            frameRenderer.EndFrame();
        }
        catch (Exception exception)
        {
            firstFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        firstFailure?.Throw();
    }

    private void AcceptWorld(GameServerHandshakeSession game, GameWorldEntrySnapshot world)
    {
        IClientContentSource source = _content ??
            throw new InvalidOperationException("World content source was not initialized.");
        _world = new ClientWorld(game, world, source, _logicalRenderSize, () => _loading?.Reset());
    }

    private void AcceptRoleWorld(GameServerHandshakeSession game, GameWorldEntrySnapshot world)
    {
        _loading?.Start();
        AcceptWorld(game, world);
    }

    private void OnLoginRequested()
    {
        if (_world is null && _roleCreation is not { IsClosing: true } && _login is { } login && _loginFlow is { } flow)
        {
            if (_loading is { Visible: false } loading &&
                login.Credentials.AccountLength != 0 && login.Credentials.PasswordLength != 0)
            {
                login.BeginLoading();
                loading.Start();
            }
            else if (!login.IsLoginPending)
            {
                flow.TryStart(login, _catalog?.CurrentCatalog(DateTimeOffset.UtcNow));
                login.ApplyFeedback(flow.Feedback, _loginErrors!);
            }
        }
    }

    private void OnRegistrationRequested()
    {
        if (_login is { } login && _registrationFlow is { } registration)
        {
            registration.Open(login.Selection, _catalog?.CurrentCatalog(DateTimeOffset.UtcNow));
            login.ApplyRegistrationFeedback(registration.Feedback, _loginErrors!);
        }
    }

    private void OnRegistrationSubmitRequested()
    {
        if (_login is { } login && _registrationFlow is { } registration)
        {
            registration.TrySubmit(login.Registration, login.Selection, _catalog?.CurrentCatalog(DateTimeOffset.UtcNow));
            login.ApplyRegistrationFeedback(registration.Feedback, _loginErrors!);
        }
    }

    private void OnRegistrationCancelRequested()
    {
        if (_login is { } login && _registrationFlow is { } registration)
        {
            registration.Close();
            login.ApplyRegistrationFeedback(registration.Feedback, _loginErrors!);
        }
    }

    private void OnFramebufferResized(PixelSize size)
    {
        _renderer?.ResizeHostFramebuffer(size.Width, size.Height);
        _world?.ResizeFramebuffer(size);
    }

    private void OnPointerMoved(PixelPoint point)
    {
        if (IsWorldVisible)
        {
            _world!.HandlePointerMoved(point);
            return;
        }

        _lastPointer = point;
        if (TryMapPointer(point, out bool mapped, out int x, out int y))
        {
            if (_roleCreation is { IsActive: true } role)
            {
                role.PointerMoved(mapped, x, y);
            }
            else
            {
                _login?.PointerMoved(mapped, x, y);
                ContinueCredentialDrag();
            }
        }
    }

    private void OnPointerPressed(PixelPoint point)
    {
        if (IsWorldVisible)
        {
            _world!.HandlePointerPressed(point);
            return;
        }

        _lastPointer = point;
        if (TryMapPointer(point, out bool mapped, out int x, out int y))
        {
            bool extend = ((_window?.KeyboardModifiers ?? DesktopKeyModifiers.None) & DesktopKeyModifiers.Shift) != 0;
            if (_roleCreation is { IsActive: true } role)
            {
                role.PointerPressed(mapped, x, y, extend);
            }
            else
            {
                _login?.PointerPressed(mapped, x, y, extend);
            }
        }
    }

    private void OnPointerReleased(PixelPoint point)
    {
        if (IsWorldVisible)
        {
            _world!.HandlePointerReleased(point);
            return;
        }

        if (TryMapPointer(point, out bool mapped, out int x, out int y))
        {
            if (_roleCreation is { IsActive: true } role)
            {
                role.PointerReleased(mapped, x, y);
            }
            else
            {
                _login?.PointerReleased(mapped, x, y);
            }
        }
    }

    private void OnKeyPressed(DesktopKeyEvent key)
    {
        if (IsWorldVisible)
        {
            return;
        }

        if (_window is { } window)
        {
            if (_roleCreation is { IsActive: true } role)
            {
                role.KeyPressed(key, window.ReadClipboardText, window.WriteClipboardText);
            }
            else
            {
                _login?.KeyPressed(key, window.ReadClipboardText, window.WriteClipboardText);
            }
        }
    }

    private void OnTextEntered(char character)
    {
        if (IsWorldVisible)
        {
            return;
        }

        if (_roleCreation is { IsActive: true } role)
        {
            role.TextEntered(character);
        }
        else
        {
            _login?.TextEntered(character);
        }
    }

    private void OnFocusChanged(bool focused)
    {
        if (!focused)
        {
            _login?.FocusLost();
            _roleCreation?.FocusLost();
        }

        _audio?.SetFocused(focused);
    }

    /// <summary>A held credential selection keeps tracking the pointer, clamped to the stage, beyond the field.</summary>
    private void ContinueCredentialDrag()
    {
        if (_login is { IsSelectingText: true } login && _renderer is { Viewport.IsEmpty: false } renderer)
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
            if (registration.Outcome is WdfPackageRegistrationOutcome.DuplicatePrefix or WdfPackageRegistrationOutcome.ArchiveUnavailable)
            {
                Console.Error.WriteLine($"OpenConquer: declared package '{registration.DeclaredName}' (prefix '{registration.Prefix}') resolved as {registration.Outcome}.");
            }
        }
    }
}
