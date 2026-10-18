namespace OpenConquer.Launcher.Play;

/// <summary>The retail <c>ini/Play.dat [Str]</c> strings, with <c>\32</c> and <c>\n</c> already unescaped.</summary>
internal static class PlayText
{
    /// <summary><c>STR_PLAY_HIGH_TIP</c>.</summary>
    public const string HighTip = "If your graphics card supports 3D Anti-aliasing, this setting will be your best option.";

    /// <summary><c>STR_PLAY_NORMAL_TIP</c>.</summary>
    public const string StandardTip = "The game will run with the standard graphics settings.";

    /// <summary><c>STR_PLAY_LOW_TIP</c>.</summary>
    public const string LowTip = "The game will run smoothly in this mode, but most of the visual effects in the game will not be displayed. Dynamic reduction will reduce the 3D quality.";

    /// <summary><c>STR_PLAY_SIMPILYCONFIRM</c>, drawn one line per <c>\n</c>.</summary>
    public static readonly string[] LowConfirmation =
    [
        "  The game will run smoothly in this",
        "mode, but most of the visual effects",
        "in the game will not be displayed.",
        "Dynamic reduction will reduce the 3D quality.",
        "We suggest you choose the standard",
        "definition or high definition.",
    ];
}
