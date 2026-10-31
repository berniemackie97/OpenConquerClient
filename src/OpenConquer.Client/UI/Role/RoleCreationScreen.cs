using System.Collections.Concurrent;
using System.Globalization;
using System.Numerics;
using System.Text;
using OpenConquer.Client.UI.Flash;
using OpenConquer.Client.UI.Role.Audio;
using OpenConquer.Content.Flash;
using OpenConquer.Content.Login;
using OpenConquer.Content.Role;
using OpenConquer.Networking.Game.Handshake;
using OpenConquer.Networking.Game.Login;
using OpenConquer.Platform.Windowing.Desktop;

namespace OpenConquer.Client.UI.Role;

/// <summary>Owns the retail create-role stage and its authenticated GameServer connection.</summary>
internal sealed class RoleCreationScreen : IRoleCommands, IAsyncDisposable
{
    private static readonly TimeSpan s_networkTimeout = TimeSpan.FromSeconds(10);
    private readonly GameServerHandshakeSession _game;
    private readonly Func<Rune, bool> _hasGlyphInk;
    private readonly CancellationTokenSource _stopping = new();
    private readonly RoleScripts _scripts;
    private readonly FlashStage _stage;
    private readonly ConcurrentQueue<string> _loadingMessages = new();
    private Task<CreationResponse>? _pending;
    private string _name = string.Empty;
    private int _body = -1;
    private int _vocation = -1;
    private string? _localError;
    private double _frameRemainder;
    private bool _stoppingRequested;
    private bool _gameStartRequested;
    private bool _loadingResumed;
    private bool _worldTransferred;
    private bool _disposed;

    public bool ConnectionLost
    {
        get; private set;
    }

    public RoleCreationScreen(GameServerHandshakeSession game, string account, SelectCharacterIni ini, IFlashRasterSource rasters,
        Func<Rune, bool> hasGlyphInk, Action<LoginAudioCue> playSound)
    {
        _game = game ?? throw new ArgumentNullException(nameof(game));
        _hasGlyphInk = hasGlyphInk ?? throw new ArgumentNullException(nameof(hasGlyphInk));
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(ini);
        ArgumentNullException.ThrowIfNull(rasters);

        FlashScriptTable table = new();
        _scripts = new RoleScripts(ini, RoleMovies.LoadCharacterInAndOut(), this, new RoleAudioFeedback(ini, playSound));
        _scripts.Register(table);
        new CharacterInAndOutScripts(_scripts).Register(table);
        _stage = new FlashStage(RoleMovies.LoadRole(), table, rasters);
        _stage.Start();
        _scripts.Receive("USERACCOUNT|" + account);
        _scripts.Receive("PATH|");
    }

    public FlashStage Stage => _stage;

    internal bool TryTakeAdmittedWorld(out GameServerHandshakeSession? game, out GameWorldEntrySnapshot? world)
    {
        if (_disposed || _worldTransferred || _pending is not null || WorldEntry is not { } admitted)
        {
            game = null;
            world = null;
            return false;
        }

        _worldTransferred = true;
        game = _game;
        world = admitted;
        return true;
    }
    public GameWorldEntrySnapshot? WorldEntry
    {
        get; private set;
    }
    public event Action? ReturnRequested;
    public event Action? ExitRequested;

    public void Tick(TimeSpan elapsed)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _frameRemainder = Math.Min(_frameRemainder + Math.Max(0, elapsed.TotalSeconds), 0.25);
        while (_frameRemainder >= 1d / 30d)
        {
            _stage.Tick();
            _stage.SetKey(13, false);
            _stage.SetKey(27, false);
            _frameRemainder -= 1d / 30d;
        }

        if (_localError is { } localError)
        {
            _localError = null;
            _scripts.Receive("ERROR|" + localError);
        }

        while (_loadingMessages.TryDequeue(out string? message))
        {
            if (!_stoppingRequested)
            {
                _scripts.Receive(message);
            }
        }

        if (_pending is not { IsCompleted: true } completed)
        {
            return;
        }

        _pending = null;
        Exception? failure = completed.Exception;
        if (_stoppingRequested)
        {
            ConnectionLost = failure is not null;
            ReturnRequested?.Invoke();
            return;
        }

        if (completed.IsCanceled || failure is not null)
        {
            // The write/read path is terminal on an unexpected transport failure.
            ConnectionLost = true;
            _stoppingRequested = true;
            _stopping.Cancel();
            ReturnRequested?.Invoke();
            return;
        }

        CreationResponse response = completed.Result;
        if (response.Refusal is { } refusal)
        {
            _scripts.Receive("ERROR|" + refusal);
        }
        else if (response.World is { } entered)
        {
            WorldEntry = entered;
            ResumeLoadingIfReady();
        }
    }

    public void PointerMoved(Vector2 position)
    {
        if (!_worldTransferred)
        {
            _stage.PointerMoved(position);
        }
    }

    public void PointerPressed(bool extendSelection)
    {
        if (!_worldTransferred)
        {
            _stage.PointerPressed(extendSelection);
        }
    }

    public void PointerReleased()
    {
        if (!_worldTransferred)
        {
            _stage.PointerReleased();
        }
    }

    public void TextEntered(char character)
    {
        if (!_worldTransferred)
        {
            _stage.TextEntered(character);
        }
    }

    public void KeyPressed(DesktopKeyEvent key, Func<string?> readClipboard, Action<string> writeClipboard)
    {
        if (_worldTransferred)
        {
            return;
        }

        if (key.Key is DesktopKey.Enter or DesktopKey.Escape)
        {
            _stage.SetKey(key.Key == DesktopKey.Enter ? 13 : 27, true);
            return;
        }

        bool shortcut = (key.Modifiers & (DesktopKeyModifiers.Control | DesktopKeyModifiers.Super)) != 0;
        if (shortcut && !key.IsRepeat)
        {
            switch (key.Key)
            {
                case DesktopKey.A:
                    _stage.SelectAll();
                    return;
                case DesktopKey.C:
                    if (_stage.SelectedText() is { Length: > 0 } selection)
                    {
                        writeClipboard(selection);
                    }
                    return;
                case DesktopKey.X:
                    string selected = _stage.SelectedText();
                    if (selected.Length != 0 && _stage.CutSelection())
                    {
                        writeClipboard(selected);
                    }
                    return;
                case DesktopKey.V:
                    if (readClipboard() is { } paste)
                    {
                        _stage.TextEntered(paste);
                    }
                    return;
            }
        }

        FlashEditKey? edit = key.Key switch
        {
            DesktopKey.Backspace => FlashEditKey.Backspace,
            DesktopKey.Delete => FlashEditKey.Delete,
            DesktopKey.Left => FlashEditKey.Left,
            DesktopKey.Right => FlashEditKey.Right,
            DesktopKey.Home => FlashEditKey.Home,
            DesktopKey.End => FlashEditKey.End,
            _ => null,
        };

        if (edit is { } action && !shortcut && (key.Modifiers & DesktopKeyModifiers.Alt) == 0)
        {
            _stage.EditKey(action, (key.Modifiers & DesktopKeyModifiers.Shift) != 0);
        }
    }

    void IRoleCommands.Command(string command, string argument)
    {
        switch (command)
        {
            case "CMD_NICK":
                _name = argument;
                break;
            case "CMD_BODYSIZE":
                _body = int.TryParse(argument, NumberStyles.Integer, CultureInfo.InvariantCulture, out int body) ? body : -1;
                break;
            case "CMD_VOCATION":
                _vocation = int.TryParse(argument, NumberStyles.Integer, CultureInfo.InvariantCulture, out int job) ? job : -1;
                break;
            case "CMD_ENTERGAME":
                Submit();
                break;
            case "CMD_DRIVERBACK":
                if (_worldTransferred || WorldEntry is not null)
                {
                    break;
                }

                if (_pending is null)
                {
                    _stoppingRequested = true;
                    _pending = SendCancelAsync();
                }
                else
                {
                    _stoppingRequested = true;
                    _stopping.Cancel();
                }
                break;
            case "CMD_EXIT":
                ExitRequested?.Invoke();
                break;
            case "CMD_GAMESTART":
                _gameStartRequested = true;
                ResumeLoadingIfReady();
                break;
            case "CMD_CLOSE":
                // Keep the role stage visible until an actual world presentation owns the display.
                break;
        }
    }

    private void ResumeLoadingIfReady()
    {
        if (!_loadingResumed && _gameStartRequested && WorldEntry is not null)
        {
            _loadingResumed = true;
            _scripts.Receive("PROGRESS|80");
            _scripts.Receive("LOADING_TRACE|Starting... Please wait...");
        }
    }

    private void Submit()
    {
        if (_pending is not null || _stoppingRequested || WorldEntry is not null)
        {
            return;
        }

        RoleNameValidationResult nameResult = RoleNameValidation.Validate(_name, _hasGlyphInk, out byte[] encodedName);
        if (nameResult != RoleNameValidationResult.Valid)
        {
            _localError = nameResult == RoleNameValidationResult.InvalidLength ?
                "First Name should be 4-15 characters!" :
                "Sorry, the name you specified contains invalid characters.";
            return;
        }

        if (_body is < 0 or > 1 || _vocation is < 0 or > 11)
        {
            _localError = "Invalid character selection.";
            return;
        }

        ushort profession = ProfessionFromVocation(_vocation);

        bool female = (_vocation & 1) != 0;
        ushort body = (ushort)(female ? (_body == 0 ? 2001 : 2002) : (_body == 0 ? 1003 : 1004));
        _gameStartRequested = false;
        _loadingResumed = false;
        _pending = CreateAsync(encodedName, body, profession, _stopping.Token);
    }

    internal static ushort ProfessionFromVocation(int vocation) => vocation switch
    {
        0 or 1 => 100,
        2 or 3 => 10,
        4 or 5 => 40,
        6 or 7 => 20,
        8 or 9 => 50,
        10 or 11 => 60,
        _ => throw new ArgumentOutOfRangeException(nameof(vocation)),
    };

    private async Task<CreationResponse> CreateAsync(byte[] name, ushort body, ushort profession, CancellationToken cancellationToken)
    {
        await _game.SendCharacterCreationAsync(name, body, profession, s_networkTimeout, cancellationToken)
            .ConfigureAwait(false);
        _loadingMessages.Enqueue("PROGRESS|1");

        GameCharacterCreationOutcome response = await _game.ReadCharacterCreationResultAsync(s_networkTimeout, cancellationToken)
            .ConfigureAwait(false);
        if (response.Kind == GameCharacterCreationKind.Refused)
        {
            string message = Encoding.Latin1.GetString(response.RefusalBytes.Span);
            return new CreationResponse(null, message.Length == 0 ? "Character creation was rejected." :
                new string(message.Where(static c => !char.IsControl(c) && c != '|').Take(160).ToArray()));
        }

        _loadingMessages.Enqueue("LOADING_TRACE|Your character has been successfully created!");
        await _game.SendRepeatedLoginProofAsync(s_networkTimeout, cancellationToken)
            .ConfigureAwait(false);

        GameLoginControlOutcome next = await _game.ReadLoginControlAsync(s_networkTimeout, cancellationToken).ConfigureAwait(false);
        if (next.Kind != GameLoginControlKind.ExistingCharacter)
        {
            throw new InvalidDataException("Created character was not admitted to the existing-character login route.");
        }

        _loadingMessages.Enqueue("LOADING_TRACE|Initializing...");
        _ = await _game.ReadExistingCharacterPreludeAsync(s_networkTimeout, cancellationToken).ConfigureAwait(false);
        GameCharacterInfo1006 character = await _game.ReadCharacterInfoAsync(s_networkTimeout, cancellationToken).ConfigureAwait(false);
        GameWorldEntrySnapshot world = await GameWorldEntryBootstrap.RunAsync(_game, character,
                () => _loadingMessages.Enqueue("LOADING_TRACE|Creating maps..."), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return new CreationResponse(world, null);
    }

    private async Task<CreationResponse> SendCancelAsync()
    {
        try
        {
            await _game.SendCharacterCreationCancelAsync(s_networkTimeout, _stopping.Token).ConfigureAwait(false);
        }
        finally
        {
            _stopping.Cancel();
        }

        return new CreationResponse(null, null);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _stopping.CancelAsync().ConfigureAwait(false);
        try
        {
            if (_pending is not null)
            {
                await _pending.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
        {
        }
        finally
        {
            try
            {
                if (!_worldTransferred)
                {
                    await _game.DisposeAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                _stopping.Dispose();
            }
        }
    }

    private sealed record CreationResponse(GameWorldEntrySnapshot? World, string? Refusal);
}
