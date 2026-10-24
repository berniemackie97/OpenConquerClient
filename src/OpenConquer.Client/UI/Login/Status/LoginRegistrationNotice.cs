namespace OpenConquer.Client.UI.Login.Status;

/// <summary>Messages of the OpenConquer Sign Up flow, shown in the retail errorMC dialog.</summary>
internal enum LoginRegistrationNotice
{
    Begin = 1,
    ConfirmPassword,
    PasswordMismatch,
    InvalidAccountName,
    InvalidPassword,
    Created,
    AccountNameTaken,
    Rejected,
    Unavailable,
    Failed,
}

/// <summary>
/// Project-owned English text for <see cref="LoginRegistrationNotice"/>. No retail resource exists for in-client
/// registration, so these strings are not native; native resources are still used for empty fields and
/// connection failures.
/// </summary>
internal static class LoginRegistrationText
{
    internal static string For(LoginRegistrationNotice notice) => notice switch
    {
        LoginRegistrationNotice.Begin => "Enter a new account name and password, then press Enter.",
        LoginRegistrationNotice.ConfirmPassword => "Please enter the password again to confirm it.",
        LoginRegistrationNotice.PasswordMismatch => "The passwords did not match. Please try again.",
        LoginRegistrationNotice.InvalidAccountName => "The account name must be 4-16 characters.",
        LoginRegistrationNotice.InvalidPassword => "The password must be 6-14 characters.",
        LoginRegistrationNotice.Created => "Your account has been created. Enter your password to log in.",
        LoginRegistrationNotice.AccountNameTaken => "That account name is already in use.",
        LoginRegistrationNotice.Rejected => "That account name or password cannot be used.",
        LoginRegistrationNotice.Unavailable => "Account registration is unavailable. Please try again later.",
        LoginRegistrationNotice.Failed => "Your account could not be created. Please try again later.",
        _ => throw new ArgumentOutOfRangeException(nameof(notice), notice, null),
    };
}
