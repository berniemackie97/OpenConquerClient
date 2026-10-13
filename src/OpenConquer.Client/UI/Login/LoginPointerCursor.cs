using OpenConquer.Platform.Windowing.Desktop;

namespace OpenConquer.Client.UI.Login;

/// <summary>Maps current, authenticated login hit targets to desktop pointer affordances.</summary>
internal sealed class LoginPointerCursor
{
    private readonly LoginVisualLayout _layout;
    private readonly LoginActionLayout _actions;
    private readonly LoginCredentialLayout _credentials;
    private readonly LoginPageControls _pages;

    public LoginPointerCursor(LoginVisualLayout layout)
    {
        _layout = layout ?? throw new ArgumentNullException(nameof(layout));
        _actions = LoginActionLayout.Create(layout);
        _credentials = LoginCredentialLayout.Create(layout);
        _pages = LoginPageControls.Create(layout);
    }

    public DesktopPointerCursor Resolve(bool mapped, int x, int y, LoginServerSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);

        if (!mapped || x < 0 || y < 0 || x >= _layout.Width || y >= _layout.Height)
        {
            return DesktopPointerCursor.Arrow;
        }

        // Enter has intentionally not been connected to authentication. It must never look clickable.
        LoginActionButton action = _actions.Hit(x, y);
        if (action != LoginActionButton.None)
        {
            return action == LoginActionButton.Exit ? DesktopPointerCursor.Hand : DesktopPointerCursor.Arrow;
        }

        if (selection.SelectedServer is not null &&
            (Contains(_credentials.AccountField, x, y) || Contains(_credentials.PasswordField, x, y)))
        {
            return DesktopPointerCursor.Text;
        }

        if (_pages.TryHit(x, y, out LoginPageControl page))
        {
            return LoginPageControls.IsEnabled(page, selection) ? DesktopPointerCursor.Hand : DesktopPointerCursor.Arrow;
        }

        ReadOnlySpan<LoginSpritePlacement> regions = _layout.RegionCells;
        for (int slot = 0; slot < regions.Length; slot++)
        {
            if (Contains(regions[slot], x, y))
            {
                return selection.RegionAtSlot(slot) is not null ? DesktopPointerCursor.Hand : DesktopPointerCursor.Arrow;
            }
        }

        ReadOnlySpan<LoginSpritePlacement> servers = _layout.ServerCells;
        for (int slot = 0; slot < servers.Length; slot++)
        {
            if (Contains(servers[slot], x, y))
            {
                return selection.ServerAtSlot(slot) is not null ? DesktopPointerCursor.Hand : DesktopPointerCursor.Arrow;
            }
        }

        return DesktopPointerCursor.Arrow;
    }

    private static bool Contains(LoginSpritePlacement rectangle, int x, int y) =>
        x >= rectangle.X && y >= rectangle.Y &&
        (long)x < (long)rectangle.X + rectangle.Width && (long)y < (long)rectangle.Y + rectangle.Height;
}
