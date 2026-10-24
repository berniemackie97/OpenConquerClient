using OpenConquer.Client.UI.Login.Status;

namespace OpenConquer.Client.UI.Login.Registration;

/// <summary>Where the OpenConquer Sign Up flow stands; retail 5517 opened a website instead.</summary>
internal enum LoginRegistrationStatus
{
    /// <summary>No Sign Up dialog. A notice opens errorMC.</summary>
    Closed,
    /// <summary>The dialog takes input. A notice is shown in it and moves focus to the field to correct.</summary>
    Editing,
    /// <summary>The request is in flight and the dialog is locked.</summary>
    Submitting,
    /// <summary>The account exists: the dialog closes, its name fills the login account field, and the notice opens errorMC.</summary>
    Created,
}

/// <summary>An atomic snapshot for the screen. A new instance is one notification, even if its text repeats.</summary>
internal sealed record LoginRegistrationFeedback(LoginRegistrationStatus Status, LoginFailure? Notice = null,
    LoginRegistrationField Focus = LoginRegistrationField.Account);
