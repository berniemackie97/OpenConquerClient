using System.Runtime.ExceptionServices;
using OpenConquer.Client.Startup;
using OpenConquer.Client.Startup.Catalog;
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
    private LoginDirectoryStatusPresentation? _directoryStatusPresentation;
    private LoginActionPresentation? _actionPresentation;
    private LoginServerIconPresentation? _serverIcons;
    private LoginActionInput? _actionInput;
    private LogicalRenderSize? _logicalRenderSize;
    private LoginArtworkSet? _loginArtwork;
    private LoginServerSelection? _loginSelection;
    private LoginSelectionInput? _loginSelectionInput;
    private LoginCredentialInput? _loginCredentials;
    private LoginCatalogRefresh? _catalogRefresh;
    private SignedLoginCatalog? _presentedCatalog;
    private bool _catalogUnavailableReported;
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
        window.PrimaryPointerPressed += OnPrimaryPointerPressed;
        window.PrimaryPointerReleased += OnPrimaryPointerReleased;
        window.PointerMoved += OnPointerMoved;
        window.KeyPressed += OnKeyPressed;
        window.TextEntered += OnTextEntered;
        window.FocusChanged += OnFocusChanged;
        window.Rendering += OnRendering;
        window.OpenGLContextReady += OnOpenGLContextReady;
        window.OpenGLContextReleasing += OnOpenGLContextReleasing;

        LoginCatalogAuthority? authority = LoginCatalogAuthority.LoadEmbedded();

        if (authority is not null)
        {
            _catalogRefresh = new LoginCatalogRefresh(token => LoadCatalogAsync(authority, token));
        }
        else
        {
            Console.Error.WriteLine("OpenConquer: realm catalog authority is not configured; no servers are available.");
        }

        try
        {
            window.Run();
            return 0;
        }
        finally
        {
            _loginCredentials?.Dispose();
            _catalogRefresh?.Dispose();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _catalogRefresh?.Cancel();

        try
        {
            _window?.Dispose();
        }
        finally
        {
            try
            {
                _catalogRefresh?.Dispose();
            }
            finally
            {
                _catalogRefresh = null;
                _presentedCatalog = null;
                _loginCredentials?.Dispose();
                _loginCredentials = null;
                _window = null;
                _loginPresentation = null;
                _directoryStatusPresentation = null;
                _actionPresentation = null;
                _serverIcons = null;
                _actionInput = null;
                _renderer = null;
                _graphicsDevice = null;
                _loginArtwork = null;
                _loginSelectionInput = null;
                _loginSelection = null;
                _disposed = true;
            }
        }
    }

    private void InitializePreWorldContent(IClientContentSource contentSource)
    {
        ArgumentNullException.ThrowIfNull(contentSource);

        GameSetupConfiguration gameSetup = GameSetupConfiguration.Load(contentSource);
        LoginArtworkSet artwork = LoginArtwork.Load();

        _logicalRenderSize = new LogicalRenderSize(gameSetup.LogicalWidthPixels, gameSetup.LogicalHeightPixels);
        _loginArtwork = artwork;
        _loginSelection = new LoginServerSelection();
        LoginVisualLayout layout = LoginVisualLayout.Create(_logicalRenderSize.Value);
        _loginSelectionInput = new LoginSelectionInput(layout, _loginSelection);
        _loginCredentials = new LoginCredentialInput(_loginSelection, LoginCredentialLayout.Create(layout));
        _actionInput = new LoginActionInput(LoginActionLayout.Create(layout));
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
        LoginDirectoryStatusPresentation? status = null;
        LoginActionPresentation? actions = null;
        LoginServerIconPresentation? icons = null;

        try
        {
            DesktopWindow window = _window ?? throw new InvalidOperationException("The desktop window has not been created.");
            LogicalRenderSize logicalSize = _logicalRenderSize ?? throw new InvalidOperationException("The logical render size has not been initialized.");
            LoginArtworkSet artwork = _loginArtwork ?? throw new InvalidOperationException("The login artwork has not been initialized.");
            PixelSize framebufferSize = window.FramebufferSize;

            renderer = graphicsDevice.CreateRenderer(logicalSize, framebufferSize.Width, framebufferSize.Height, _presentationPolicy);
            login = new LoginPresentation(graphicsDevice, logicalSize, artwork);
            LoginVisualLayout layout = LoginVisualLayout.Create(logicalSize);
            status = new LoginDirectoryStatusPresentation(graphicsDevice, layout);
            actions = new LoginActionPresentation(graphicsDevice, LoginActionLayout.Create(layout), artwork);
            icons = new LoginServerIconPresentation(graphicsDevice, layout);

            _graphicsDevice = graphicsDevice;
            _renderer = renderer;
            _loginPresentation = login;
            _directoryStatusPresentation = status;
            _actionPresentation = actions;
            _serverIcons = icons;
        }
        catch
        {
            try
            {
                icons?.Dispose();
            }
            catch { }

            try
            {
                actions?.Dispose();
            }
            catch { }

            try
            {
                status?.Dispose();
            }
            catch { }

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

    private void OnPrimaryPointerPressed(PixelPoint point) => RoutePointer(point, pressed: true);

    private void OnPrimaryPointerReleased(PixelPoint point) => RoutePointer(point, pressed: false);

    private void OnPointerMoved(PixelPoint point)
    {
        OpenGLRenderer? renderer = _renderer;
        if (renderer is null || _actionInput is null)
        {
            return;
        }

        bool mapped = renderer.Viewport.TryMapPointerToLogical(point.X, point.Y, out int x, out int y);
        _actionInput.PointerMoved(mapped, x, y);
    }

    private void OnKeyPressed(DesktopKeyEvent key)
    {
        if (_loginCredentials?.KeyPressed(key) != true)
        {
            _loginSelectionInput?.KeyPressed(key);
        }
    }

    private void OnTextEntered(char character) => _loginCredentials?.TextEntered(character);

    private void OnFocusChanged(bool focused)
    {
        if (!focused)
        {
            _loginSelectionInput?.FocusLost();
            _loginCredentials?.FocusLost();
            _actionInput?.FocusLost();
        }
    }

    private void RoutePointer(PixelPoint point, bool pressed)
    {
        LoginSelectionInput? input = _loginSelectionInput;
        OpenGLRenderer? renderer = _renderer;

        if (input is null || renderer is null)
        {
            return;
        }

        bool mapped = renderer.Viewport.TryMapPointerToLogical(point.X, point.Y, out int x, out int y);

        if (pressed)
        {
            bool actionCaptured = _actionInput?.PointerPressed(mapped, x, y) == true;
            bool credentialCaptured = !actionCaptured && _loginCredentials?.PointerPressed(mapped, x, y) == true;
            input.PointerPressed(!actionCaptured && !credentialCaptured && mapped, x, y);

            if (actionCaptured || credentialCaptured)
            {
                input.FocusLost();
            }

            if (actionCaptured)
            {
                _loginCredentials?.FocusLost();
            }
        }
        else
        {
            if (_actionInput?.PointerReleased(mapped, x, y) == LoginActionButton.Exit)
            {
                _loginCredentials?.FocusLost();
                _window?.RequestClose();
                return;
            }

            input.PointerReleased(mapped, x, y);
        }
    }

    private void OnRendering(double elapsedSeconds)
    {
        AcceptCatalogIfReady();

        OpenGLRenderer renderer = _renderer ?? throw new InvalidOperationException("The renderer is not initialized.");
        LoginPresentation login = _loginPresentation ?? throw new InvalidOperationException("The login presentation is not initialized.");
        LoginServerSelection selection = _loginSelection ?? throw new InvalidOperationException("The login selection state is not initialized.");
        LoginSelectionInput input = _loginSelectionInput ?? throw new InvalidOperationException("The login input handler is not initialized.");
        LoginCredentialInput credentials = _loginCredentials ?? throw new InvalidOperationException("The login credentials handler is not initialized.");
        LoginDirectoryStatusPresentation status = _directoryStatusPresentation ?? throw new InvalidOperationException("The realm directory status renderer is not initialized.");
        LoginActionPresentation actions = _actionPresentation ?? throw new InvalidOperationException("The login action artwork is not initialized.");
        LoginServerIconPresentation icons = _serverIcons ?? throw new InvalidOperationException("The realm icon renderer is not initialized.");
        LoginActionInput actionInput = _actionInput ?? throw new InvalidOperationException("The login action handler is not initialized.");
        credentials.Synchronize();

        renderer.BeginFrame();
        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            login.Draw(renderer, selection, input, credentials);
            icons.Draw(renderer, selection);
            actions.Draw(renderer, actionInput);
            status.Draw(renderer, LoginDirectoryStatus.Resolve(_catalogRefresh is not null, _catalogRefresh?.Current is not null,
                _catalogRefresh?.LastAttemptFailed ?? false, selection));
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

    private void OnOpenGLContextReleasing()
    {
        _catalogRefresh?.Cancel();
        _loginSelectionInput?.FocusLost();
        _loginCredentials?.FocusLost();
        _actionInput?.FocusLost();
        ReleaseRenderingResources();
    }

    private void ReleaseRenderingResources()
    {
        LoginPresentation? login = _loginPresentation;
        LoginDirectoryStatusPresentation? status = _directoryStatusPresentation;
        LoginActionPresentation? actions = _actionPresentation;
        LoginServerIconPresentation? icons = _serverIcons;
        OpenGLRenderer? renderer = _renderer;
        OpenGLGraphicsDevice? graphicsDevice = _graphicsDevice;

        _loginPresentation = null;
        _directoryStatusPresentation = null;
        _actionPresentation = null;
        _serverIcons = null;
        _renderer = null;
        _graphicsDevice = null;

        ExceptionDispatchInfo? firstFailure = null;

        try
        {
            icons?.Dispose();
        }
        catch (Exception exception) { firstFailure = ExceptionDispatchInfo.Capture(exception); }

        try
        {
            actions?.Dispose();
        }
        catch (Exception exception) { firstFailure ??= ExceptionDispatchInfo.Capture(exception); }

        try
        {
            status?.Dispose();
        }
        catch (Exception exception) { firstFailure ??= ExceptionDispatchInfo.Capture(exception); }

        try
        {
            login?.Dispose();
        }
        catch (Exception exception) { firstFailure ??= ExceptionDispatchInfo.Capture(exception); }

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

    private static async Task<SignedLoginCatalog?> LoadCatalogAsync(LoginCatalogAuthority authority, CancellationToken token)
    {
        using LoginCatalogClient client = new();
        return await client.FetchAsync(authority, DateTimeOffset.UtcNow, token).ConfigureAwait(false);
    }

    private void AcceptCatalogIfReady()
    {
        if (_catalogRefresh is not { } refresh)
        {
            return;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (refresh.Advance(now))
        {
            SignedLoginCatalog? incoming = refresh.Current;
            if (_loginSelectionInput is { } input && _loginSelection is { } selection)
            {
                LoginVerifiedSelectionRetention.Apply(_presentedCatalog, incoming, now, input, selection);
            }
            _presentedCatalog = incoming;
            _loginCredentials?.Synchronize();
        }

        if (refresh.LastAttemptFailed && !_catalogUnavailableReported)
        {
            Console.Error.WriteLine("OpenConquer: authenticated realm discovery is unavailable or rejected.");
            _catalogUnavailableReported = true;
        }
        else if (!refresh.LastAttemptFailed)
        {
            _catalogUnavailableReported = false;
        }
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
