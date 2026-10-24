using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using OpenConquer.Client.Startup.Catalog;
using OpenConquer.Client.UI.Login.Registration;
using OpenConquer.Client.UI.Login.Selection;
using OpenConquer.Client.UI.Login.Status;
using OpenConquer.Networking.Authentication;

namespace OpenConquer.Client.Startup.LoginFlow;

/// <summary>
/// Owns the OpenConquer Sign Up flow: whether the dialog may open, the account rules, and the single
/// registration exchange. Only the desktop thread calls it; I/O is async.
/// </summary>
/// <remarks>Retail 5517 opened a signup website from the same button.</remarks>
internal sealed class LoginRegistrationCoordinator : IAsyncDisposable
{
    private readonly TimeProvider _clock;
    private Task<LoginRegistrationFeedback>? _pending;
    private CancellationTokenSource? _cancellation;
    private SignedLoginCatalog? _catalog;
    private LoginServerSelection? _selection;
    private long _revision;
    private LoginRegistrationFeedback _feedback = new(LoginRegistrationStatus.Closed);
    private bool _disposed;

    internal LoginRegistrationCoordinator(TimeProvider? clock = null)
    {
        _clock = clock ?? TimeProvider.System;
    }

    internal LoginRegistrationFeedback Feedback => _feedback;
    internal LoginRegistrationStatus Status => _feedback.Status;

    /// <summary>Sign Up was clicked. The dialog opens only for a realm a login could use.</summary>
    internal void Open(LoginServerSelection selection, SignedLoginCatalog? catalog)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(selection);
        if (Status is LoginRegistrationStatus.Editing or LoginRegistrationStatus.Submitting)
        {
            return;
        }

        _feedback = IsUsable(selection, catalog)
            ? new LoginRegistrationFeedback(LoginRegistrationStatus.Editing)
            : new LoginRegistrationFeedback(LoginRegistrationStatus.Closed, LoginFailure.Registration(LoginRegistrationNotice.ServerNotSelected));
    }

    /// <summary>Cancel before submission. A request already sent cannot be withdrawn.</summary>
    internal void Close()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Status == LoginRegistrationStatus.Editing)
        {
            _feedback = new LoginRegistrationFeedback(LoginRegistrationStatus.Closed);
        }
    }

    /// <summary>
    /// Ok or Enter in the dialog. Checks the entries in field order, then takes them from the form and sends one
    /// request. Returns whether a request started.
    /// </summary>
    internal bool TrySubmit(LoginRegistrationForm form, LoginServerSelection selection, SignedLoginCatalog? catalog)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(selection);
        if (Status != LoginRegistrationStatus.Editing)
        {
            return false;
        }

        if (!LoginRegistrationPolicy.IsValidAccount(form.Field(LoginRegistrationField.Account).Characters))
        {
            return Refuse(LoginRegistrationNotice.InvalidAccountName, LoginRegistrationField.Account);
        }

        if (!LoginRegistrationPolicy.IsValidPassword(form.Field(LoginRegistrationField.Password).Characters))
        {
            return Refuse(LoginRegistrationNotice.InvalidPassword, LoginRegistrationField.Password);
        }

        if (!form.PasswordsMatch())
        {
            return Refuse(LoginRegistrationNotice.PasswordMismatch, LoginRegistrationField.Password);
        }

        if (!IsUsable(selection, catalog))
        {
            return Refuse(LoginRegistrationNotice.ServerNotSelected, LoginRegistrationField.Account);
        }

        if (!form.TryTakeCredentials(out byte[] name, out char[] password))
        {
            return Refuse(LoginRegistrationNotice.InvalidPassword, LoginRegistrationField.Password);
        }

        // The buffers now belong to the exchange, which zeroes them on every path.
        _catalog = catalog;
        _selection = selection;
        _revision = selection.Revision;
        _cancellation = new CancellationTokenSource();
        _feedback = new LoginRegistrationFeedback(LoginRegistrationStatus.Submitting);
        _pending = RunAsync(catalog, selection, name, password, _cancellation.Token);
        return true;
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
        _cancellation?.Cancel();
        if (_pending is { } pending)
        {
            _ = await pending.ConfigureAwait(false);
        }

        _cancellation?.Dispose();
        _cancellation = null;
        _pending = null;
    }

    /// <summary>The same realm checks a login makes before it connects.</summary>
    private bool IsUsable(LoginServerSelection selection, [NotNullWhen(true)] SignedLoginCatalog? catalog) =>
        catalog is not null && catalog.IsCurrent(_clock.GetUtcNow()) &&
        ReferenceEquals(selection.Catalog, catalog.View) &&
        selection.SelectedRegion is { } region &&
        selection.SelectedServer is { } server &&
        catalog.TryGetAccountEndpoint(region, server, out LoginAccountEndpoint? endpoint) &&
        endpoint is not null && endpoint.Host == "127.0.0.1";

    private bool Refuse(LoginRegistrationNotice notice, LoginRegistrationField field)
    {
        _feedback = Editing(LoginFailure.Registration(notice), field);
        return false;
    }

    private async Task<LoginRegistrationFeedback> RunAsync(SignedLoginCatalog catalog, LoginServerSelection selection,
        byte[] name, char[] password, CancellationToken cancellationToken)
    {
        try
        {
            AccountRegistrationResult result = await LoginSignedAccountExchange.RegisterAsync(catalog, selection,
                name, password, LoginAccountTimeouts.Default, _clock, cancellationToken).ConfigureAwait(false);

            // After any answer the passwords are gone from the form, so every refusal but an account problem
            // asks for them again.
            return result switch
            {
                AccountRegistrationResult.Created => new LoginRegistrationFeedback(LoginRegistrationStatus.Created,
                    LoginFailure.Registration(LoginRegistrationNotice.Created)),
                AccountRegistrationResult.AccountNameTaken => Editing(LoginRegistrationNotice.AccountNameTaken, LoginRegistrationField.Account),
                AccountRegistrationResult.InvalidRequest => Editing(LoginRegistrationNotice.Rejected, LoginRegistrationField.Account),
                AccountRegistrationResult.Unavailable => Editing(LoginRegistrationNotice.Unavailable, LoginRegistrationField.Password),
                _ => Editing(LoginRegistrationNotice.Failed, LoginRegistrationField.Password),
            };
        }
        catch (LoginConnectFailureException)
        {
            // Nothing reached the server; same resource as a login that cannot connect.
            return Editing(LoginFailure.Resource(10397), LoginRegistrationField.Password);
        }
        catch (Exception)
        {
            // Revocation, timeouts and malformed replies close the socket; untrusted exception text never reaches
            // the UI. The request may already have created the account, so nothing is claimed either way.
            return Editing(LoginRegistrationNotice.OutcomeUnknown, LoginRegistrationField.Password);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(password.AsSpan()));
            CryptographicOperations.ZeroMemory(name);
        }
    }

    private static LoginRegistrationFeedback Editing(LoginRegistrationNotice notice, LoginRegistrationField field) =>
        Editing(LoginFailure.Registration(notice), field);

    private static LoginRegistrationFeedback Editing(LoginFailure notice, LoginRegistrationField field) =>
        new(LoginRegistrationStatus.Editing, notice, field);
}
