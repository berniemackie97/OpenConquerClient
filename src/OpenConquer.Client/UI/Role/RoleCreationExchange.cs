using System.Collections.Concurrent;
using OpenConquer.Networking.Game.Handshake;
using OpenConquer.Networking.Game.Login;

namespace OpenConquer.Client.UI.Role;

/// <summary>How a create-role request ended.</summary>
internal enum RoleCreationOutcomeKind
{
    /// <summary>The GameServer refused the name; the stage stays open for another request.</summary>
    Refused,

    /// <summary>The created character passed the existing-character bootstrap and its world entry is ready.</summary>
    Admitted,

    /// <summary>The cancel (<c>CMD_DRIVERBACK</c>) was sent; the socket is closed.</summary>
    Cancelled,

    /// <summary>The transport failed, timed out or broke the protocol; the socket is closed.</summary>
    Failed,
}

internal sealed record RoleCreationOutcome(RoleCreationOutcomeKind Kind, string? Refusal = null,
    GameWorldEntrySnapshot? World = null, Exception? Failure = null);

/// <summary>
/// Owns the authenticated GameServer session behind the create-role screen and runs one exchange at a time on it:
/// the 1001 request and its result, the repeated 1052 proof and the existing-character bootstrap, or the cancel.
/// The UI thread starts exchanges and collects their outcomes; loading texts arrive through <see cref="TryDequeueLoadingMessage"/>.
/// </summary>
internal sealed class RoleCreationExchange(GameServerHandshakeSession game, RoleCreationText text) : IAsyncDisposable
{
    private static readonly TimeSpan s_networkTimeout = TimeSpan.FromSeconds(10);

    private readonly GameServerHandshakeSession _game = game ?? throw new ArgumentNullException(nameof(game));
    private readonly RoleCreationText _text = text ?? throw new ArgumentNullException(nameof(text));
    private readonly CancellationTokenSource _stopping = new();
    private readonly ConcurrentQueue<string> _loadingMessages = new();
    private Task<RoleCreationOutcome>? _pending;
    private bool _gameTransferred;
    private bool _disposed;

    public bool IsBusy => _pending is not null;

    public void Create(byte[] name, ushort body, ushort profession)
    {
        ArgumentNullException.ThrowIfNull(name);
        Start(() => CreateAsync(name, body, profession, _stopping.Token));
    }

    /// <summary>Sends the cancel, or aborts the request in flight; either way the session ends.</summary>
    public void Cancel()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_pending is null)
        {
            Start(SendCancelAsync);
        }
        else
        {
            _stopping.Cancel();
        }
    }

    public bool TryDequeueLoadingMessage(out string message)
    {
        if (_loadingMessages.TryDequeue(out string? next))
        {
            message = next;
            return true;
        }

        message = string.Empty;
        return false;
    }

    public bool TryTakeOutcome(out RoleCreationOutcome outcome)
    {
        if (_pending is not { IsCompleted: true } completed)
        {
            outcome = null!;
            return false;
        }

        _pending = null;
        outcome = completed.IsCompletedSuccessfully ? completed.Result
            : new RoleCreationOutcome(RoleCreationOutcomeKind.Failed, Failure: completed.Exception?.GetBaseException() ??
                new OperationCanceledException("The create-role exchange was cancelled."));
        if (outcome.Kind is RoleCreationOutcomeKind.Failed)
        {
            _stopping.Cancel();
        }

        return true;
    }

    /// <summary>Hands the session to the world after an admitted outcome; this exchange no longer disposes it.</summary>
    public GameServerHandshakeSession TransferGame()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_gameTransferred || _pending is not null)
        {
            throw new InvalidOperationException("The GameServer session cannot be transferred now.");
        }

        _gameTransferred = true;
        return _game;
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
            if (_pending is { } pending)
            {
                // The session is closing; whatever ended the exchange no longer matters.
                await ((Task)pending).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }
        }
        finally
        {
            try
            {
                if (!_gameTransferred)
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

    private void Start(Func<Task<RoleCreationOutcome>> exchange)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_pending is not null || _gameTransferred || _stopping.IsCancellationRequested)
        {
            throw new InvalidOperationException("A create-role exchange is already running or the session has ended.");
        }

        _pending = exchange();
    }

    private async Task<RoleCreationOutcome> CreateAsync(byte[] name, ushort body, ushort profession, CancellationToken cancellationToken)
    {
        await _game.SendCharacterCreationAsync(name, body, profession, s_networkTimeout, cancellationToken).ConfigureAwait(false);
        _loadingMessages.Enqueue("PROGRESS|1");

        GameCharacterCreationOutcome response = await _game.ReadCharacterCreationResultAsync(s_networkTimeout, cancellationToken)
            .ConfigureAwait(false);
        if (response.Kind == GameCharacterCreationKind.Refused)
        {
            return new RoleCreationOutcome(RoleCreationOutcomeKind.Refused, Refusal: _text.Refusal(response.RefusalBytes.Span));
        }

        _loadingMessages.Enqueue("LOADING_TRACE|" + _text.Created);
        await _game.SendRepeatedLoginProofAsync(s_networkTimeout, cancellationToken).ConfigureAwait(false);

        GameLoginControlOutcome next = await _game.ReadLoginControlAsync(s_networkTimeout, cancellationToken).ConfigureAwait(false);
        if (next.Kind != GameLoginControlKind.ExistingCharacter)
        {
            throw new InvalidDataException("The GameServer did not admit the created character.");
        }

        _loadingMessages.Enqueue("LOADING_TRACE|" + _text.Initializing);
        _ = await _game.ReadExistingCharacterPreludeAsync(s_networkTimeout, cancellationToken).ConfigureAwait(false);
        GameCharacterInfo1006 character = await _game.ReadCharacterInfoAsync(s_networkTimeout, cancellationToken).ConfigureAwait(false);
        GameWorldEntrySnapshot world = await GameWorldEntryBootstrap.RunAsync(_game, character,
                () => _loadingMessages.Enqueue("LOADING_TRACE|" + _text.CreatingMaps), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return new RoleCreationOutcome(RoleCreationOutcomeKind.Admitted, World: world);
    }

    private async Task<RoleCreationOutcome> SendCancelAsync()
    {
        try
        {
            await _game.SendCharacterCreationCancelAsync(s_networkTimeout, _stopping.Token).ConfigureAwait(false);
        }
        finally
        {
            _stopping.Cancel();
        }

        return new RoleCreationOutcome(RoleCreationOutcomeKind.Cancelled);
    }
}
