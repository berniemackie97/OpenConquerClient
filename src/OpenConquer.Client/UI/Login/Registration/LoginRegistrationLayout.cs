using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Content.Login;

namespace OpenConquer.Client.UI.Login.Registration;

/// <summary>
/// Stage placement of the Sign Up dialog. Positions inside it are offsets into the dialog picture, which keeps
/// clearMC's header (rows 0-26), body and footer (from row 185) and its Ok and Cancel buttons.
/// </summary>
internal sealed class LoginRegistrationLayout
{
    // Field boxes start 118 columns and 40 rows into the picture, one every 34 rows; labels end 6 columns left of them.
    private const int BoxLeft = 118;
    private const int BoxTop = 40;
    private const int BoxPitch = 34;
    private const int LabelGap = 6;

    // enterMC text field 364 lies (4.15, 3.45) inside the account panel's password box and is 140.7 x 22.1.
    private const double FieldInsetX = 4.15;
    private const double FieldInsetY = 3.45;
    private const double FieldWidth = 140.7;
    private const double FieldHeight = 22.1;

    // clearMC's header band and its Ok and Cancel buttons, 96x26, at (20, 156) and (180, 156) before the extension.
    private const int HeaderHeight = 27;
    private const int ButtonTop = 156 + LoginRegistrationArtwork.BodyExtension;

    // The message sits between the last box and the footer divider.
    private const int MessageLeft = 12;
    private const int MessageTop = BoxTop + 2 * BoxPitch + LoginRegistrationArtwork.FieldHeight;
    private const int MessageBottom = LoginRegistrationArtwork.FooterTopRow;

    private readonly LoginStageLayout _stage;
    private readonly LoginRect[] _boxes = new LoginRect[3];
    private readonly LoginRect[] _fields = new LoginRect[3];
    private readonly LoginRect[] _labels = new LoginRect[3];

    public LoginRegistrationLayout(LoginStageLayout stage)
    {
        _stage = stage ?? throw new ArgumentNullException(nameof(stage));
        (int x, int y) = LoginRegistrationArtwork.DialogPosition;
        Dialog = stage.Place(x, y, LoginRegistrationArtwork.DialogWidth, LoginRegistrationArtwork.DialogHeight);
        Title = stage.Place(x, y, LoginRegistrationArtwork.DialogWidth, HeaderHeight);
        for (int index = 0; index < _boxes.Length; index++)
        {
            int top = y + BoxTop + index * BoxPitch;
            _boxes[index] = stage.Place(x + BoxLeft, top, LoginRegistrationArtwork.FieldWidth, LoginRegistrationArtwork.FieldHeight);
            _fields[index] = stage.PlaceExact(x + BoxLeft + FieldInsetX, top + FieldInsetY, FieldWidth, FieldHeight);
            _labels[index] = stage.PlaceExact(x, top + FieldInsetY, BoxLeft - LabelGap, FieldHeight);
        }

        Message = stage.Place(x + MessageLeft, y + MessageTop, LoginRegistrationArtwork.DialogWidth - 2 * MessageLeft, MessageBottom - MessageTop);
        Ok = stage.Place(x + 20, y + ButtonTop, 96, 26);
        Cancel = stage.Place(x + 180, y + ButtonTop, 96, 26);

        (int maskY, int maskHeight) = LoginAccountArtwork.MaskRows;
        Mask = stage.Place(0, maskY, 1024, maskHeight);
    }

    public LoginRect Dialog
    {
        get;
    }

    /// <summary>clearMC's header band, above its first divider.</summary>
    public LoginRect Title
    {
        get;
    }

    public LoginRect Message
    {
        get;
    }

    public LoginRect Ok
    {
        get;
    }

    public LoginRect Cancel
    {
        get;
    }

    public LoginRect Mask
    {
        get;
    }

    /// <summary>The field's box artwork, which also takes its presses.</summary>
    public LoginRect Box(LoginRegistrationField field) => _boxes[Index(field)];

    /// <summary>The field's text area, gutter included, placed as the account panel places field 364.</summary>
    public LoginRect Field(LoginRegistrationField field) => _fields[Index(field)];

    /// <summary>The area the field's label is right-aligned in, level with the field's text area.</summary>
    public LoginRect Label(LoginRegistrationField field) => _labels[Index(field)];

    public int Scale(int referenceValue) => _stage.Scale(referenceValue);

    private static int Index(LoginRegistrationField field) =>
        Enum.IsDefined(field) ? (int)field : throw new ArgumentOutOfRangeException(nameof(field));
}
