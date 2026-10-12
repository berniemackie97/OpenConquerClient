namespace OpenConquer.Client.UI.Login;

internal enum LoginActionButton
{
    None,
    Enter,
    Exit,
}

/// <summary>First-party placement for verified native action-button sprites on the 1024x768 login stage.</summary>
internal readonly record struct LoginActionLayout(LoginSpritePlacement Enter, LoginSpritePlacement Exit)
{
    public static LoginActionLayout Create(LoginVisualLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        LoginActionLayout result = new(Place(layout, 654, 726), Place(layout, 862, 726));
        Validate(result.Enter, layout);
        Validate(result.Exit, layout);

        LoginCredentialLayout credentials = LoginCredentialLayout.Create(layout);
        if (Overlaps(result.Enter, result.Exit) ||
            Overlaps(result.Enter, credentials.AccountField) || Overlaps(result.Enter, credentials.PasswordField) ||
            Overlaps(result.Exit, credentials.AccountField) || Overlaps(result.Exit, credentials.PasswordField))
        {
            throw new InvalidOperationException("Login actions overlap a credential field or each other.");
        }

        return result;
    }

    public LoginActionButton Hit(int x, int y) => Contains(Enter, x, y) ? LoginActionButton.Enter : Contains(Exit, x, y) ? LoginActionButton.Exit : LoginActionButton.None;

    private static LoginSpritePlacement Place(LoginVisualLayout layout, int x, int y) => new(
        Scale(x, layout.Width), Scale(y, layout.Width), Scale(124, layout.Width), Scale(34, layout.Width));

    private static void Validate(LoginSpritePlacement bounds, LoginVisualLayout layout)
    {
        if (bounds.X < 0 || bounds.Y < 0 || bounds.Width <= 0 || bounds.Height <= 0 ||
            (long)bounds.X + bounds.Width > layout.Width || (long)bounds.Y + bounds.Height > layout.Height)
        {
            throw new InvalidOperationException("Login action artwork must fit inside the logical stage.");
        }
    }

    private static bool Overlaps(LoginSpritePlacement left, LoginSpritePlacement right) =>
        (long)left.X < (long)right.X + right.Width && (long)right.X < (long)left.X + left.Width &&
        (long)left.Y < (long)right.Y + right.Height && (long)right.Y < (long)left.Y + left.Height;

    private static bool Contains(LoginSpritePlacement bounds, int x, int y) =>
        x >= bounds.X && y >= bounds.Y && (long)x < (long)bounds.X + bounds.Width && (long)y < (long)bounds.Y + bounds.Height;

    private static int Scale(int value, int logicalWidth) => checked((int)(((long)value * logicalWidth + 512) / 1024));
}
