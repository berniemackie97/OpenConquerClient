using OpenConquer.Content.Images;

namespace OpenConquer.Content.Login;

/// <summary>Held pictures of start.dat errorMC (274) and quitWindow (287), including their backdrop and Up buttons.</summary>
public static class LoginErrorArtwork
{
    private static readonly EmbeddedImage s_error = new("Login/Error/Error.rgba.br", 296, 192,
        "95DB6F8321AA6E65ED8DF46FFB65E46BF684EEB8C2062B4CD0C80E3DE3E8142D");
    private static readonly EmbeddedImage s_quit = new("Login/Error/Quit.rgba.br", 296, 192,
        "60C097EF6E447B625D4153A074757F4E1ECA9066659D497F75DC58C4DEA7EEF8");

    public static RgbaImage Load(bool quit) => EmbeddedMedia.Load(quit ? s_quit : s_error);

    // Both root instances translate the exported local picture by (-1, 0).
    public static (int X, int Y) Position => (360, 259);
}
