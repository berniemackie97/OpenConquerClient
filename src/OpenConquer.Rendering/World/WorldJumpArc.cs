using OpenConquer.Content.Map;

namespace OpenConquer.Rendering.World;

/// <summary>
/// The 5517 terrain-aware jump arc and the helpers it uses. See 20_notes
/// Network/15_msgwalk_10005_direction_index_order.md, "Remote jump presentation".
/// </summary>
public static class WorldJumpArc
{
    /// <summary><c>Math_RoundDoubleByHalfStepCompare</c> (0x768AA1).</summary>
    public static int RoundHalfStep(double value)
    {
        int truncated = (int)value;
        return (int)(value + 0.5) > truncated ? truncated + 1 : truncated;
    }

    /// <summary><c>CGameMap_ProjectScreenPointToTileCoordinatesUnchecked</c> (0x61D707): no clamping.</summary>
    public static (int X, int Y) ScreenToTile(int mapWidth, int worldX, int worldY)
    {
        double normalizedX = ((double)worldX - (long)mapWidth * 32) * (1.0 / 64);
        double normalizedY = ((double)worldY - 16) * (1.0 / 32);
        return (RoundHalfStep(normalizedX + normalizedY), RoundHalfStep(normalizedY - normalizedX));
    }

    /// <summary>
    /// <c>BuildRoundedLineTileVectorExcludingStart</c> (0x620171): the integer-rounded line from source to
    /// target, excluding the source and including the target; ties use the Y-dominant branch.
    /// </summary>
    public static IEnumerable<(int X, int Y)> RoundedLineExcludingStart(int x0, int y0, int x1, int y1)
    {
        int dx = x1 - x0;
        int dy = y1 - y0;
        if (dx == 0 && dy == 0)
        {
            yield break;
        }

        int ax = Math.Abs(dx);
        int ay = Math.Abs(dy);
        int sx = dx > 0 ? 1 : -1;
        int sy = dy > 0 ? 1 : -1;
        if (ax > ay)
        {
            for (int k = 1; k <= ax; k++)
            {
                yield return (x0 + sx * k, y0 + (2 * dy * k + sy * ax) / (2 * ax));
            }
        }
        else
        {
            for (int k = 1; k <= ay; k++)
            {
                yield return (x0 + (2 * dx * k + sx * ay) / (2 * ay), y0 + sy * k);
            }
        }
    }

    /// <summary>
    /// <c>Map_ComputeMotionArcScreenHeightOffsetFromProjectedPoints</c> (0x6505B3): arc height at the
    /// current point between the projected start and end points; 0 when an endpoint or the peak tile
    /// has no entry.
    /// </summary>
    public static int Height(GameMapTerrainElevation elevation,
        (int X, int Y) start, (int X, int Y) end, (int X, int Y) current)
    {
        ArgumentNullException.ThrowIfNull(elevation);
        (int startTileX, int startTileY) = ScreenToTile(elevation.Width, start.X, start.Y);
        (int endTileX, int endTileY) = ScreenToTile(elevation.Width, end.X, end.Y);

        // CGameMap_BuildLineTileEntriesAndFindPeakElevationTile (0x61E886): first strict maximum, from -65535.
        int peakTail = unchecked((int)0xFFFF0001);
        (int X, int Y)? peak = null;
        foreach ((int x, int y) in RoundedLineExcludingStart(startTileX, startTileY, endTileX, endTileY))
        {
            if (elevation.TryGetTail(x, y, out int tail) && tail > peakTail)
            {
                peakTail = tail;
                peak = (x, y);
            }
        }

        if (!elevation.TryGetAdjusted(startTileX, startTileY, out int startAdjusted) ||
            !elevation.TryGetAdjusted(endTileX, endTileY, out int endAdjusted) ||
            peak is not { } peakTile ||
            !elevation.TryGetAdjusted(peakTile.X, peakTile.Y, out int peakAdjusted))
        {
            return 0;
        }

        long spanX = start.X - end.X;
        long spanY = start.Y - end.Y;
        int apex = (int)(Math.Sqrt((double)unchecked((int)(spanX * spanX + spanY * spanY))) * (1.0 / 3) +
            peakAdjusted) + 20;
        int midX = (start.X + end.X) / 2;
        int midY = (start.Y + end.Y) / 2;

        uint toCurrent = unchecked((uint)(Square(start.X - current.X) + Square(start.Y - current.Y)));
        uint toMiddle = unchecked((uint)(Square(start.Y - midY) + Square(start.X - midX)));
        (int X, int Y) anchor = toMiddle > toCurrent ? start : end;
        int anchorAdjusted = toMiddle > toCurrent ? startAdjusted : endAdjusted;
        double fromAnchor = Math.Sqrt(unchecked(Square(anchor.X - current.X) + Square(anchor.Y - current.Y)));
        double half = Math.Sqrt(unchecked(Square(anchor.Y - midY) + Square(anchor.X - midX)));
        if (half == 0)
        {
            half = 1;
        }

        double rise = Math.Sin(fromAnchor * 3.1415926535 / half * 0.5) * (apex - anchorAdjusted);
        return (int)(rise + anchorAdjusted);
    }

    private static int Square(int value) => unchecked(value * value);
}
