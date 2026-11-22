using OpenConquer.Client.UI.Login.Credentials;

namespace OpenConquer.Client.UI.Login.Registration;

internal static class LoginRegistrationPolicy
{
    internal const int MinimumAccountLength = 4;
    internal const int MaximumAccountLength = 16;
    internal const int MinimumPasswordLength = 6;
    internal const int MaximumPasswordLength = 14;

    internal static bool IsValidAccount(ReadOnlySpan<char> name)
    {
        return name.Length is >= MinimumAccountLength and <= MaximumAccountLength && IsKeypadText(name);
    }

    internal static bool IsValidPassword(ReadOnlySpan<char> password)
    {
        return password.Length is >= MinimumPasswordLength and <= MaximumPasswordLength && IsKeypadText(password);
    }

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
