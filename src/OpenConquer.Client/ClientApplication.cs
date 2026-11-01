using System.Runtime.ExceptionServices;
using OpenConquer.Client.Startup;
using OpenConquer.Client.Startup.LoginFlow;
using OpenConquer.Client.UI;
using OpenConquer.Client.UI.Flash;
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
using OpenConquer.Content.Map;
using OpenConquer.Content.Role;
using OpenConquer.Content.Startup;
using OpenConquer.Content.Wdf;
using OpenConquer.Networking.Game.Handshake;
using OpenConquer.Networking.Game.Login;
using OpenConquer.Platform.Geometry;
using OpenConquer.Platform.OpenGL;
using OpenConquer.Platform.Windowing.Desktop;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.Presentation;
using OpenConquer.Rendering.Text.Fonts;

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
    private OpenGLRenderer? _renderer;
    private LoginScreenRenderer? _loginRenderer;
    private RoleCreationScreen? _role;
    private GameServerHandshakeSession? _worldGame;
    private GameWorldEntrySnapshot? _worldEntry;
    private GameMapCatalog? _terrainCatalog;
    private WorldMapScene? _worldScene;
    private WorldMapTextureRenderer? _worldTextures;
    private IClientContentSource? _roleContent;
    private SelectCharacterIni? _roleIni;
    private RoleCreationText? _roleText;
    private FontGlyphInk? _roleGlyphInk;
    private FlashRasterCache? _roleRasters;
    private FlashStagePresentation? _rolePresentation;
    private bool _returnFromRole;
    private Task? _roleClosing;
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
            try
            {
                try
                {
                    _role?.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
                finally
                {
                    _role = null;
                    try
                    {
                        _roleClosing?.GetAwaiter().GetResult();
                    }
                    finally
                    {
                        _roleClosing = null;
                        _loginFlow?.DisposeAsync().AsTask().GetAwaiter().GetResult();
                    }
                }
            }
            finally
            {
                try
                {
                    _worldGame?.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
                finally
                {
                    _worldGame = null;
                    _worldEntry = null;
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
                    _roleGlyphInk?.Dispose();
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
    }

    private void InitializeLogin(IClientContentSource contentSource)
    {
        GameSetupConfiguration gameSetup = GameSetupConfiguration.Load(contentSource);
        _logicalRenderSize = new LogicalRenderSize(gameSetup.LogicalWidthPixels, gameSetup.LogicalHeightPixels);

        _roleContent = contentSource;
        _roleRasters = new FlashRasterCache();
        _login = new LoginScreen(LoginStageLayout.Create(_logicalRenderSize), LoginAccountHistory.ForCurrentUser(),
            ClientVersionLabel.Load(contentSource), cue => _audio?.Play(cue));
        _login.ExitRequested += () => _window?.RequestClose();
        _loginFlow = new LoginAttemptCoordinator(_contentRootPath);
        _loginErrors = LoginErrorMessages.Load(contentSource, LoginErrorEncoding.ForCurrentHost());
        _login.LoginRequested += OnLoginRequested;
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
            _loginRenderer = graphics.Track(new LoginScreenRenderer(device, login));
            _rolePresentation = graphics.Track(new FlashStagePresentation(device,
                _roleRasters ?? throw new InvalidOperationException("Role raster source is missing.")));
            _worldTextures = graphics.Track(new WorldMapTextureRenderer(
                _roleContent ?? throw new InvalidOperationException("World content source is missing."), device));
            if (_role is { } activeRole)
            {
                activeRole.Stage.TextCaretAt = _rolePresentation.LocateCaret;
            }

            login.CaretLocator = _loginRenderer.CaretLocator;
            login.RegistrationCaretLocator = _loginRenderer.RegistrationCaretLocator;
            _graphics = graphics;
        }
        catch
        {
            _renderer = null;
            _loginRenderer = null;
            _rolePresentation = null;
            _worldTextures = null;
            graphics.DisposeAfterInitializationFailure();
            throw;
        }
    }

    private void OnOpenGLContextReleasing()
    {
        _catalog?.Cancel();
        _login?.FocusLost();
        if (_role is { } activeRole)
        {
            activeRole.Stage.TextCaretAt = null;
        }

        if (_login is not null)
        {
            _login.CaretLocator = null;
            _login.RegistrationCaretLocator = null;
        }

        ResourceOwnership? graphics = _graphics;
        _graphics = null;
        _renderer = null;
        _loginRenderer = null;
        _rolePresentation = null;
        _worldTextures = null;
        graphics?.Dispose();
    }

    private void OnRendering(double elapsedSeconds)
    {
        LoginScreen login = _login ?? throw new InvalidOperationException("The login screen is not initialized.");
        OpenGLRenderer renderer = _renderer ?? throw new InvalidOperationException("The renderer is not initialized.");
        LoginScreenRenderer loginRenderer = _loginRenderer ?? throw new InvalidOperationException("The login renderer is not initialized.");

        DateTimeOffset now = DateTimeOffset.UtcNow;
        _catalog?.Update(login, now);
        _loginFlow?.Update(_catalog?.CurrentCatalog(now));
        _registrationFlow?.Update(_catalog?.CurrentCatalog(now));
        if (_worldGame is null && _loginFlow is { } existing &&
            existing.TryTakeAdmittedWorld(out GameServerHandshakeSession? acceptedGame,
                out GameWorldEntrySnapshot? acceptedWorld))
        {
            AcceptWorld(acceptedGame!, acceptedWorld!);
        }
        if (_roleClosing is { IsCompleted: true } closed)
        {
            _roleClosing = null;
            closed.GetAwaiter().GetResult();
        }

        if (_role is null && _roleClosing is null &&
            _loginFlow is { Status: LoginAttemptStatus.CharacterCreation } creation &&
            creation.TryTakeAcceptedGame(out var accepted, out string accountName))
        {
            try
            {
                IClientContentSource roleContent = _roleContent ??
                    throw new InvalidOperationException("Role content source was not initialized.");
                _roleIni ??= SelectCharacterIni.Load(roleContent);
                if (_roleGlyphInk is null)
                {
                    ClientFontSettingsConfiguration fontSettings = ClientFontSettingsConfiguration.Load(roleContent);
                    GameFontConfiguration font = GameFontConfiguration.Load(roleContent);
                    _roleGlyphInk = new FontGlyphInk(fontSettings.GuiFontFaceName, font.NominalPixelHeight);
                }

                _roleText ??= RoleCreationText.Load(roleContent, LoginErrorEncoding.ForCurrentHost());
                _role = new RoleCreationScreen(accepted!, accountName, _roleIni, _roleText,
                    _roleRasters ?? throw new InvalidOperationException("Role raster source is unavailable."),
                    _roleGlyphInk.HasMoreThanFivePixels, cue => _audio?.Play(cue));
                if (_rolePresentation is { } presentation)
                {
                    _role.Stage.TextCaretAt = presentation.LocateCaret;
                }

                _role.ReturnRequested += () => _returnFromRole = true;
                _role.ExitRequested += () => _window?.RequestClose();
            }
            catch (Exception exception) when (exception is FileNotFoundException or InvalidDataException or IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"OpenConquer: retail role content is unavailable ({exception.Message}).");
                _roleClosing = accepted?.DisposeAsync().AsTask();
                creation.FailAcceptedGame();
            }
            catch
            {
                _roleClosing = accepted?.DisposeAsync().AsTask();
                throw;
            }
        }

        if (_role is { } activeRole)
        {
            activeRole.Tick(double.IsFinite(elapsedSeconds) && elapsedSeconds > 0 ?
                TimeSpan.FromSeconds(elapsedSeconds) : TimeSpan.Zero);
            if (_worldGame is null && activeRole.TryTakeAdmittedWorld(
                    out GameServerHandshakeSession? createdGame, out GameWorldEntrySnapshot? createdWorld))
            {
                AcceptWorld(createdGame!, createdWorld!);
                _role = null;
                _roleClosing = activeRole.DisposeAsync().AsTask();
            }

            if (_returnFromRole && _worldScene is null)
            {
                _returnFromRole = false;
                _role = null;
                _loginFlow?.CompleteRoleReturn(activeRole.ConnectionLost);
                _roleClosing = activeRole.DisposeAsync().AsTask();
            }
        }

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
        if (_worldScene is null)
        {
            login.Advance(double.IsFinite(elapsedSeconds) && elapsedSeconds > 0 ?
                TimeSpan.FromSeconds(elapsedSeconds) : TimeSpan.Zero);
            ContinueCredentialDrag();
        }

        if (_worldEntry is not null && _worldGame is null)
        {
            throw new InvalidOperationException("World admission lost its authenticated connection.");
        }

        if (_worldScene is { } scene)
        {
            scene.Prepare(_worldTextures ?? throw new InvalidOperationException("World textures are unavailable."),
                _logicalRenderSize.Width, _logicalRenderSize.Height, unchecked((uint)Environment.TickCount));
        }

        renderer.BeginFrame();
        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            if (_worldScene is { } worldScene)
            {
                worldScene.Draw(_worldTextures!, renderer);
            }
            else if (_role is { } role)
            {
                (_rolePresentation ?? throw new InvalidOperationException("Role renderer is unavailable."))
                    .Draw(renderer, role.Stage);
            }
            else
            {
                loginRenderer.Draw(renderer, login, _catalog?.Availability(login.Selection) ?? default);
            }
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

    private void AcceptWorld(GameServerHandshakeSession game, GameWorldEntrySnapshot world)
    {
        // Own the authenticated connection before any content operation can fail.
        _worldGame = game;
        _worldEntry = world;
        IClientContentSource source = _roleContent ??
            throw new InvalidOperationException("World content source was not initialized.");
        _terrainCatalog ??= GameMapCatalog.Load(source);
        _worldScene = WorldMapScene.Load(source, _terrainCatalog,
            world.MapDataId, world.X, world.Y, unchecked((uint)Environment.TickCount));
    }

    private void OnLoginRequested()
    {
        if (_worldGame is null && _roleClosing is null && _login is { } login && _loginFlow is { } flow)
        {
            flow.TryStart(login, _catalog?.CurrentCatalog(DateTimeOffset.UtcNow));
            login.ApplyFeedback(flow.Feedback, _loginErrors!);
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

    private void OnFramebufferResized(PixelSize size) => _renderer?.ResizeHostFramebuffer(size.Width, size.Height);

    private void OnPointerMoved(PixelPoint point)
    {
        if (_worldScene is not null)
        {
            return;
        }

        _lastPointer = point;
        if (TryMapPointer(point, out bool mapped, out int x, out int y))
        {
            if (_role is { } role)
            {
                role.PointerMoved(mapped ? new System.Numerics.Vector2(x, y) : new System.Numerics.Vector2(-1, -1));
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
        if (_worldScene is not null)
        {
            return;
        }

        _lastPointer = point;
        if (TryMapPointer(point, out bool mapped, out int x, out int y))
        {
            bool extend = ((_window?.KeyboardModifiers ?? DesktopKeyModifiers.None) & DesktopKeyModifiers.Shift) != 0;
            if (_role is { } role)
            {
                if (mapped)
                {
                    role.PointerMoved(new System.Numerics.Vector2(x, y));
                    role.PointerPressed(extend);
                }
            }
            else
            {
                _login?.PointerPressed(mapped, x, y, extend);
            }
        }
    }

    private void OnPointerReleased(PixelPoint point)
    {
        if (_worldScene is not null)
        {
            return;
        }

        if (TryMapPointer(point, out bool mapped, out int x, out int y))
        {
            if (_role is { } role)
            {
                role.PointerMoved(mapped ? new System.Numerics.Vector2(x, y) : new System.Numerics.Vector2(-1, -1));
                role.PointerReleased();
            }
            else
            {
                _login?.PointerReleased(mapped, x, y);
            }
        }
    }

    private void OnKeyPressed(DesktopKeyEvent key)
    {
        if (_worldScene is not null)
        {
            return;
        }

        if (_window is { } window)
        {
            if (_role is { } role)
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
        if (_worldScene is not null)
        {
            return;
        }

        if (_role is { } role)
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
            _role?.Stage.SetFocus(null);
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
            if (registration.Outcome != WdfPackageRegistrationOutcome.Registered)
            {
                Console.Error.WriteLine($"OpenConquer: declared package '{registration.DeclaredName}' (prefix '{registration.Prefix}') resolved as {registration.Outcome}.");
            }
        }
    }
}
