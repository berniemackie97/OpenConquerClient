using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace OpenConquer.Launcher.Play;

/// <summary>A decoded launcher image as an Avalonia bitmap, with the opaque-pixel mask the retail skin used for hit testing.</summary>
internal sealed class PlayBitmap : IDisposable
{
    private readonly bool[] _opaque;

    private PlayBitmap(Bitmap bitmap, bool[] opaque, int width, int height)
    {
        Bitmap = bitmap;
        _opaque = opaque;
        Width = width;
        Height = height;
    }

    public Bitmap Bitmap
    {
        get;
    }

    public int Width
    {
        get;
    }

    public int Height
    {
        get;
    }

    public static PlayBitmap Load(PlayImage image)
    {
        byte[] pixels = PlayArtwork.Load(image);
        WriteableBitmap bitmap = new(new PixelSize(image.Width, image.Height), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
        try
        {
            using (ILockedFramebuffer framebuffer = bitmap.Lock())
            {
                int rowLength = image.Width * 4;
                for (int row = 0; row < image.Height; row++)
                {
                    Marshal.Copy(pixels, row * rowLength, framebuffer.Address + row * framebuffer.RowBytes, rowLength);
                }
            }

            bool[] opaque = new bool[image.Width * image.Height];
            for (int pixel = 0; pixel < opaque.Length; pixel++)
            {
                opaque[pixel] = pixels[pixel * 4 + 3] != 0;
            }

            return new PlayBitmap(bitmap, opaque, image.Width, image.Height);
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    /// <summary>Whether a point lies on an opaque pixel: the area <c>CSkinButton_CreateRegionFromColorKey</c> kept.</summary>
    public bool IsOpaqueAt(Point point)
    {
        int x = (int)Math.Floor(point.X);
        int y = (int)Math.Floor(point.Y);
        return (uint)x < (uint)Width && (uint)y < (uint)Height && _opaque[y * Width + x];
    }

    public void Dispose() => Bitmap.Dispose();
}
