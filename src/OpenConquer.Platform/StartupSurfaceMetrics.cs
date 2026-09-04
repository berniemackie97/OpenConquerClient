namespace OpenConquer.Platform;

/// <summary>
/// The two sizes a startup surface renderer needs for one frame.
/// </summary>
public readonly record struct StartupSurfaceMetrics(PixelSize FramebufferSize, PixelSize LogicalSize);
