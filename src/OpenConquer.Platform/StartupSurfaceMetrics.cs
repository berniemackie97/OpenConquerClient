namespace OpenConquer.Platform;

/// <summary>
/// The two sizes a startup-surface renderer needs for one frame.
/// </summary>
/// <param name="FramebufferSize">Drawable size in device pixels.</param>
/// <param name="LogicalSize">
/// Client size in logical units. On a scaled display this is smaller than
/// <paramref name="FramebufferSize"/>; their ratio is the device pixel ratio.
/// </param>
public readonly record struct StartupSurfaceMetrics(PixelSize FramebufferSize, PixelSize LogicalSize);
