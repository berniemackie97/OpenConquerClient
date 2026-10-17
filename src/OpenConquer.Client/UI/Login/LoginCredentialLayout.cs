namespace OpenConquer.Client.UI.Login;

/// <summary>Edit-text boxes of the retail account panel (enterMC fields 363 and 364), gutter included.</summary>
internal readonly record struct LoginCredentialLayout(LoginSpritePlacement AccountField, LoginSpritePlacement PasswordField)
{
    public static LoginCredentialLayout Create(LoginVisualLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        LoginSpritePlacement panel = layout.AccountPanel;

        // enterMC (512.5, 720.4): userNameTxt at (-40, -75), bounds (-2,-2)-(120,20.1);
        // passwordTxt at (-40.35, -43.95), x-scale 1.000351, bounds (-2,-2)-(138.65,20.1).
        LoginCredentialLayout result = new(Field(layout, 470.5, 643.4, 122, 22.1), Field(layout, 470.15, 674.45, 140.7, 22.1));
        Validate(result.AccountField, panel);
        Validate(result.PasswordField, panel);

        if ((long)result.AccountField.Y + result.AccountField.Height >= result.PasswordField.Y)
        {
            throw new InvalidOperationException("The retail credential fields overlap.");
        }

        return result;
    }

    private static LoginSpritePlacement Field(LoginVisualLayout layout, double x, double y, double width, double height)
    {
        int left = Round(x, layout.Width);
        int top = Round(y, layout.Width);
        return new LoginSpritePlacement(left, top, Round(x + width, layout.Width) - left, Round(y + height, layout.Width) - top);
    }

    private static void Validate(LoginSpritePlacement field, LoginSpritePlacement panel)
    {
        if (field.Width <= 0 || field.Height <= 0 || field.X < panel.X || field.Y < panel.Y ||
            (long)field.X + field.Width > (long)panel.X + panel.Width ||
            (long)field.Y + field.Height > (long)panel.Y + panel.Height)
        {
            throw new InvalidOperationException("Credential input extends outside the retail account panel.");
        }
    }

    private static int Round(double referenceValue, int logicalWidth) => checked((int)Math.Floor(referenceValue * logicalWidth / 1024 + 0.5));
}
