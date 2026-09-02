namespace OpenConquer.Rendering;

/// <summary>
/// How the logical frame is resampled when it is copied to the host framebuffer.
/// </summary>
/// <remarks>
/// Kept free of any graphics-API type so the presentation transform stays testable without a
/// device. The renderer translates this to its own backend enumeration.
/// </remarks>
public enum PresentationFilter
{
    /// <summary>
    /// Point sampling. Correct whenever the copy is 1:1 or a whole-number magnification, where it
    /// reproduces the logical frame exactly.
    /// </summary>
    Nearest = 0,

    /// <summary>
    /// Bilinear sampling, for fractional scales where point sampling would drop or duplicate rows
    /// and columns unevenly.
    /// </summary>
    Linear = 1,
}
