namespace OpenConquer.Client.UI.Login;

/// <summary>Original bitmap footprints centered over selected grid cells on the 1024x768 stage.</summary>
internal static class LoginSelectionArtworkLayout
{
    public static LoginSpritePlacement SelectedRegion(LoginVisualLayout layout, int slot)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if ((uint)slot >= (uint)layout.RegionCells.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }

        return Center(layout, layout.RegionCells[slot], 150, 50);
    }

    public static LoginSpritePlacement SelectedServer(LoginVisualLayout layout, int slot)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if ((uint)slot >= (uint)layout.ServerCells.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(slot));
        }

        return Center(layout, layout.ServerCells[slot], 200, 100);
    }

    private static LoginSpritePlacement Center(LoginVisualLayout layout, LoginSpritePlacement cell, int nativeWidth, int nativeHeight)
    {
        int width = Scale(nativeWidth, layout.Width);
        int height = Scale(nativeHeight, layout.Width);
        // Arithmetic shifts floor negative half-pixel offsets instead of biasing oversized artwork down and right.
        LoginSpritePlacement bounds = new(checked(cell.X + ((cell.Width - width) >> 1)), checked(cell.Y + ((cell.Height - height) >> 1)), width, height);

        if (bounds.X < 0 || bounds.Y < 0 || (long)bounds.X + bounds.Width > layout.Width || (long)bounds.Y + bounds.Height > layout.Height)
        {
            throw new InvalidOperationException("Selected login artwork exceeds the logical stage.");
        }

        return bounds;
    }

    private static int Scale(int value, int width) => checked((int)(((long)value * width + 512) / 1024));
}
