using OpenConquer.Client.UI.Login.Stage;

namespace OpenConquer.Client.UI.Login.Credentials;

internal readonly record struct LoginCredentialLayout(LoginRect AccountField, LoginRect PasswordField)
{
    public static LoginCredentialLayout Create(LoginStageLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        LoginRect panel = layout.AccountPanel;

        LoginCredentialLayout result = new(layout.PlaceExact(470.5, 643.4, 122, 22.1), layout.PlaceExact(470.15, 674.45, 140.7, 22.1));
        Validate(result.AccountField, panel);
        Validate(result.PasswordField, panel);

        return (long)result.AccountField.Y + result.AccountField.Height >= result.PasswordField.Y
            ? throw new InvalidOperationException("The retail credential fields overlap.")
            : result;
    }

    private static void Validate(LoginRect field, LoginRect panel)
    {
        if (field.Width <= 0 || field.Height <= 0 || field.X < panel.X || field.Y < panel.Y ||
            (long)field.X + field.Width > (long)panel.X + panel.Width ||
            (long)field.Y + field.Height > (long)panel.Y + panel.Height)
        {
            throw new InvalidOperationException("Credential input extends outside the retail account panel.");
        }
    }
}
