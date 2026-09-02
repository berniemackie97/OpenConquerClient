namespace OpenConquer.Platform;

/// <summary>
/// A position in host framebuffer pixels, measured from the top-left.
/// </summary>
/// <remarks>
/// Distinct from <see cref="PixelSize"/> because a position and an extent are not interchangeable,
/// and because negative values are meaningful here: a pointer can sit outside the window while a
/// drag is in progress.
/// </remarks>
public readonly record struct PixelPoint
{
    public PixelPoint(int x, int y)
    {
        X = x;
        Y = y;
    }

    /// <summary>Distance from the left edge of the framebuffer, in pixels.</summary>
    public int X
    {
        get;
    }

    /// <summary>Distance from the top edge of the framebuffer, in pixels.</summary>
    public int Y
    {
        get;
    }
}
