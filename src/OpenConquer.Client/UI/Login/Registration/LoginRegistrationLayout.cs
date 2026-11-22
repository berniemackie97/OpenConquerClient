using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Content.Login;

namespace OpenConquer.Client.UI.Login.Registration;

internal sealed class LoginRegistrationLayout
{
    private const int BoxLeft = 118;
    private const int BoxTop = 40;
    private const int BoxPitch = 34;
    private const int LabelGap = 6;

    private const double FieldInsetX = 4.15;
    private const double FieldInsetY = 3.45;
    private const double FieldWidth = 140.7;
    private const double FieldHeight = 22.1;

    private const int HeaderHeight = 27;
    private const int ButtonTop = 156 + LoginRegistrationArtwork.BodyExtension;

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

    public LoginRect Box(LoginRegistrationField field)
    {
        return _boxes[Index(field)];
    }

    public LoginRect Field(LoginRegistrationField field)
    {
        return _fields[Index(field)];
    }

    public LoginRect Label(LoginRegistrationField field)
    {
        return _labels[Index(field)];
    }

    public int Scale(int referenceValue)
    {
        return _stage.Scale(referenceValue);
    }

    private static int Index(LoginRegistrationField field)
    {
        return Enum.IsDefined(field) ? (int)field : throw new ArgumentOutOfRangeException(nameof(field));
    }
}
