using OpenConquer.Content.Startup;

namespace OpenConquer.Client.UI.Login;

/// <summary>Authoritative 5517 start.dat placements on the scaled 1024×768 stage.</summary>
internal sealed class LoginRetailFlashLayout
{
    private const int ReferenceWidth = 1024;
    private readonly LoginSpritePlacement[] _buttons;

    private LoginRetailFlashLayout(LoginVisualLayout layout)
    {
        Logo = Place(layout, 776, 2, 248, 78);
        _buttons =
        [
            Place(layout, 26, 713, 82, 21),
            Place(layout, 138, 713, 109, 21),
            Place(layout, 256, 713, 57, 23),
            Place(layout, 597, 646, 17, 15),
            Place(layout, 625, 647, 22, 19),
            Place(layout, 656, 646, 20, 21),
        ];
    }

    public LoginSpritePlacement Logo
    {
        get;
    }

    public LoginSpritePlacement ButtonAt(LoginRetailFlashArtwork.Button button)
    {
        if (!Enum.IsDefined(button))
        {
            throw new ArgumentOutOfRangeException(nameof(button));
        }

        return _buttons[(int)button];
    }

    public static LoginRetailFlashLayout Create(LoginVisualLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        return new LoginRetailFlashLayout(layout);
    }

    private static LoginSpritePlacement Place(LoginVisualLayout layout, int x, int y, int width, int height) =>
        new(Scale(x, layout.Width), Scale(y, layout.Width), Scale(width, layout.Width), Scale(height, layout.Width));

    private static int Scale(int value, int width) => checked((int)(((long)value * width + ReferenceWidth / 2) / ReferenceWidth));
}
