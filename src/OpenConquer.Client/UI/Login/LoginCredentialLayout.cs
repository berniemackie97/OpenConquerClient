namespace OpenConquer.Client.UI.Login;

/// <summary>Client-owned input placements within the previously verified account-panel bounds.</summary>
internal readonly record struct LoginCredentialLayout(LoginSpritePlacement AccountField, LoginSpritePlacement PasswordField)
{
    public static LoginCredentialLayout Create(LoginVisualLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        LoginSpritePlacement panel = layout.AccountPanel;

        return new LoginCredentialLayout(Field(panel, 12), Field(panel, 44));
    }

    private static LoginSpritePlacement Field(LoginSpritePlacement panel, int referenceOffsetY)
    {
        int x = checked(panel.X + Scale(122, panel.Width, 508));
        int y = checked(panel.Y + Scale(referenceOffsetY, panel.Height, 98));
        int width = Scale(230, panel.Width, 508);
        int height = Scale(23, panel.Height, 98);
        return new LoginSpritePlacement(x, y, width, height);
    }

    private static int Scale(int value, int destination, int reference) => checked((int)(((long)value * destination + reference / 2) / reference));
}
