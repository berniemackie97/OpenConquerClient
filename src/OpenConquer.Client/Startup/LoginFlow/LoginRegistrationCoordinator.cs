using System.Runtime.InteropServices;
using System.Security.Cryptography;
using OpenConquer.Client.Startup.Catalog;
using OpenConquer.Client.UI.Login;
using OpenConquer.Client.UI.Login.Selection;
using OpenConquer.Client.UI.Login.Status;
using OpenConquer.Networking.Authentication;

namespace OpenConquer.Client.Startup.LoginFlow;

/// <summary>
/// Owns the OpenConquer Sign Up flow: the two-step entry in the account panel, the first password held until it
/// is confirmed, and the single registration exchange. Only the desktop thread calls it; I/O is async.
/// </summary>
/// <remarks>
/// Retail 5517 opened a signup website. The account panel keeps its native fields and the errorMC dialog carries
/// every prompt, so the flow adds no new artwork.
/// </remarks>
internal sealed class LoginRegistrationCoordinator : IAsyncDisposable
{
    internal const int MinimumAccountLength = 4;
    internal const int MaximumAccountLength = 16;
    internal const int MinimumPasswordLength = 6;

    // The English client refuses passwords over 14 characters at login, so registering a longer one would
    // create an account it cannot sign in to.
    internal const int MaximumPasswordLength = 14;

    private readonly TimeProvider _clock;
    private byte[]? _heldName;
    private char[]? _heldPassword;
    private Task<LoginRegistrationFeedback>? _pending;
    private CancellationTokenSource? _cancellation;
    private SignedLoginCatalog? _catalog;
    private LoginServerSelection? _selection;
    private long _revision;
    private LoginRegistrationFeedback _feedback = new(LoginRegistrationStep.None);
    private bool _disposed;

    internal LoginRegistrationCoordinator(TimeProvider? clock = null)
    {
        _clock = clock ?? TimeProvider.System;
    }

    internal LoginRegistrationFeedback Feedback => _feedback;
    internal LoginRegistrationStep Step => _feedback.Step;

    /// <summary>Sign Up while idle enters the flow; Sign Up again leaves it.</summary>
    internal void Toggle()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        switch (Step)
        {
            case LoginRegistrationStep.None:
                Publish(LoginRegistrationStep.EnterCredentials, LoginRegistrationNotice.Begin);
                break;
            case LoginRegistrationStep.EnterCredentials or LoginRegistrationStep.ConfirmPassword:
                Cancel();
                break;
        }
    }

    /// <summary>Leaves the flow before submission. An exchange already sent cannot be withdrawn.</summary>
    internal void Cancel()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Step is LoginRegistrationStep.EnterCredentials or LoginRegistrationStep.ConfirmPassword)
        {
            ReleaseHeld();
            _feedback = new LoginRegistrationFeedback(LoginRegistrationStep.None);
        }
    }

    /// <summary>Handles Enter while the flow is active.</summary>
    internal bool TrySubmit(LoginScreen screen, SignedLoginCatalog? catalog)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(screen);
        screen.Credentials.Synchronize();

        return Step switch
        {
            LoginRegistrationStep.EnterCredentials => TryHoldCredentials(screen),
            LoginRegistrationStep.ConfirmPassword => TryConfirmAndStart(screen, catalog),
            _ => false,
        };
    }

    internal void Update(SignedLoginCatalog? current)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_pending is not { } pending)
        {
            return;
        }

        bool revoked = !ReferenceEquals(current, _catalog) || current is null ||
            !current.IsCurrent(_clock.GetUtcNow()) || _selection?.Revision != _revision;
        if (revoked)
        {
            _cancellation?.Cancel();
        }

        if (!pending.IsCompleted)
        {
            return;
        }

        // RunAsync maps every failure to a nonsecret notice and never faults.
        LoginRegistrationFeedback result = pending.GetAwaiter().GetResult();
        _pending = null;
        _cancellation?.Dispose();
        _cancellation = null;
        _catalog = null;
        _selection = null;
        _feedback = result;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ReleaseHeld();
        _cancellation?.Cancel();
        if (_pending is { } pending)
        {
            _ = await pending.ConfigureAwait(false);
        }

        _cancellation?.Dispose();
        _cancellation = null;
        _pending = null;
    }

    private bool TryHoldCredentials(LoginScreen screen)
    {
        if (screen.Credentials.AccountLength == 0)
        {
            Publish(LoginRegistrationStep.EnterCredentials, LoginFailure.EmptyAccount());
            return false;
        }

        if (screen.Credentials.PasswordLength == 0)
        {
            Publish(LoginRegistrationStep.EnterCredentials, LoginFailure.EmptyPassword());
            return false;
        }

        // The account and password fields only accept the native keypad alphabet, so length is all that is left
        // of the server's registration policy to check here.
        if (screen.Credentials.AccountLength is < MinimumAccountLength or > MaximumAccountLength)
        {
            Publish(LoginRegistrationStep.EnterCredentials, LoginRegistrationNotice.InvalidAccountName);
            return false;
        }

        if (screen.Credentials.PasswordLength is < MinimumPasswordLength or > MaximumPasswordLength ||
            !screen.Credentials.TryTakeLoginCredentials(out byte[] name, out char[] password))
        {
            Publish(LoginRegistrationStep.EnterCredentials, LoginRegistrationNotice.InvalidPassword);
            return false;
        }

        _heldName = name;
        _heldPassword = password;
        Publish(LoginRegistrationStep.ConfirmPassword, LoginRegistrationNotice.ConfirmPassword);
        return true;
    }

    private bool TryConfirmAndStart(LoginScreen screen, SignedLoginCatalog? catalog)
    {
        if (screen.Credentials.AccountLength == 0)
        {
            ReleaseHeld();
            Publish(LoginRegistrationStep.EnterCredentials, LoginFailure.EmptyAccount());
            return false;
        }

        if (screen.Credentials.PasswordLength == 0)
        {
            Publish(LoginRegistrationStep.ConfirmPassword, LoginFailure.EmptyPassword());
            return false;
        }

        byte[] heldName = _heldName ?? throw new InvalidOperationException("The registration lost its held account name.");
        char[] heldPassword = _heldPassword ?? throw new InvalidOperationException("The registration lost its held password.");
        if (!screen.Credentials.TryTakeLoginCredentials(out byte[] name, out char[] password))
        {
            ReleaseHeld();
            Publish(LoginRegistrationStep.EnterCredentials, LoginRegistrationNotice.PasswordMismatch);
            return false;
        }

        bool sameAccount = name.AsSpan().SequenceEqual(heldName);
        bool samePassword = password.Length == heldPassword.Length &&
            CryptographicOperations.FixedTimeEquals(MemoryMarshal.AsBytes(password.AsSpan()), MemoryMarshal.AsBytes(heldPassword.AsSpan()));
        CryptographicOperations.ZeroMemory(name);
        CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(password.AsSpan()));

        if (!sameAccount)
        {
            // The account was edited between the two entries; start over with the new name.
            ReleaseHeld();
            Publish(LoginRegistrationStep.EnterCredentials, LoginRegistrationNotice.Begin);
            return false;
        }

        if (!samePassword)
        {
            ReleaseHeld();
            Publish(LoginRegistrationStep.EnterCredentials, LoginRegistrationNotice.PasswordMismatch);
            return false;
        }

        LoginServerSelection selected = screen.Selection;
        if (catalog is null || !catalog.IsCurrent(_clock.GetUtcNow()) ||
            !ReferenceEquals(selected.Catalog, catalog.View) ||
            selected.SelectedRegion is not { } region ||
            selected.SelectedServer is not { } server ||
            !catalog.TryGetAccountEndpoint(region, server, out LoginAccountEndpoint? endpoint) ||
            endpoint is null || endpoint.Host != "127.0.0.1")
        {
            // Same resource as a login without a usable realm selection.
            ReleaseHeld();
            Publish(LoginRegistrationStep.EnterCredentials, LoginFailure.Resource(10012));
            return false;
        }

        // The held buffers now belong to the exchange, which zeroes them on every path.
        _heldName = null;
        _heldPassword = null;
        _catalog = catalog;
        _selection = selected;
        _revision = selected.Revision;
        _cancellation = new CancellationTokenSource();
        _feedback = new LoginRegistrationFeedback(LoginRegistrationStep.Submitting);
        _pending = RunAsync(catalog, selected, heldName, heldPassword, _cancellation.Token);
        return true;
    }

    private async Task<LoginRegistrationFeedback> RunAsync(SignedLoginCatalog catalog, LoginServerSelection selection,
        byte[] name, char[] password, CancellationToken cancellationToken)
    {
        try
        {
            AccountRegistrationResult result = await LoginSignedAccountExchange.RegisterAsync(catalog, selection,
                name, password, LoginAccountTimeouts.Default, _clock, cancellationToken).ConfigureAwait(false);

            return result switch
            {
                AccountRegistrationResult.Created => Notice(LoginRegistrationStep.None, LoginRegistrationNotice.Created),
                AccountRegistrationResult.AccountNameTaken => Notice(LoginRegistrationStep.EnterCredentials, LoginRegistrationNotice.AccountNameTaken),
                AccountRegistrationResult.InvalidRequest => Notice(LoginRegistrationStep.EnterCredentials, LoginRegistrationNotice.Rejected),
                AccountRegistrationResult.Unavailable => Notice(LoginRegistrationStep.EnterCredentials, LoginRegistrationNotice.Unavailable),
                _ => Notice(LoginRegistrationStep.EnterCredentials, LoginRegistrationNotice.Failed),
            };
        }
        catch (OperationCanceledException)
        {
            // Revoked by a catalog or selection change. A request already sent may still have created the
            // account, so nothing is claimed either way.
            return new LoginRegistrationFeedback(LoginRegistrationStep.EnterCredentials);
        }
        catch (LoginConnectFailureException)
        {
            return new LoginRegistrationFeedback(LoginRegistrationStep.EnterCredentials, LoginFailure.Resource(10397));
        }
        catch (Exception)
        {
            // Timeouts and malformed replies close the socket; untrusted exception text never reaches the UI.
            // Whether the server created the account is unknown, so the native connection-loss text is shown.
            return new LoginRegistrationFeedback(LoginRegistrationStep.EnterCredentials, LoginFailure.Resource(100116));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(password.AsSpan()));
            CryptographicOperations.ZeroMemory(name);
        }
    }

    private void ReleaseHeld()
    {
        if (_heldPassword is { } password)
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(password.AsSpan()));
        }

        if (_heldName is { } name)
        {
            CryptographicOperations.ZeroMemory(name);
        }

        _heldPassword = null;
        _heldName = null;
    }

    private void Publish(LoginRegistrationStep step, LoginRegistrationNotice notice) =>
        _feedback = Notice(step, notice);

    private void Publish(LoginRegistrationStep step, LoginFailure notice) =>
        _feedback = new LoginRegistrationFeedback(step, notice);

    private static LoginRegistrationFeedback Notice(LoginRegistrationStep step, LoginRegistrationNotice notice) =>
        new(step, LoginFailure.Registration(notice));
}
