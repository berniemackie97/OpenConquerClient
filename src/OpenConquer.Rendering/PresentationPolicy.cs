namespace OpenConquer.Rendering;

/// <summary>
/// Selects how the fixed logical frame is placed inside the resizable host framebuffer.
/// </summary>
public enum PresentationPolicy
{
    /// <summary>
    /// Scales by the largest uniform factor that fits and centres the result, leaving pillarbox
    /// or letterbox bars. Aspect ratio is preserved.
    /// </summary>
    /// <remarks>
    /// The default. It fills as much of the host window as a distortion-free scale allows, which
    /// is what a player resizing a window expects to happen.
    /// </remarks>
    Fit = 0,

    /// <summary>
    /// Scales by the largest whole-number factor that fits and centres the result. Aspect ratio is
    /// preserved and every logical pixel becomes an exact square block of host pixels.
    /// </summary>
    /// <remarks>
    /// Produces the sharpest image because no resampling occurs, at the cost of larger bars. When
    /// the host framebuffer is smaller than one whole logical frame there is no whole-number factor
    /// to use, and this falls back to <see cref="Fit"/> so the frame stays fully visible.
    /// </remarks>
    IntegerScale = 1,

    /// <summary>
    /// Stretches the logical frame across the whole host framebuffer, distorting it whenever the
    /// two aspect ratios differ.
    /// </summary>
    /// <remarks>
    /// Retained only as an explicit opt-in. A 4:3 logical frame on a 16:9 host is stretched about
    /// 1.33x horizontally, so this is never a sensible default.
    /// </remarks>
    Stretch = 2,
}
