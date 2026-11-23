using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Content.Login;

namespace OpenConquer.Client.UI.Login.Account;

internal sealed class LoginAccountLayout
{
    private const int ListX = 452;
    private const int ListBottom = 631;
    public const int RowWidth = 166;
    public const int RowHeight = 25;

    private readonly LoginStageLayout _stage;

    public LoginAccountLayout(LoginStageLayout stage)
    {
        _stage = stage ?? throw new ArgumentNullException(nameof(stage));

        Ok = stage.Place(381, 415, 96, 26);
        Cancel = stage.Place(541, 415, 96, 26);
        ClearMessage = stage.Place(365, 293, 293, 110);

        (int maskY, int maskHeight) = LoginAccountArtwork.MaskRows;
        Mask = stage.Place(0, maskY, 1024, maskHeight);
    }

    public LoginRect Ok
    {
        get;
    }

    public LoginRect Cancel
    {
        get;
    }

    public LoginRect ClearMessage
    {
        get;
    }

    public LoginRect Mask
    {
        get;
    }

    public int Scale(int referenceValue)
    {
        return _stage.Scale(referenceValue);
    }

    public LoginRect ListClip(int rowCount)
    {
        return _stage.Place(ListX, ListBottom - RowHeight * rowCount, RowWidth, RowHeight * rowCount);
    }

    public LoginRect Row(int row, int offset)
    {
        return _stage.Place(ListX, ListBottom + offset + RowHeight * row, RowWidth, RowHeight);
    }

    public LoginRect Dialog(LoginAccountDialog dialog, int width, int height)
    {
        (int x, int y) = LoginAccountArtwork.DialogPosition(dialog);
        return _stage.Place(x, y, width, height);
    }
}
