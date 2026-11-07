using OpenConquer.Client.Startup.LoginFlow;
using OpenConquer.Client.UI.Login.Account;
using OpenConquer.Client.UI.Login.Audio;
using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Client.UI.Login.Status;
using OpenConquer.Content;
using OpenConquer.Content.Login;
using OpenConquer.Platform.Geometry;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.Presentation;

namespace OpenConquer.Client.UI.Login;

/// <summary>Owns login UI, discovery, attempts, registration, loading presentation and startup audio.</summary>
internal sealed class ClientLogin : IDisposable
{
    private readonly IClientContentSource _content;
    private readonly LoginErrorMessages _errors;
    private readonly Func<bool> _canLogin;
    private readonly TimeProvider _clock;
    private LoginCatalogBinding? _catalog;
    private LoginAudioPlayer? _audio;
    private LoginScreenRenderer? _renderer;
    private PixelPoint _lastPointer;
    private bool _disposed;

    internal ClientLogin(IClientContentSource content, LoginScreen screen, LoginAttemptCoordinator attempts,
        LoginRegistrationCoordinator registration, LoginErrorMessages errors, LoginLoadingScreen? loading,
        Func<bool> canLogin, TimeProvider? clock = null, LoginCatalogBinding? catalog = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(screen);
        ArgumentNullException.ThrowIfNull(attempts);
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(canLogin);
        _content = content;
        Screen = screen;
        Attempts = attempts;
        Registration = registration;
        _errors = errors;
        Loading = loading;
        _canLogin = canLogin;
        _clock = clock ?? TimeProvider.System;
        _catalog = catalog;
        Screen.LoginRequested += OnLoginRequested;
        Screen.PendingLoginCancelled += ResetLoading;
        Screen.RegistrationRequested += OnRegistrationRequested;
        Screen.RegistrationSubmitRequested += OnRegistrationSubmitRequested;
        Screen.RegistrationCancelRequested += OnRegistrationCancelRequested;
    }

    public LoginScreen Screen
    {
        get;
    }
    public LoginAttemptCoordinator Attempts
    {
        get;
    }
    public LoginRegistrationCoordinator Registration
    {
        get;
    }
    public LoginLoadingScreen? Loading
    {
        get;
    }
    public bool CanPresentWorld => Loading is not { Visible: true } || Loading.CanEnterWorld;

    public static ClientLogin Create(string contentRoot, IClientContentSource content, LogicalRenderSize size,
        Func<bool> canLogin, Action requestExit, TimeProvider clock)
    {
        LoginScreen? screen = null;
        LoginAttemptCoordinator? attempts = null;
        LoginRegistrationCoordinator? registration = null;
        ClientLogin? owner = null;
        try
        {
            screen = new LoginScreen(LoginStageLayout.Create(size), LoginAccountHistory.ForCurrentUser(),
                ClientVersionLabel.Load(content), cue => owner?.PlaySound(cue));
            screen.ExitRequested += requestExit;
            attempts = new LoginAttemptCoordinator(contentRoot, clock);
            LoginErrorMessages errors = LoginErrorMessages.Load(content, LoginErrorEncoding.ForCurrentHost());
            LoginLoadingScreen? loading = LoginLoadingScreen.LoadIfPresent(content);
            registration = new LoginRegistrationCoordinator(clock);
            owner = new ClientLogin(content, screen, attempts, registration, errors, loading, canLogin, clock);
            return owner;
        }
        catch
        {
            // Release partially constructed owners while preserving the initialization failure.
            ClientShutdown cleanup = new();
            cleanup.Run(() => attempts?.DisposeAsync().AsTask().GetAwaiter().GetResult());
            cleanup.Run(() => registration?.DisposeAsync().AsTask().GetAwaiter().GetResult());
            cleanup.Run(() => screen?.Dispose());
            throw;
        }
    }

    public void StartServices()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_catalog is not null)
        {
            throw new InvalidOperationException("Login services have already started.");
        }
        _catalog = LoginCatalogBinding.ForEmbeddedAuthority(_clock);
        _audio = LoginAudioPlayer.TryCreate();
    }

    public void Update()
    {
        DateTimeOffset now = _clock.GetUtcNow();
        _catalog?.Update(Screen, now);
        Attempts.Update(_catalog?.CurrentCatalog(now));
        if (Screen.AwaitingLoginCommand && Loading?.Sequence.ReadyForAuthentication == true)
        {
            Screen.DispatchPendingLogin();
            bool started = Attempts.TryStart(Screen, _catalog?.CurrentCatalog(now));
            Screen.ApplyFeedback(Attempts.Feedback, _errors);
            if (!started)
            {
                Loading.Reset();
            }
        }
        Registration.Update(_catalog?.CurrentCatalog(now));
    }

    public void ApplyFeedback()
    {
        Screen.ApplyFeedback(Attempts.Feedback, _errors);
        Screen.ApplyRegistrationFeedback(Registration.Feedback, _errors);
    }

    public void UpdateLoading(bool worldActive, bool worldPresentable, TimeSpan elapsed) =>
        Loading?.Update(worldActive ? LoginAttemptStatus.CharacterReceived : Attempts.Status, worldPresentable, elapsed);

    public void StartLoading() => Loading?.Start();
    public void ResetLoading() => Loading?.Reset();
    public void PlaySound(LoginAudioCue cue) => _audio?.Play(cue);
    public void SetWorldActive(bool active) => _audio?.SetWorldActive(active);
    public void SetFocused(bool focused) => _audio?.SetFocused(focused);
    public void CancelCatalog() => _catalog?.Cancel();

    public void CreateGraphics(OpenGLGraphicsDevice device, ResourceOwnership graphics)
    {
        _renderer = graphics.Track(new LoginScreenRenderer(device, Screen, _clock, _content, Loading));
    }

    public void BindCaret()
    {
        if (_renderer is { } renderer)
        {
            Screen.CaretLocator = renderer.CaretLocator;
            Screen.RegistrationCaretLocator = renderer.RegistrationCaretLocator;
        }
    }

    public void ReleaseGraphicsBinding()
    {
        Screen.CaretLocator = null;
        Screen.RegistrationCaretLocator = null;
        _renderer = null;
    }

    public void Draw(OpenGLRenderer renderer) =>
        (_renderer ?? throw new InvalidOperationException("The login renderer is not initialized."))
            .Draw(renderer, Screen, _catalog?.Availability(Screen.Selection) ?? default);

    public void TrackPointer(PixelPoint point) => _lastPointer = point;

    public void Advance(TimeSpan elapsed, OpenGLRenderer renderer)
    {
        Screen.Advance(elapsed);
        ContinueCredentialDrag(renderer);
    }

    /// <summary>A held credential selection keeps tracking the pointer, clamped to the stage, beyond the field.</summary>
    public void ContinueCredentialDrag(OpenGLRenderer renderer)
    {
        if (Screen.IsSelectingText && !renderer.Viewport.IsEmpty)
        {
            Screen.PointerDragged(renderer.Viewport.MapPointerXClamped(_lastPointer.X));
        }
    }

    /// <summary>Services outlive network close and the window, but are released before role fonts and login UI.</summary>
    public void ReleaseServices()
    {
        LoginCatalogBinding? catalog = _catalog;
        LoginAudioPlayer? audio = _audio;
        _catalog = null;
        _audio = null;
        ClientShutdown cleanup = new();
        cleanup.Run(() => catalog?.Dispose());
        cleanup.Run(() => audio?.Dispose());
        cleanup.ThrowIfFailed();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        ClientShutdown cleanup = new();
        cleanup.Run(CancelCatalog);
        cleanup.Run(() => Attempts.DisposeAsync().AsTask().GetAwaiter().GetResult());
        cleanup.Run(() => Registration.DisposeAsync().AsTask().GetAwaiter().GetResult());
        cleanup.Run(ReleaseServices);
        cleanup.Run(Screen.Dispose);
        cleanup.ThrowIfFailed();
    }

    private void OnLoginRequested()
    {
        if (!_canLogin())
        {
            return;
        }
        if (Loading is { Visible: false } loading &&
            Screen.Credentials.AccountLength != 0 && Screen.Credentials.PasswordLength != 0)
        {
            Screen.BeginLoading();
            loading.Start();
        }
        else if (!Screen.IsLoginPending)
        {
            Attempts.TryStart(Screen, _catalog?.CurrentCatalog(_clock.GetUtcNow()));
            Screen.ApplyFeedback(Attempts.Feedback, _errors);
        }
    }

    private void OnRegistrationRequested()
    {
        Registration.Open(Screen.Selection, _catalog?.CurrentCatalog(_clock.GetUtcNow()));
        Screen.ApplyRegistrationFeedback(Registration.Feedback, _errors);
    }

    private void OnRegistrationSubmitRequested()
    {
        Registration.TrySubmit(Screen.Registration, Screen.Selection, _catalog?.CurrentCatalog(_clock.GetUtcNow()));
        Screen.ApplyRegistrationFeedback(Registration.Feedback, _errors);
    }

    private void OnRegistrationCancelRequested()
    {
        Registration.Close();
        Screen.ApplyRegistrationFeedback(Registration.Feedback, _errors);
    }
}
