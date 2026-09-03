namespace OpenConquer.Rendering;

/// <summary>
/// Where the fixed logical frame lands inside the resizable host framebuffer, and how to get back
/// again.
/// </summary>
/// <remarks>
/// <para>
/// This is the single definition of the logical-to-host transform. Rendering blits into the
/// rectangle it describes and input maps host positions back through <see cref="TryMapPointerToLogical"/>.
/// Deriving the two independently is how a client ends up drawing in one place and resolving clicks
/// in another, with the error growing towards the edges of the window.
/// </para>
/// <para>
/// All values are host framebuffer pixels, not window coordinates. On a display with a scale factor
/// the two differ, so a caller mapping a pointer position must convert it to framebuffer pixels
/// before calling <see cref="TryMapPointerToLogical"/>.
/// </para>
/// <para>
/// The default value is empty and maps nothing. A viewport is only meaningful when produced by
/// <see cref="Compute"/>.
/// </para>
/// </remarks>
public readonly record struct PresentationViewport
{
    private PresentationViewport(int offsetX, int offsetY, int width, int height, int hostWidth, int hostHeight, int logicalWidth, int logicalHeight, PresentationFilter filter)
    {
        HostWidth = hostWidth;
        HostHeight = hostHeight;
        OffsetX = offsetX;
        OffsetY = offsetY;
        Width = width;
        Height = height;
        LogicalWidth = logicalWidth;
        LogicalHeight = logicalHeight;
        Filter = filter;
    }

    /// <summary>Left edge of the destination rectangle, in host framebuffer pixels.</summary>
    public int OffsetX
    {
        get;
    }

    /// <summary>Bottom edge of the destination rectangle, in host framebuffer pixels.</summary>
    /// <remarks>
    /// Measured from the bottom because that is the origin OpenGL framebuffer operations use.
    /// Centring makes the two conventions agree, but a caller supplying a top-left pointer position
    /// must flip it before mapping.
    /// </remarks>
    public int OffsetY
    {
        get;
    }

    /// <summary>Width of the destination rectangle, in host framebuffer pixels.</summary>
    public int Width
    {
        get;
    }

    /// <summary>Height of the destination rectangle, in host framebuffer pixels.</summary>
    public int Height
    {
        get;
    }

    /// <summary>Width of the host framebuffer this viewport was computed for, in pixels.</summary>
    public int HostWidth
    {
        get;
    }

    /// <summary>Height of the host framebuffer this viewport was computed for, in pixels.</summary>
    /// <remarks>
    /// Retained so the viewport can flip a top-down pointer position itself. A caller that has to
    /// supply the framebuffer height separately is a caller that can supply the wrong one.
    /// </remarks>
    public int HostHeight
    {
        get;
    }

    /// <summary>Width of the logical frame being presented.</summary>
    public int LogicalWidth
    {
        get;
    }

    /// <summary>Height of the logical frame being presented.</summary>
    public int LogicalHeight
    {
        get;
    }

    /// <summary>How the copy should be resampled.</summary>
    public PresentationFilter Filter
    {
        get;
    }

    /// <summary>Whether this viewport has no drawable area, as when the host window is minimised.</summary>
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>Horizontal magnification applied to the logical frame.</summary>
    public double ScaleX => IsEmpty ? 0d : (double)Width / LogicalWidth;

    /// <summary>Vertical magnification applied to the logical frame.</summary>
    public double ScaleY => IsEmpty ? 0d : (double)Height / LogicalHeight;

    /// <summary>
    /// Whether the destination rectangle fills the whole host framebuffer, leaving no bars.
    /// </summary>
    /// <remarks>
    /// When this is <see langword="false"/> the caller must clear the host framebuffer before
    /// copying, because the area outside the rectangle is never written and would otherwise show
    /// whatever the previous frame left in the back buffer.
    /// </remarks>
    public bool CoversHostFramebuffer()
    {
        return OffsetX == 0 && OffsetY == 0 && Width == HostWidth && Height == HostHeight;
    }

    /// <summary>
    /// Places <paramref name="logical"/> inside a host framebuffer according to
    /// <paramref name="policy"/>.
    /// </summary>
    /// <param name="logical">The fixed logical frame size.</param>
    /// <param name="hostFramebufferWidth">Host framebuffer width in pixels. Zero while minimised.</param>
    /// <param name="hostFramebufferHeight">Host framebuffer height in pixels. Zero while minimised.</param>
    /// <param name="policy">How the frame should be fitted.</param>
    public static PresentationViewport Compute(LogicalRenderSize logical, int hostFramebufferWidth, int hostFramebufferHeight, PresentationPolicy policy = PresentationPolicy.Fit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(hostFramebufferWidth);
        ArgumentOutOfRangeException.ThrowIfNegative(hostFramebufferHeight);

        if (!Enum.IsDefined(policy))
        {
            throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unknown presentation policy.");
        }

        int logicalWidth = logical.Width;
        int logicalHeight = logical.Height;

        if (logicalWidth <= 0 || logicalHeight <= 0 || hostFramebufferWidth == 0 || hostFramebufferHeight == 0)
        {
            // A default LogicalRenderSize bypasses its own constructor validation, and a minimised
            // host reports a zero framebuffer. Neither is an error; there is simply nothing to
            // present until the next resize.
            return new PresentationViewport(0, 0, 0, 0, hostFramebufferWidth, hostFramebufferHeight, logicalWidth, logicalHeight, PresentationFilter.Nearest);
        }

        (int width, int height) = policy switch
        {
            PresentationPolicy.Stretch => (hostFramebufferWidth, hostFramebufferHeight),
            PresentationPolicy.IntegerScale => FitWhole(logicalWidth, logicalHeight, hostFramebufferWidth, hostFramebufferHeight),
            _ => FitUniform(logicalWidth, logicalHeight, hostFramebufferWidth, hostFramebufferHeight),
        };

        // Centring the remainder splits any odd leftover pixel towards the top-right. The
        // alternative is a bar that changes side as the window is dragged across a size.
        int offsetX = (hostFramebufferWidth - width) / 2;
        int offsetY = (hostFramebufferHeight - height) / 2;

        return new PresentationViewport(offsetX, offsetY, width, height, hostFramebufferWidth, hostFramebufferHeight, logicalWidth, logicalHeight,
            SelectFilter(width, height, logicalWidth, logicalHeight));
    }

    /// <summary>
    /// Converts a pointer position to the logical pixel underneath it.
    /// </summary>
    /// <param name="pointerX">Pointer X in host framebuffer pixels, measured from the left.</param>
    /// <param name="pointerY">Pointer Y in host framebuffer pixels, measured from the <b>top</b>.</param>
    /// <param name="logicalX"></param>
    /// <param name="logicalY"></param>
    /// <remarks>
    /// <para>
    /// This is the entry point input should use. Pointer positions are top-down and framebuffer
    /// operations are bottom-up, so the flip happens here rather than at each call site, where
    /// forgetting it mirrors every click about the horizontal centre — an error that looks correct
    /// at the middle of the window and worsens towards the top and bottom edges.
    /// </para>
    /// <para>
    /// The position must already be in host framebuffer pixels. A windowing pointer position is in
    /// window coordinates, which differ from framebuffer pixels on a scaled display; convert it
    /// first, on the desktop through <c>DesktopWindow.PointToFramebuffer</c>.
    /// </para>
    /// </remarks>
    /// <returns>
    /// <see langword="false"/> when the position falls in a letterbox bar or outside the window,
    /// which is not a position in the game world and must not be resolved to one.
    /// </returns>
    public bool TryMapPointerToLogical(int pointerX, int pointerY, out int logicalX, out int logicalY)
    {
        if (IsEmpty)
        {
            logicalX = 0;
            logicalY = 0;

            return false;
        }

        // Two flips, not one. The first converts the pointer row into the bottom-up framebuffer row
        // the destination rectangle is expressed in.
        long flippedPointerY = (long)HostHeight - 1 - pointerY;

        if (flippedPointerY is < 0 or > int.MaxValue)
        {
            logicalX = 0;
            logicalY = 0;

            return false;
        }

        if (!TryMapFramebufferPointToLogical(pointerX, (int)flippedPointerY, out logicalX, out int bottomUpLogicalY))
        {
            logicalY = 0;

            return false;
        }

        // The second converts the result back out of framebuffer space. Without it the top of the
        // window resolves to the bottom of the logical frame: every position maps successfully and
        // every one is mirrored, which is why only an ordering check catches it.
        logicalY = LogicalHeight - 1 - bottomUpLogicalY;

        return true;
    }

    /// <summary>
    /// Converts a host framebuffer pixel to the logical pixel drawn there.
    /// </summary>
    /// <param name="hostFramebufferX">X in host framebuffer pixels, measured from the left.</param>
    /// <param name="hostFramebufferY">Y in host framebuffer pixels, measured from the <b>bottom</b>.</param>
    /// <param name="logicalX"></param>
    /// <param name="logicalY"></param>
    /// <remarks>
    /// Bottom-up, matching the framebuffer's own origin. Input positions are top-down and belong in
    /// <see cref="TryMapPointerToLogical"/>; the two are named apart so neither origin can be
    /// assumed by a caller reading only the signature.
    /// </remarks>
    /// <returns>
    /// <see langword="false"/> when the position falls in a letterbox bar or outside the window,
    /// which is not a position in the game world and must not be resolved to one.
    /// </returns>
    public bool TryMapFramebufferPointToLogical(int hostFramebufferX, int hostFramebufferY, out int logicalX, out int logicalY)
    {
        logicalX = 0;
        logicalY = 0;

        if (IsEmpty)
        {
            return false;
        }

        long relativeX = (long)hostFramebufferX - OffsetX;
        long relativeY = (long)hostFramebufferY - OffsetY;

        if (relativeX < 0 || relativeX >= Width || relativeY < 0 || relativeY >= Height)
        {
            return false;
        }

        // Both quotients are below the logical extent because the relative position is below the
        // destination extent, so the result needs no clamping.
        logicalX = (int)(relativeX * LogicalWidth / Width);
        logicalY = (int)(relativeY * LogicalHeight / Height);

        return true;
    }

    /// <summary>
    /// Largest rectangle with the logical aspect ratio that fits the host framebuffer.
    /// </summary>
    /// <remarks>
    /// Compares the two cross products rather than dividing, so the constraining axis is chosen
    /// exactly and the derived extent can never round above the host framebuffer.
    /// </remarks>
    private static (int Width, int Height) FitUniform(int logicalWidth, int logicalHeight, int hostWidth, int hostHeight)
    {
        if ((long)hostWidth * logicalHeight <= (long)hostHeight * logicalWidth)
        {
            int height = (int)((long)hostWidth * logicalHeight / logicalWidth);

            return (hostWidth, Math.Max(height, 1));
        }

        int width = (int)((long)hostHeight * logicalWidth / logicalHeight);

        return (Math.Max(width, 1), hostHeight);
    }

    /// <summary>
    /// Largest whole-number magnification of the logical frame that fits the host framebuffer.
    /// </summary>
    /// <remarks>
    /// Falls back to <see cref="FitUniform"/> when not even a single whole copy fits, so shrinking
    /// the window below the logical size keeps the whole frame visible instead of clipping it.
    /// </remarks>
    private static (int Width, int Height) FitWhole(int logicalWidth, int logicalHeight, int hostWidth, int hostHeight)
    {
        int scale = Math.Min(hostWidth / logicalWidth, hostHeight / logicalHeight);

        return scale < 1
            ? FitUniform(logicalWidth, logicalHeight, hostWidth, hostHeight)
            : (logicalWidth * scale, logicalHeight * scale);
    }

    /// <summary>
    /// Chooses point sampling whenever the copy reproduces the logical frame exactly.
    /// </summary>
    /// <remarks>
    /// That is the case for a 1:1 copy and for any equal whole-number magnification on both axes,
    /// where every logical pixel becomes a square block and bilinear sampling would only soften
    /// edges that land on exact texel boundaries. Every other case resamples unevenly and reads
    /// better bilinear. Derived from the final rectangle rather than from the policy, so a
    /// <see cref="PresentationPolicy.Fit"/> window that happens to land on a whole multiple gets
    /// the sharper result too.
    /// </remarks>
    private static PresentationFilter SelectFilter(int width, int height, int logicalWidth, int logicalHeight)
    {
        if (width % logicalWidth != 0 || height % logicalHeight != 0)
        {
            return PresentationFilter.Linear;
        }

        int horizontalScale = width / logicalWidth;
        int verticalScale = height / logicalHeight;

        return horizontalScale == verticalScale && horizontalScale >= 1 ? PresentationFilter.Nearest : PresentationFilter.Linear;
    }
}
