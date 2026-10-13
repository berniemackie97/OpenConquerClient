using System.IO.Compression;
using OpenConquer.Content.Images;

namespace OpenConquer.Content.Startup;

/// <summary>The original five 32×32 cursor frames extracted from retail 5517 Normal.ani.</summary>
public static class LoginCursorArtwork
{
    private const int FrameCount = 5;
    private const int Side = 32;
    private const int DecodedFrameBytes = Side * Side * 4;
    private const string ResourcePrefix = "OpenConquer.Content.Assets.Login.Cursor.Normal";

    public static TimeSpan FrameDuration => TimeSpan.FromSeconds(10d / 60d);
    public static int HotspotX => 0;
    public static int HotspotY => 0;

    public static IReadOnlyList<RgbaImage> Load()
    {
        RgbaImage[] frames = new RgbaImage[FrameCount];
        for (int frame = 0; frame < frames.Length; frame++)
        {
            using Stream stream = typeof(LoginCursorArtwork).Assembly.GetManifestResourceStream($"{ResourcePrefix}{frame}.rgba.br")
                ?? throw new InvalidDataException($"Embedded retail cursor frame {frame} is absent.");
            using BrotliStream decoder = new(stream, CompressionMode.Decompress);
            byte[] pixels = new byte[DecodedFrameBytes];
            try
            {
                decoder.ReadExactly(pixels);
            }
            catch (EndOfStreamException error)
            {
                throw new InvalidDataException($"Embedded retail cursor frame {frame} is truncated.", error);
            }
            if (decoder.ReadByte() != -1)
            {
                throw new InvalidDataException($"Embedded retail cursor frame {frame} contains extra decoded data.");
            }
            frames[frame] = new RgbaImage(Side, Side, pixels);
        }
        return Array.AsReadOnly(frames);
    }
}
