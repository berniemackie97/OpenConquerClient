using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace OpenConquer.Launcher.Play;

/// <summary>
/// <c>SetWindowRgn</c> with <c>Play/Main.rgn</c>: the window exists only where the main art is opaque, so the
/// screen behind its torn edges stays clickable. Other platforms let clicks through fully transparent pixels.
/// </summary>
internal static partial class PlayWindowShape
{
    private const int RgnOr = 2;
    private const int RgnError = 0;

    public static void Apply(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (!OperatingSystem.IsWindows() || window.TryGetPlatformHandle()?.Handle is not { } handle || handle == IntPtr.Zero)
        {
            return;
        }

        double scale = window.RenderScaling;
        using PlayBitmap main = PlayBitmap.Load(PlayArtwork.Main);
        IntPtr region = CreateRectRgn(0, 0, 0, 0);
        if (region == IntPtr.Zero)
        {
            return;
        }

        try
        {
            // One rectangle per run of opaque pixels in each row: the shape Main.rgn stores.
            for (int y = 0; y < main.Height; y++)
            {
                int x = 0;
                while (x < main.Width)
                {
                    while (x < main.Width && !main.IsOpaqueAt(new(x, y)))
                    {
                        x++;
                    }

                    int start = x;
                    while (x < main.Width && main.IsOpaqueAt(new(x, y)))
                    {
                        x++;
                    }

                    if (x > start && !AddRun(region, Scale(start, scale), Scale(y, scale), Scale(x, scale), Scale(y + 1, scale)))
                    {
                        return;
                    }
                }
            }

            // On success the system owns the region.
            if (SetWindowRgn(handle, region, true) != 0)
            {
                region = IntPtr.Zero;
            }
        }
        finally
        {
            if (region != IntPtr.Zero)
            {
                DeleteObject(region);
            }
        }
    }

    private static bool AddRun(IntPtr region, int left, int top, int right, int bottom)
    {
        IntPtr run = CreateRectRgn(left, top, right, bottom);
        if (run == IntPtr.Zero)
        {
            return false;
        }

        bool combined = CombineRgn(region, region, run, RgnOr) != RgnError;
        DeleteObject(run);
        return combined;
    }

    private static int Scale(int value, double scale) => (int)Math.Round(value * scale, MidpointRounding.AwayFromZero);

    [LibraryImport("gdi32.dll")]
    private static partial IntPtr CreateRectRgn(int left, int top, int right, int bottom);

    [LibraryImport("gdi32.dll")]
    private static partial int CombineRgn(IntPtr destination, IntPtr source1, IntPtr source2, int mode);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteObject(IntPtr handle);

    [LibraryImport("user32.dll")]
    private static partial int SetWindowRgn(IntPtr window, IntPtr region, [MarshalAs(UnmanagedType.Bool)] bool redraw);
}
