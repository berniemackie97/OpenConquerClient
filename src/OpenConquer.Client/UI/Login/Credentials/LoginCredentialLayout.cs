using OpenConquer.Client.UI.Login.Stage;

namespace OpenConquer.Client.UI.Login.Credentials;

/// <summary>Edit-text boxes of the retail account panel (enterMC fields 363 and 364), gutter included.</summary>
internal readonly record struct LoginCredentialLayout(LoginRect AccountField, LoginRect PasswordField)
{
    public static LoginCredentialLayout Create(LoginStageLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        LoginRect panel = layout.AccountPanel;

        // enterMC (512.5, 720.4): userNameTxt at (-40, -75), bounds (-2,-2)-(120,20.1);
        // passwordTxt at (-40.35, -43.95), x-scale 1.000351, bounds (-2,-2)-(138.65,20.1).
        LoginCredentialLayout result = new(layout.PlaceExact(470.5, 643.4, 122, 22.1), layout.PlaceExact(470.15, 674.45, 140.7, 22.1));
        Validate(result.AccountField, panel);
        Validate(result.PasswordField, panel);

        if ((long)result.AccountField.Y + result.AccountField.Height >= result.PasswordField.Y)
        {
            throw new InvalidOperationException("The retail credential fields overlap.");
        }

        return result;
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
