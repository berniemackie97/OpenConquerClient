using OpenConquer.Content.Images;

namespace OpenConquer.Content.Startup;

/// <summary>Validated visual inputs for one pre-world login presentation.</summary>
public sealed record LoginArtworkSet(RgbaImage Backdrop, RgbaImage Header, RgbaImage Footer, RgbaImage AccountPanel, RgbaImage TopDragon);
