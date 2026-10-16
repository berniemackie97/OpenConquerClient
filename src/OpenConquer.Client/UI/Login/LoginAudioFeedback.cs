using OpenConquer.Content.Startup;

namespace OpenConquer.Client.UI.Login;

/// <summary>Deterministic mapping of actual login hit regions to retail flashLogin.ini sound events.</summary>
internal sealed class LoginAudioFeedback
{
    private enum HoverArea
    {
        None, Region, Server, Page, Account, Password, Enter, Exit
    }
    private readonly LoginVisualLayout _layout;
    private readonly LoginPageControls _pages;
    private readonly LoginActionLayout _actions;
    private readonly LoginCredentialLayout _fields;
    private HoverArea _hover;
    private int _slot = -1;
    private int _typedKeys;

    public LoginAudioFeedback(LoginVisualLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        _layout = layout;
        _pages = LoginPageControls.Create(layout);
        _actions = LoginActionLayout.Create(layout);
        _fields = LoginCredentialLayout.Create(layout);
    }

    public LoginAudioCue? PointerMoved(bool mapped, int x, int y, LoginServerSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        (HoverArea area, int slot) = Hit(mapped, x, y, selection);
        if (_hover == area && _slot == slot)
        {
            return null;
        }
        _hover = area;
        _slot = slot;
        return area switch
        {
            HoverArea.Region or HoverArea.Server => LoginAudioCue.Glide,
            HoverArea.Page or HoverArea.Account or HoverArea.Password or HoverArea.Enter or HoverArea.Exit => LoginAudioCue.ButtonHover,
            _ => null,
        };
    }

    public LoginAudioCue AcceptedCredentialEdit()
    {
        LoginAudioCue cue = _typedKeys switch
        {
            0 => LoginAudioCue.KeyOne,
            1 => LoginAudioCue.KeyTwo,
            _ => LoginAudioCue.KeyThree,
        };
        _typedKeys = (_typedKeys + 1) % 3;
        return cue;
    }

    public void Reset()
    {
        _hover = HoverArea.None;
        _slot = -1;
    }

    private (HoverArea Area, int Slot) Hit(bool mapped, int x, int y, LoginServerSelection selection)
    {
        if (!mapped)
        {
            return (HoverArea.None, -1);
        }
        if (_actions.Hit(x, y) == LoginActionButton.Exit)
        {
            return (HoverArea.Exit, 0);
        }
        if (_actions.Hit(x, y) == LoginActionButton.Enter)
        {
            return (HoverArea.Enter, 0);
        }
        if (Contains(_fields.AccountField, x, y))
        {
            return (HoverArea.Account, 0);
        }

        if (Contains(_fields.PasswordField, x, y))
        {
            return (HoverArea.Password, 0);
        }

        if (_pages.TryHit(x, y, out LoginPageControl page) && LoginPageControls.IsEnabled(page, selection))
        {
            return (HoverArea.Page, (int)page);
        }
        ReadOnlySpan<LoginSpritePlacement> regions = _layout.RegionCells;
        for (int slot = 0; slot < regions.Length; slot++)
        {
            if (selection.RegionAtSlot(slot) is not null && Contains(regions[slot], x, y))
            {
                return (HoverArea.Region, slot);
            }
        }
        ReadOnlySpan<LoginSpritePlacement> servers = _layout.ServerCells;
        for (int slot = 0; slot < servers.Length; slot++)
        {
            if (selection.ServerAtSlot(slot) is not null && Contains(servers[slot], x, y))
            {
                return (HoverArea.Server, slot);
            }
        }
        return (HoverArea.None, -1);
    }

    private static bool Contains(LoginSpritePlacement p, int x, int y)
    {
        return x >= p.X && y >= p.Y && (long)x < (long)p.X + p.Width && (long)y < (long)p.Y + p.Height;
    }
}
