using OpenConquer.Client.UI.Login.Credentials;

namespace OpenConquer.Client.UI.Login.Registration;

/// <summary>
/// The client's account rules for Sign Up. OpenConquerServer applies its own policy (name 4-16 printable ASCII
/// without spaces, password 6-15 in the keypad alphabet); these rules are narrower so every account they allow
/// can also log in.
/// </summary>
internal static class LoginRegistrationPolicy
{
    internal const int MinimumAccountLength = 4;
    internal const int MaximumAccountLength = 16;
    internal const int MinimumPasswordLength = 6;

    // The English client refuses passwords over 14 characters at login (StrRes 11400), so a longer one would
    // create an account it cannot sign in to.
    internal const int MaximumPasswordLength = 14;

    internal static bool IsValidAccount(ReadOnlySpan<char> name) =>
        name.Length is >= MinimumAccountLength and <= MaximumAccountLength && IsKeypadText(name);

    internal static bool IsValidPassword(ReadOnlySpan<char> password) =>
        password.Length is >= MinimumPasswordLength and <= MaximumPasswordLength && IsKeypadText(password);

    private static bool IsKeypadText(ReadOnlySpan<char> text)
    {
        foreach (char character in text)
        {
            if (!LoginTextField.Accepts(character))
            {
                return false;
            }
        }

        return true;
    }
}
