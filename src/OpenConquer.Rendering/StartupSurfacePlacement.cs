namespace OpenConquer.Rendering;

/// <summary>
/// Where the startup logo is drawn inside a startup window's framebuffer.
/// </summary>
/// <param name="Width">Destination width in device pixels.</param>
/// <param name="Height">Destination height in device pixels.</param>
/// <remarks>
/// <para>
/// The origin is always the framebuffer's top-left corner, so only a size is carried. Retail paints
/// the logo through a <c>CreatePatternBrush</c> (<c>0x4B0AA2</c>) returned from
/// <c>WM_CTLCOLORDLG</c> (<c>0x4B0B26</c>), and a pattern brush tiles the bitmap at its natural
/// size from the client origin. There is no centring, aspect fit, or letterbox to describe.
/// </para>
/// <para>
/// "Natural size" is a logical-unit concept. On a display whose framebuffer is larger than its
/// logical client area the destination is scaled by the device pixel ratio, so the logo covers the
/// same logical area at every density instead of shrinking into a corner.
/// </para>
/// </remarks>
public readonly record struct StartupSurfacePlacement(int Width, int Height)
{
    /// <summary>
    /// Computes the destination size for an image of <paramref name="imageWidth"/> by
    /// <paramref name="imageHeight"/> logical units.
    /// </summary>
    /// <param name="framebufferWidth">Framebuffer width in device pixels.</param>
    /// <param name="framebufferHeight">Framebuffer height in device pixels.</param>
    /// <param name="logicalWidth">Window client width in logical units.</param>
    /// <param name="logicalHeight">Window client height in logical units.</param>
    /// <param name="imageWidth">Image width in logical units.</param>
    /// <param name="imageHeight">Image height in logical units.</param>
    /// <remarks>
    /// Sizes round to the nearest whole pixel away from zero and clamp to at least one pixel, so a
    /// degenerate ratio still produces a drawable quad rather than an empty one.
    /// </remarks>
    public static StartupSurfacePlacement Compute(
        int framebufferWidth,
        int framebufferHeight,
        int logicalWidth,
        int logicalHeight,
        int imageWidth,
        int imageHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(framebufferWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(framebufferHeight);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(logicalWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(logicalHeight);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(imageWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(imageHeight);

        double horizontalScale = (double)framebufferWidth / logicalWidth;
        double verticalScale = (double)framebufferHeight / logicalHeight;

        return new StartupSurfacePlacement(
            Scale(imageWidth, horizontalScale),
            Scale(imageHeight, verticalScale)
        );
    }

    private static int Scale(int lengthInLogicalUnits, double scale)
    {
        double scaledLength = Math.Round(lengthInLogicalUnits * scale, MidpointRounding.AwayFromZero);

        return scaledLength >= int.MaxValue ? int.MaxValue : Math.Max(1, (int)scaledLength);
    }
}
