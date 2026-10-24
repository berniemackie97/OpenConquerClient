namespace OpenConquer.Client.UI.Login.Status;

/// <summary>Where the account panel is in the OpenConquer Sign Up flow; retail opened a website instead.</summary>
internal enum LoginRegistrationStep
{
    None,
    EnterCredentials,
    ConfirmPassword,
    Submitting,
}

internal sealed record LoginRegistrationFeedback(LoginRegistrationStep Step, LoginFailure? Notice = null);
