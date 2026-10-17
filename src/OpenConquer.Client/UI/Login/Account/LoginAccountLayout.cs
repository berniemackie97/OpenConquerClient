using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Content.Login;

namespace OpenConquer.Client.UI.Login.Account;

/// <summary>Stage placement of the saved-account list and the confirmation dialogs.</summary>
internal sealed class LoginAccountLayout
{
    // enterMC sprite 360 listMC at (451.5, 631.4): rows are itemBtn sprite 6, 166x25, stacked upward from here.
    private const int ListX = 452;
    private const int ListBottom = 631;
    public const int RowWidth = 166;
    public const int RowHeight = 25;

    private readonly LoginStageLayout _stage;

    public LoginAccountLayout(LoginStageLayout stage)
    {
        _stage = stage ?? throw new ArgumentNullException(nameof(stage));

        // saveMC/clearMC sit at (-1, 0); okBtn and cancelBtn at (430, 428) and (590, 428), 96x26 centred.
        Ok = stage.Place(381, 415, 96, 26);
        Cancel = stage.Place(541, 415, 96, 26);
        // clearMC frame 6 edit text 284: the account name, Arial 14, #FF4642.
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

    public int Scale(int referenceValue) => _stage.Scale(referenceValue);

    /// <summary>The list's visible area while <paramref name="rowCount"/> rows are shown above its anchor.</summary>
    public LoginRect ListClip(int rowCount) => _stage.Place(ListX, ListBottom - RowHeight * rowCount, RowWidth, RowHeight * rowCount);

    /// <summary>A row while the list is <paramref name="offset"/> pixels into its slide.</summary>
    public LoginRect Row(int row, int offset) => _stage.Place(ListX, ListBottom + offset + RowHeight * row, RowWidth, RowHeight);

    public LoginRect Dialog(LoginAccountDialog dialog, int width, int height)
    {
        (int x, int y) = LoginAccountArtwork.DialogPosition(dialog);
        return _stage.Place(x, y, width, height);
    }
}
