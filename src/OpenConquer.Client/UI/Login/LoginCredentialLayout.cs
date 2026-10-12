namespace OpenConquer.Client.UI.Login;

/// <summary>Hit regions inside the original 508x98 retail account-panel image.</summary>
internal readonly record struct LoginCredentialLayout(LoginSpritePlacement AccountField, LoginSpritePlacement PasswordField)
{
    public static LoginCredentialLayout Create(LoginVisualLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        LoginSpritePlacement panel = layout.AccountPanel;

        // Source: start.dat DefineBitsJPEG3 character 327. The top field's right-hand inset is
        // part of the retail artwork and must not be covered by user-entered text.
        LoginCredentialLayout result = new(Field(panel, 214, 38, 120, 21), Field(panel, 214, 69, 142, 19));
        Validate(result.AccountField, panel);
        Validate(result.PasswordField, panel);

        if ((long)result.AccountField.Y + result.AccountField.Height >= result.PasswordField.Y)
        {
            throw new InvalidOperationException("The retail credential fields overlap.");
        }

        return result;
    }

    private static LoginSpritePlacement Field(LoginSpritePlacement panel, int x, int y, int width, int height) => new(
        checked(panel.X + Scale(x, panel.Width, 508)), checked(panel.Y + Scale(y, panel.Height, 98)),
        Scale(width, panel.Width, 508), Scale(height, panel.Height, 98));

    private static void Validate(LoginSpritePlacement field, LoginSpritePlacement panel)
    {
        if (field.Width <= 0 || field.Height <= 0 || field.X < panel.X || field.Y < panel.Y ||
            (long)field.X + field.Width > (long)panel.X + panel.Width ||
            (long)field.Y + field.Height > (long)panel.Y + panel.Height)
        {
            throw new InvalidOperationException("Credential input extends outside the retail account panel.");
        }
    }

    private static int Scale(int value, int destination, int reference) => checked((int)(((long)value * destination + reference / 2) / reference));
}
