using OpenConquer.Client.UI.Login.Stage;
using OpenConquer.Content.Login;

namespace OpenConquer.Client.UI.Login.Controls;

/// <summary>Stage placement of the buttons inside <c>enterMC</c> (root depth 221 at (512.5, 720.4)).</summary>
internal static class LoginControlLayout
{
    private static readonly (LoginButton Button, int X, int Y, int Width, int Height)[] s_placements =
    [
        (LoginButton.Register, 26, 713, 82, 21),
        (LoginButton.Purchase, 138, 713, 109, 21),
        (LoginButton.Help, 256, 713, 57, 23),
        (LoginButton.Accounts, 597, 646, 17, 15),
        (LoginButton.Save, 625, 647, 22, 19),
        (LoginButton.Clear, 656, 646, 20, 21),
        (LoginButton.Enter, 650, 704, 132, 40),
        (LoginButton.Exit, 859, 704, 132, 40),
    ];

    public static LoginButtonSet<LoginButton> CreateButtons(LoginStageLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        return new LoginButtonSet<LoginButton>(s_placements.Select(placement =>
            (placement.Button, layout.Place(placement.X, placement.Y, placement.Width, placement.Height))));
    }

    public static IEnumerable<LoginButton> Buttons => s_placements.Select(static placement => placement.Button);
}
