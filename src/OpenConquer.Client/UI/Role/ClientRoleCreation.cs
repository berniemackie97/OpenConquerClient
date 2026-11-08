using System.Numerics;
using OpenConquer.Client.Diagnostics;
using OpenConquer.Client.UI.Flash;
using OpenConquer.Client.UI.Login.Status;
using OpenConquer.Content;
using OpenConquer.Content.Configuration;
using OpenConquer.Content.Login;
using OpenConquer.Content.Role;
using OpenConquer.Networking.Game.Handshake;
using OpenConquer.Networking.Game.Login;
using OpenConquer.Platform.Windowing.Desktop;
using OpenConquer.Rendering.OpenGL;
using OpenConquer.Rendering.Text.Fonts;

namespace OpenConquer.Client.UI.Role;

/// <summary>
/// Owns the create-role screen, its cached content, input and session-close lifetime. The application supplies
/// transitions to login/world and the context resource group; native commands and exchanges remain on the screen.
/// </summary>
internal sealed class ClientRoleCreation : IAsyncDisposable
{
    private readonly IClientContentSource _content;
    private readonly Action<LoginAudioCue> _playSound;
    private readonly Action _requestExit;
    private readonly Func<bool> _canAdmitWorld;
    private readonly Func<bool> _hasWorldScene;
    private readonly Action<GameServerHandshakeSession, GameWorldEntrySnapshot> _admitWorld;
    private readonly Action<bool> _returnToLogin;
    private readonly FlashRasterCache _rasters = new();
    private readonly Func<GameServerHandshakeSession, string, IRoleCreationScreen> _createScreen;
    private readonly TimeProvider _clock;
    private readonly ClientDiagnostics _diagnostics;
    private SelectCharacterIni? _ini;
    private RoleCreationText? _text;
    private FontGlyphInk? _glyphInk;
    private FlashStagePresentation? _presentation;
    private IRoleCreationScreen? _screen;
    private bool _returnRequested;
    private Task? _closing;
    private Task? _disposeTask;

    public ClientRoleCreation(IClientContentSource content, Action<LoginAudioCue> playSound, Action requestExit,
        Func<bool> canAdmitWorld, Func<bool> hasWorldScene,
        Action<GameServerHandshakeSession, GameWorldEntrySnapshot> admitWorld, Action<bool> returnToLogin,
        Func<GameServerHandshakeSession, string, IRoleCreationScreen>? createScreen = null, TimeProvider? clock = null, ClientDiagnostics? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(playSound);
        ArgumentNullException.ThrowIfNull(requestExit);
        ArgumentNullException.ThrowIfNull(canAdmitWorld);
        ArgumentNullException.ThrowIfNull(hasWorldScene);
        ArgumentNullException.ThrowIfNull(admitWorld);
        ArgumentNullException.ThrowIfNull(returnToLogin);
        _content = content;
        _playSound = playSound;
        _requestExit = requestExit;
        _canAdmitWorld = canAdmitWorld;
        _hasWorldScene = hasWorldScene;
        _admitWorld = admitWorld;
        _returnToLogin = returnToLogin;
        _clock = clock ?? TimeProvider.System;
        _diagnostics = diagnostics ?? ClientDiagnostics.Silent;
        _createScreen = createScreen ?? CreateScreen;
    }

    public bool IsActive => _screen is not null;
    public bool IsClosing => _closing is not null;
    public bool CanOpen => _screen is null && _closing is null && _disposeTask is null;

    public bool TryOpen(GameServerHandshakeSession game, string accountName)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(accountName);
        return TryOpen(() => _createScreen(game, accountName), game.DisposeAsync);
    }

    /// <summary>Transfers the accepted session only after its screen is constructed; failed construction closes it.</summary>
    internal bool TryOpen(Func<IRoleCreationScreen> createScreen, Func<ValueTask> closeUnclaimedSession)
    {
        ArgumentNullException.ThrowIfNull(createScreen);
        ArgumentNullException.ThrowIfNull(closeUnclaimedSession);
        if (!CanOpen)
        {
            throw new InvalidOperationException("Role creation is active, closing or disposed.");
        }

        _returnRequested = false;
        IRoleCreationScreen? screen = null;
        try
        {
            screen = createScreen() ?? throw new InvalidOperationException("The role screen factory returned no screen.");
            if (_presentation is { } presentation)
            {
                screen.Stage.TextCaretAt = presentation.LocateCaret;
            }

            screen.ReturnRequested += OnReturnRequested;
            screen.ExitRequested += _requestExit;
            _screen = screen;
            return true;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _closing = screen is null ? CloseUnclaimedAsync(closeUnclaimedSession) : CloseScreenAsync(screen);
            _diagnostics.RoleContentUnavailable(ClientFailure.Classify(exception));
            return false;
        }
        catch
        {
            // Observe cleanup separately so a synchronous close failure cannot replace the original failure.
            _closing = screen is null ? CloseUnclaimedAsync(closeUnclaimedSession) : CloseScreenAsync(screen);
            throw;
        }
    }

    private static Task CloseUnclaimedAsync(Func<ValueTask> close)
    {
        try
        {
            return close().AsTask();
        }
        catch (Exception failure)
        {
            return Task.FromException(failure);
        }
    }

    public void ObserveClose()
    {
        if (_closing is { IsCompleted: true } closed)
        {
            _closing = null;
            closed.GetAwaiter().GetResult();
        }
    }

    public void Update(TimeSpan elapsed)
    {
        if (_screen is not { } activeRole)
        {
            return;
        }

        activeRole.Tick(elapsed);
        if (_returnRequested && !_hasWorldScene())
        {
            _returnRequested = false;
            _screen = null;
            _closing = CloseScreenAsync(activeRole, () => _returnToLogin(activeRole.ConnectionLost));
            return;
        }

        if (_canAdmitWorld() && activeRole.TryTakeAdmittedWorld(
                out GameServerHandshakeSession? createdGame, out GameWorldEntrySnapshot? createdWorld))
        {
            _admitWorld(createdGame, createdWorld);
            _screen = null;
            _returnRequested = false;
            _closing = CloseScreenAsync(activeRole);
        }
    }

    /// <summary>Registers role graphics at their existing position in the window's reverse-order resource group.</summary>
    public void CreateGraphics(OpenGLGraphicsDevice device, ResourceOwnership graphics)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(graphics);
        _presentation = graphics.Track(new FlashStagePresentation(device, _rasters));
    }

    public void BindCaret()
    {
        if (_screen is { } screen && _presentation is { } presentation)
        {
            screen.Stage.TextCaretAt = presentation.LocateCaret;
        }
    }

    /// <summary>Detaches stage callbacks before the context's resource group disposes the presentation.</summary>
    public void ReleaseGraphicsBinding()
    {
        if (_screen is { } screen)
        {
            screen.Stage.TextCaretAt = null;
        }

        _presentation = null;
    }

    public void Draw(OpenGLRenderer renderer)
    {
        if (_screen is { } screen)
        {
            (_presentation ?? throw new InvalidOperationException("Role renderer is unavailable.")).Draw(renderer, screen.Stage);
        }
    }

    public void PointerMoved(bool mapped, int x, int y) =>
        _screen?.PointerMoved(mapped ? new Vector2(x, y) : new Vector2(-1, -1));

    public void PointerPressed(bool mapped, int x, int y, bool extendSelection)
    {
        if (mapped && _screen is { } screen)
        {
            screen.PointerMoved(new Vector2(x, y));
            screen.PointerPressed(extendSelection);
        }
    }

    public void PointerReleased(bool mapped, int x, int y)
    {
        if (_screen is { } screen)
        {
            screen.PointerMoved(mapped ? new Vector2(x, y) : new Vector2(-1, -1));
            screen.PointerReleased();
        }
    }

    public void KeyPressed(DesktopKeyEvent key, Func<string?> readClipboard, Action<string> writeClipboard) =>
        _screen?.KeyPressed(key, readClipboard, writeClipboard);

    public void TextEntered(char character) => _screen?.TextEntered(character);
    public void FocusLost() => _screen?.Stage.SetFocus(null);

    public ValueTask DisposeAsync() => new(_disposeTask ??= DisposeCoreAsync());

    /// <summary>Releases cached name-gate font resources after the window closes, at the original host release point.</summary>
    public void ReleaseContent() => _glyphInk?.Dispose();

    private async Task DisposeCoreAsync()
    {
        ClientShutdown cleanup = new();
        IRoleCreationScreen? screen = _screen;
        Task? closing = _closing;
        _screen = null;
        _closing = null;
        _returnRequested = false;

        if (screen is not null)
        {
            await cleanup.RunAsync(() => new ValueTask(CloseScreenAsync(screen))).ConfigureAwait(false);
        }

        if (closing is not null)
        {
            await cleanup.RunAsync(() => new ValueTask(closing)).ConfigureAwait(false);
        }

        cleanup.ThrowIfFailed();
    }

    private async Task CloseScreenAsync(IRoleCreationScreen screen, Action? notify = null)
    {
        ClientShutdown cleanup = new();
        cleanup.Run(() => screen.ReturnRequested -= OnReturnRequested);
        cleanup.Run(() => screen.ExitRequested -= _requestExit);
        cleanup.Run(() => screen.Stage.TextCaretAt = null);
        if (notify is not null)
        {
            cleanup.Run(notify);
        }

        await cleanup.RunAsync(screen.DisposeAsync).ConfigureAwait(false);
        cleanup.ThrowIfFailed();
    }

    private RoleCreationScreen CreateScreen(GameServerHandshakeSession game, string accountName)
    {
        _ini ??= SelectCharacterIni.Load(_content);
        if (_glyphInk is null)
        {
            ClientFontSettingsConfiguration fontSettings = ClientFontSettingsConfiguration.Load(_content);
            GameFontConfiguration font = GameFontConfiguration.Load(_content);
            _glyphInk = new FontGlyphInk(fontSettings.GuiFontFaceName, font.NominalPixelHeight);
        }

        _text ??= RoleCreationText.Load(_content, LoginErrorEncoding.ForCurrentHost());
        return new RoleCreationScreen(game, accountName, _ini, _text, _rasters, _glyphInk.HasMoreThanFivePixels, _playSound, _clock);
    }

    private void OnReturnRequested() => _returnRequested = true;
}
