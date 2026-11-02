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
    private readonly string _mapText;

    private LoginLoadingScreen(LoginLoadingTimeline timeline, string accountText, string gameText, string mapText)
    {
        Timeline = timeline;
        _accountText = accountText;
        _gameText = gameText;
        _mapText = mapText;
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
            encoding.GetString(numeric.GetEncoded(11239).Span));
    }

    internal void Start()
    {
        _sequence.Start();
        Trace = string.Empty;
    }

    internal void Reset()
    {
        _sequence.Reset();
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
            LoginAttemptStatus.CharacterReceived)
        {
            _sequence.AccountReady();
        }

        if (worldReady)
        {
            _sequence.WorldReady();
        }

        _sequence.Advance(elapsed);
        Trace = worldReady || status == LoginAttemptStatus.CharacterReceived ? _mapText :
            status is LoginAttemptStatus.ConnectingGame or LoginAttemptStatus.ReadingCharacter ? _gameText :
            _accountText;
    }
}
