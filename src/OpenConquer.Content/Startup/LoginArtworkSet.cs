using OpenConquer.Content.Images;

namespace OpenConquer.Content.Startup;

/// <summary>Validated artwork for one pre-world login presentation.</summary>
public sealed record LoginArtworkSet(RgbaImage Backdrop, RgbaImage AccountPanel, RgbaImage EnterUp, RgbaImage EnterHover, RgbaImage EnterPressed,
    RgbaImage ExitUp, RgbaImage ExitHover, RgbaImage ExitPressed);
