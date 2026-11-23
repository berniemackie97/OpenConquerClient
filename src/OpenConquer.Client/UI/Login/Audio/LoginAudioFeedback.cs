using OpenConquer.Client.UI.Login.Credentials;
using OpenConquer.Client.UI.Login.Selection;
using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Content.Login;

namespace OpenConquer.Client.UI.Login.Audio;

internal sealed class LoginAudioFeedback(LoginStageLayout layout)
{
    private enum Area
    {
        None,
        Region,
        Server,
        Account,
        Password,
    }

    private static readonly LoginAudioCue[] s_keySounds = [LoginAudioCue.KeyOne, LoginAudioCue.KeyTwo, LoginAudioCue.KeyThree];

    private readonly LoginStageLayout _layout = layout ?? throw new ArgumentNullException(nameof(layout));
    private readonly LoginCredentialLayout _fields = LoginCredentialLayout.Create(layout);
    private Area _area;
    private int _slot = -1;
    private int _keySound;

    public LoginAudioCue? PointerMoved(bool mapped, int x, int y, LoginServerSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);

        (Area area, int slot) = mapped ? Hit(x, y, selection) : (Area.None, -1);
        if (area == _area && slot == _slot)
        {
            return null;
        }

        _area = area;
        _slot = slot;
        return area switch
        {
            Area.Region or Area.Server => LoginAudioCue.Glide,
            Area.Account or Area.Password => LoginAudioCue.ButtonHover,
            _ => null,
        };
    }

    public LoginAudioCue NextKeySound()
    {
        LoginAudioCue cue = s_keySounds[_keySound];
        _keySound = (_keySound + 1) % s_keySounds.Length;
        return cue;
    }

    public void Reset()
    {
        _area = Area.None;
        _slot = -1;
    }

    private (Area Area, int Slot) Hit(int x, int y, LoginServerSelection selection)
    {
        if (_fields.AccountField.Contains(x, y))
        {
            return (Area.Account, 0);
        }

        if (_fields.PasswordField.Contains(x, y))
        {
            return (Area.Password, 0);
        }

        int region = LoginStageLayout.HitSlot(_layout.RegionHitAreas, x, y);
        if (region >= 0 && region != selection.SelectedRegionSlot && selection.RegionAtSlot(region) is not null)
        {
            return (Area.Region, region);
        }

        int server = LoginStageLayout.HitSlot(_layout.ServerHitAreas, x, y);
        if (server >= 0 && selection.ServerAtSlot(server) is not null)
        {
            return (Area.Server, server);
        }

        return (Area.None, -1);
    }
}
