using Avalonia;

namespace OpenConquer.Launcher.Play;

/// <summary>
/// Positions from the retail <c>ini/PlayGui.ini</c>, in dialog pixels. Every skin control takes the size of its
/// normal bitmap, so only positions are listed.
/// </summary>
internal static class PlayLayout
{
    /// <summary>Section <c>0-102</c> sizes the dialog 900x650, but <c>Play/Main.rgn</c> keeps only the 800x600 art.</summary>
    public static readonly Size Window = new(800, 600);

    /// <summary><c>CenterWindow</c> centres the 900x650 dialog, so the art sits up and left of the screen centre.</summary>
    public static readonly Size CenteredDialog = new(900, 650);

    /// <summary>Foreground picture 1, <c>Play/MainS.bmp</c>.</summary>
    public static readonly Point Side = new(594, 0);

    /// <summary>Foreground picture 2, <c>Play/Version.bmp</c>.</summary>
    public static readonly Point VersionLabel = new(35, 29);

    /// <summary>Control 1012: the version text, drawn from its top left (<c>TextRect=0,0,...</c>).</summary>
    public static readonly Point VersionText = new(110, 33);

    /// <summary>Control 1005.</summary>
    public static readonly Point Logo = new(385, -1);

    /// <summary>Control 1000, the web browser, clipped by <c>Play/Center.rgn</c> (one 546x410 rectangle).</summary>
    public static readonly Rect WebPanel = new(40, 106, 546, 405);

    public static readonly Point High = new(580, 392);
    public static readonly Point Standard = new(585, 443);
    public static readonly Point Low = new(580, 520);
    public static readonly Point Exit = new(160, 541);
    public static readonly Point Site = new(273, 541);
    public static readonly Point Register = new(385, 541);

    /// <summary>Control 1009, <c>Play/Hint.bmp</c>; its text starts at <c>TextRect=20,20</c>.</summary>
    public static readonly Point Hint = new(160, 200);
    public static readonly Point HintText = new(20, 20);
    public static readonly Point Continue = new(220, 350);
    public static readonly Point Return = new(380, 350);

    public static Point Of(PlayButton button) => button switch
    {
        PlayButton.High => High,
        PlayButton.Standard => Standard,
        PlayButton.Low => Low,
        PlayButton.Continue => Continue,
        PlayButton.Return => Return,
        PlayButton.Exit => Exit,
        PlayButton.Site => Site,
        PlayButton.Register => Register,
        _ => throw new ArgumentOutOfRangeException(nameof(button), button, "Unknown play button."),
    };
}
