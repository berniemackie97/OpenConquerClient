using OpenConquer.Client.UI.Login.Status;
using OpenConquer.Content;
using OpenConquer.Content.Login;
using OpenConquer.Content.Text;

namespace OpenConquer.Client.UI.Login.Stage;

internal sealed class LoginLoadingScreen
{
    private readonly LoginLoadingSequence _sequence = new();
    private readonly string _accountText;
    private readonly string _gameText;
    private readonly string _initializingText;
    private readonly string _mapText;
    private readonly string _startingText;
    private bool _startingTraced;

    internal LoginLoadingScreen(LoginLoadingTimeline timeline, string accountText, string gameText,
        string initializingText, string mapText, string startingText)
    {
        Timeline = timeline;
        _accountText = accountText;
        _gameText = gameText;
        _initializingText = initializingText;
        _mapText = mapText;
        _startingText = startingText;
    }

    internal LoginLoadingTimeline Timeline
    {
        get;
    }

    internal LoginLoadingSequence Sequence => _sequence;
    internal bool Visible => _sequence.Visible;
    internal bool CanEnterWorld => _sequence.CanEnterWorld;

    internal string Trace
    {
        get; private set;
    } = string.Empty;

    internal static LoginLoadingScreen? LoadIfPresent(IClientContentSource source)
    {
        if (!LoginLoadingContentPaths.IsPresent(source))
        {
            return null;
        }

        System.Text.Encoding encoding = LoginErrorEncoding.ForCurrentHost();
        ClientStringResources numeric = ClientStringResources.Load(source);
        ClientKeyedStringResources keyed = ClientKeyedStringResources.Load(source);

        return new LoginLoadingScreen(LoginLoadingTimeline.Load(source),
            encoding.GetString(numeric.GetEncoded(11237).Span),
            encoding.GetString(keyed.GetEncoded("STR_LOGIN_GAME_SERVER").Span),
            encoding.GetString(numeric.GetEncoded(11238).Span),
            encoding.GetString(numeric.GetEncoded(11239).Span),
            encoding.GetString(numeric.GetEncoded(11240).Span));
    }

    internal void Start()
    {
        _sequence.Start();
        _startingTraced = false;
        Trace = string.Empty;
    }

    internal void Reset()
    {
        _sequence.Reset();
        _startingTraced = false;
        Trace = string.Empty;
    }

    internal void Update(LoginAttemptStatus? status, bool worldReady, TimeSpan elapsed)
    {
        if (!Visible)
        {
            return;
        }

        if (status is LoginAttemptStatus.CharacterCreation or LoginAttemptStatus.AccountRefused or
            LoginAttemptStatus.GameRefused or LoginAttemptStatus.Timeout or LoginAttemptStatus.Unavailable or
            LoginAttemptStatus.Cancelled)
        {
            Reset();
            return;
        }

        if (status is LoginAttemptStatus.ConnectingGame or LoginAttemptStatus.ReadingCharacter or
            LoginAttemptStatus.InitializingWorld or LoginAttemptStatus.CharacterReceived)
        {
            _sequence.AccountReady();
        }

        if (worldReady)
        {
            _sequence.WorldReady();
        }

        _sequence.Advance(elapsed);
        Trace = status switch
        {
            LoginAttemptStatus.ConnectingGame or LoginAttemptStatus.ReadingCharacter => _gameText,
            LoginAttemptStatus.InitializingWorld => _initializingText,
            LoginAttemptStatus.CharacterReceived => _mapText,
            _ => _accountText,
        };

        if (worldReady)
        {
            Trace = !_startingTraced && _sequence.Frame >= 36 ? _startingText : _mapText;
            if (_sequence.Frame >= 36)
            {
                _startingTraced = true;
            }
        }
    }
}
