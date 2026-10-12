namespace OpenConquer.Client.UI.Login;

internal enum LoginPageControl
{
    None,
    RegionPrevious,
    RegionNext,
    ServerPrevious,
    ServerNext,
}

/// <summary>First-party pointer affordances for the existing bounded login selection pages.</summary>
internal readonly record struct LoginPageControls(
    LoginSpritePlacement RegionPrevious,
    LoginSpritePlacement RegionNext,
    LoginSpritePlacement ServerPrevious,
    LoginSpritePlacement ServerNext)
{
    public static LoginPageControls Create(LoginVisualLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        ReadOnlySpan<LoginSpritePlacement> regions = layout.RegionCells;
        ReadOnlySpan<LoginSpritePlacement> servers = layout.ServerCells;
        int size = Math.Max(1, Scale(24, layout.Width));
        int regionX = checked(regions[0].X - size - Scale(7, layout.Width));
        int serverX = checked(servers[2].X + servers[2].Width + Scale(10, layout.Width));
        LoginPageControls controls = new(
            new LoginSpritePlacement(regionX, regions[0].Y, size, size),
            new LoginSpritePlacement(regionX, checked(regions[^1].Y + regions[^1].Height - size), size, size),
            new LoginSpritePlacement(serverX, servers[0].Y, size, size),
            new LoginSpritePlacement(serverX, checked(servers[^1].Y + servers[^1].Height - size), size, size));

        ValidateBounds(controls.RegionPrevious, layout);
        ValidateBounds(controls.RegionNext, layout);
        ValidateBounds(controls.ServerPrevious, layout);
        ValidateBounds(controls.ServerNext, layout);

        return controls;
    }

    public LoginSpritePlacement Bounds(LoginPageControl control) => control switch
    {
        LoginPageControl.RegionPrevious => RegionPrevious,
        LoginPageControl.RegionNext => RegionNext,
        LoginPageControl.ServerPrevious => ServerPrevious,
        LoginPageControl.ServerNext => ServerNext,
        _ => throw new ArgumentOutOfRangeException(nameof(control), control, "Unsupported page control."),
    };

    public static bool IsVisible(LoginPageControl control, LoginServerSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        return control switch
        {
            LoginPageControl.RegionPrevious or LoginPageControl.RegionNext => selection.RegionPageCount > 1,
            LoginPageControl.ServerPrevious or LoginPageControl.ServerNext => selection.ServerPageCount > 1,
            _ => false,
        };
    }

    public static bool IsEnabled(LoginPageControl control, LoginServerSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        return control switch
        {
            LoginPageControl.RegionPrevious => selection.RegionPage > 0,
            LoginPageControl.RegionNext => selection.RegionPage + 1 < selection.RegionPageCount,
            LoginPageControl.ServerPrevious => selection.SelectedRegion is not null && selection.ServerPage > 0,
            LoginPageControl.ServerNext => selection.SelectedRegion is not null && selection.ServerPage + 1 < selection.ServerPageCount,
            _ => false,
        };
    }

    public bool TryHit(int x, int y, out LoginPageControl control)
    {
        if (Contains(RegionPrevious, x, y))
        {
            control = LoginPageControl.RegionPrevious;
        }
        else if (Contains(RegionNext, x, y))
        {
            control = LoginPageControl.RegionNext;
        }
        else if (Contains(ServerPrevious, x, y))
        {
            control = LoginPageControl.ServerPrevious;
        }
        else if (Contains(ServerNext, x, y))
        {
            control = LoginPageControl.ServerNext;
        }
        else
        {
            control = LoginPageControl.None;
            return false;
        }

        return true;
    }

    private static void ValidateBounds(LoginSpritePlacement rectangle, LoginVisualLayout layout)
    {
        if (rectangle.X < 0 || rectangle.Y < 0 || rectangle.Width <= 0 || rectangle.Height <= 0 ||
            (long)rectangle.X + rectangle.Width > layout.Width || (long)rectangle.Y + rectangle.Height > layout.Height)
        {
            throw new InvalidOperationException("Login page controls do not fit within the logical stage.");
        }
    }

    private static bool Contains(LoginSpritePlacement rect, int x, int y) =>
        x >= rect.X && y >= rect.Y && (long)x < (long)rect.X + rect.Width && (long)y < (long)rect.Y + rect.Height;

    private static int Scale(int referenceValue, int logicalWidth) =>
        checked((int)(((long)referenceValue * logicalWidth + 512) / 1024));
}
