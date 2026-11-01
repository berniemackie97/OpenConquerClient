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

/// <summary>
/// The retail create-role screen: hosts role.dat's stage, answers its host commands as the native
/// <c>CRoleCreateFlashDlg</c> does, and drives the GameServer exchange behind it.
/// </summary>
internal sealed class RoleCreationScreen : IRoleCommands, IAsyncDisposable
{
    private const double FrameSeconds = 1d / 30d;
    private const double MaximumCatchUpSeconds = 0.25;

    private readonly RoleCreationExchange _exchange;
    private readonly RoleCreationText _text;
    private readonly Func<Rune, bool> _hasGlyphInk;
    private readonly RoleScripts _scripts;
    private readonly FlashStage _stage;
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

    public RoleCreationScreen(GameServerHandshakeSession game, string account, SelectCharacterIni ini, RoleCreationText text,
        IFlashRasterSource rasters, Func<Rune, bool> hasGlyphInk, Action<LoginAudioCue> playSound)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(ini);
        ArgumentNullException.ThrowIfNull(rasters);
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _hasGlyphInk = hasGlyphInk ?? throw new ArgumentNullException(nameof(hasGlyphInk));

        FlashScriptTable table = new();
        _scripts = new RoleScripts(ini, RoleMovies.LoadCharacterInAndOut(), RoleMovies.LoadLogo(), this, new RoleAudioFeedback(ini, playSound));
        _scripts.Register(table);
        new CharacterInAndOutScripts(_scripts).Register(table);
        _stage = new FlashStage(RoleMovies.LoadRole(), table, rasters);
        _exchange = new RoleCreationExchange(game, text);
        _stage.Start();
        _scripts.Receive("USERACCOUNT|" + account);
        _scripts.Receive("PATH|");
    }

    public event Action? ReturnRequested;

    public event Action? ExitRequested;

    public FlashStage Stage => _stage;

    /// <summary>True when the screen closed because the GameServer connection failed, not because the player went back.</summary>
    public bool ConnectionLost
    {
        get; private set;
    }

    public GameWorldEntrySnapshot? WorldEntry
    {
        get; private set;
    }

    internal bool TryTakeAdmittedWorld(out GameServerHandshakeSession? game, out GameWorldEntrySnapshot? world)
    {
        if (_disposed || _worldTransferred || _exchange.IsBusy || WorldEntry is not { } admitted)
        {
            game = null;
            world = null;
            return false;
        }

        _worldTransferred = true;
        game = _exchange.TransferGame();
        world = admitted;
        return true;
    }

    public void Tick(TimeSpan elapsed)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _frameRemainder = Math.Min(_frameRemainder + Math.Max(0, elapsed.TotalSeconds), MaximumCatchUpSeconds);
        while (_frameRemainder >= FrameSeconds)
        {
            _stage.Tick();
            FlashStageKeyboard.ReleasePolledKeys(_stage);
            _frameRemainder -= FrameSeconds;
        }

        if (_localError is { } localError)
        {
            _localError = null;
            _scripts.Receive("ERROR|" + localError);
        }

        while (_exchange.TryDequeueLoadingMessage(out string message))
        {
            if (!_stoppingRequested)
            {
                _scripts.Receive(message);
            }
        }

        if (_exchange.TryTakeOutcome(out RoleCreationOutcome outcome))
        {
            Apply(outcome);
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
        if (!_worldTransferred)
        {
            FlashStageKeyboard.KeyPressed(_stage, key, readClipboard, writeClipboard);
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
                if (!_worldTransferred && WorldEntry is null && !_stoppingRequested)
                {
                    _stoppingRequested = true;
                    _exchange.Cancel();
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

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _exchange.DisposeAsync().ConfigureAwait(false);
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

    /// <summary>Native body look: even vocations are male (1003 big, 1004 small), odd ones female (2001, 2002).</summary>
    internal static ushort BodyFromSelection(int vocation, int bodySize) => (vocation & 1) == 0
        ? (ushort)(bodySize == 0 ? 1003 : 1004)
        : (ushort)(bodySize == 0 ? 2001 : 2002);

    private void Apply(RoleCreationOutcome outcome)
    {
        if (_stoppingRequested || outcome.Kind is RoleCreationOutcomeKind.Cancelled or RoleCreationOutcomeKind.Failed)
        {
            // A cancel the player asked for is not a lost connection, even when it interrupted a request.
            ConnectionLost = outcome.Failure is not (null or OperationCanceledException) || !_stoppingRequested;
            _stoppingRequested = true;
            ReturnRequested?.Invoke();
            return;
        }

        if (outcome.Kind == RoleCreationOutcomeKind.Refused)
        {
            _scripts.Receive("ERROR|" + outcome.Refusal);
        }
        else
        {
            WorldEntry = outcome.World;
            ResumeLoadingIfReady();
        }
    }

    private void ResumeLoadingIfReady()
    {
        if (!_loadingResumed && _gameStartRequested && WorldEntry is not null)
        {
            _loadingResumed = true;
            _scripts.Receive("PROGRESS|80");
            _scripts.Receive("LOADING_TRACE|" + _text.Starting);
        }
    }

    /// <summary><c>CRoleCreateFlashDlg_SubmitCreatePlayerRequest</c> (<c>0x51C5C3</c>): the native name gate, then the 1001.</summary>
    private void Submit()
    {
        // role.dat sends CMD_BODYSIZE and CMD_VOCATION before every CMD_ENTERGAME, so out-of-range values never reach here.
        if (_exchange.IsBusy || _stoppingRequested || WorldEntry is not null || _body is < 0 or > 1 || _vocation is < 0 or > 11)
        {
            return;
        }

        RoleNameValidationResult nameResult = RoleNameValidation.Validate(_name, _hasGlyphInk, out byte[] encodedName);
        if (nameResult != RoleNameValidationResult.Valid)
        {
            _localError = nameResult == RoleNameValidationResult.InvalidLength ? _text.NameLength : _text.NameCharacters;
            return;
        }

        _gameStartRequested = false;
        _loadingResumed = false;
        _exchange.Create(encodedName, BodyFromSelection(_vocation, _body), ProfessionFromVocation(_vocation));
    }
}
