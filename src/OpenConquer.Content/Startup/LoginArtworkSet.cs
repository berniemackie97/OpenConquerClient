using OpenConquer.Content.Images;

namespace OpenConquer.Content.Startup;

/// <summary>Validated artwork for one pre-world login presentation.</summary>
public sealed record LoginArtworkSet(RgbaImage Backdrop, RgbaImage Header, RgbaImage Footer, RgbaImage AccountPanel, RgbaImage TopDragon,
    RgbaImage RegionCell, RgbaImage ServerCell, RgbaImage SelectedRegion, RgbaImage SelectedServerGlow, RgbaImage EnterUp, RgbaImage EnterHover, RgbaImage EnterPressed, RgbaImage ExitUp, RgbaImage ExitHover, RgbaImage ExitPressed);
