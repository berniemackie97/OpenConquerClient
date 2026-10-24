namespace OpenConquer.Client.UI.Login.Status;

/// <summary>Text of the OpenConquer Sign Up dialog and its results.</summary>
internal enum LoginRegistrationNotice
{
    /// <summary>The account rules, shown while the dialog waits for input.</summary>
    Guidance = 1,
    Submitting,
    InvalidAccountName,
    InvalidPassword,
    PasswordMismatch,
    ServerNotSelected,
    Created,
    AccountNameTaken,
    Rejected,
    Unavailable,
    Failed,
    /// <summary>The request may have reached the server, but no result arrived.</summary>
    OutcomeUnknown,
}

/// <summary>
/// Project-owned English text of the Sign Up dialog. No retail resource exists for in-client registration, so
/// none of it is native; native resources are still used for connection failure.
/// </summary>
internal static class LoginRegistrationText
{
    internal const string Title = "Create Account";

    // The account panel's own labels are "Account :" and "Password :".
    internal const string AccountLabel = "Account :";
    internal const string PasswordLabel = "Password :";
    internal const string ConfirmLabel = "Confirm :";

    internal static string For(LoginRegistrationNotice notice) => notice switch
    {
        LoginRegistrationNotice.Guidance => "Account name: 4 to 16 characters.\nPassword: 6 to 14 characters.",
        LoginRegistrationNotice.Submitting => "Creating your account...",
        LoginRegistrationNotice.InvalidAccountName => "The account name must be 4 to 16 characters.",
        LoginRegistrationNotice.InvalidPassword => "The password must be 6 to 14 characters.",
        LoginRegistrationNotice.PasswordMismatch => "The passwords do not match.",
        LoginRegistrationNotice.ServerNotSelected => "Select a server before signing up.",
        LoginRegistrationNotice.Created => "Your account has been created. Enter your password to log in.",
        LoginRegistrationNotice.AccountNameTaken => "That account name is already taken.",
        LoginRegistrationNotice.Rejected => "That account name or password cannot be used.",
        LoginRegistrationNotice.Unavailable => "Sign up is unavailable right now. Please try again later.",
        LoginRegistrationNotice.Failed => "Your account could not be created. Please try again later.",
        LoginRegistrationNotice.OutcomeUnknown => "The server did not answer. The account may exist, so try logging in first.",
        _ => throw new ArgumentOutOfRangeException(nameof(notice), notice, null),
    };
}
