namespace OpenConquer.Platform;

/// <summary>
/// A position in host framebuffer pixels, measured from the top left.
/// </summary>
public readonly record struct PixelPoint(int X, int Y)
{
    /// <summary>Distance from the left edge of the framebuffer, in pixels.</summary>
    public int X { get; } = X;

    /// <summary>Distance from the top edge of the framebuffer, in pixels.</summary>
    public int Y { get; } = Y;
}
