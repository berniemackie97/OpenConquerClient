using OpenConquer.Client.UI.Login.Status;

namespace OpenConquer.Client.UI.Login.Registration;

internal enum LoginRegistrationStatus
{
    Closed,
    Editing,
    Submitting,
    Created,
}

internal sealed record LoginRegistrationFeedback(LoginRegistrationStatus Status, LoginFailure? Notice = null, LoginRegistrationField Focus = LoginRegistrationField.Account);
