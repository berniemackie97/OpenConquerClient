using System.Numerics;
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
        Action<GameServerHandshakeSession, GameWorldEntrySnapshot> admitWorld, Action<bool> returnToLogin)
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
    }

    public bool IsActive => _screen is not null;
    public bool IsClosing => _closing is not null;
    public bool CanOpen => _screen is null && _closing is null && _disposeTask is null;

    public bool TryOpen(GameServerHandshakeSession game, string accountName)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(accountName);
        return TryOpen(() => CreateScreen(game, accountName), game.DisposeAsync);
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

        try
        {
            _screen = createScreen() ?? throw new InvalidOperationException("The role screen factory returned no screen.");
            if (_presentation is { } presentation)
            {
                _screen.Stage.TextCaretAt = presentation.LocateCaret;
            }

            _screen.ReturnRequested += OnReturnRequested;
            _screen.ExitRequested += _requestExit;
            return true;
        }
        catch (Exception exception) when (exception is FileNotFoundException or InvalidDataException or IOException or UnauthorizedAccessException)
        {
            // Preserve the content-failure classification while the unclaimed session closes.
            Console.Error.WriteLine($"OpenConquer: retail role content is unavailable ({exception.Message}).");
            _closing = closeUnclaimedSession().AsTask();
            return false;
        }
        catch
        {
            // Start session cleanup before propagating an unexpected construction failure.
            _closing = closeUnclaimedSession().AsTask();
            throw;
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
        if (_canAdmitWorld() && activeRole.TryTakeAdmittedWorld(
                out GameServerHandshakeSession? createdGame, out GameWorldEntrySnapshot? createdWorld))
        {
            _admitWorld(createdGame!, createdWorld!);
            _screen = null;
            _closing = activeRole.DisposeAsync().AsTask();
        }

        if (_returnRequested && !_hasWorldScene())
        {
            _returnRequested = false;
            _screen = null;
            _returnToLogin(activeRole.ConnectionLost);
            _closing = activeRole.DisposeAsync().AsTask();
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
        try
        {
            if (_screen is { } screen)
            {
                await screen.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _screen = null;
            try
            {
                if (_closing is { } closing)
                {
                    await closing.ConfigureAwait(false);
                }
            }
            finally
            {
                _closing = null;
            }
        }
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
        return new RoleCreationScreen(game, accountName, _ini, _text, _rasters, _glyphInk.HasMoreThanFivePixels, _playSound);
    }

    private void OnReturnRequested() => _returnRequested = true;
}
