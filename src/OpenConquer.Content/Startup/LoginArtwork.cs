using System.IO.Compression;
using OpenConquer.Content.Images;

namespace OpenConquer.Content.Startup;

/// <summary>
/// Loads the curated, offline-composited 5517 login backdrop. This is first-party presentation
/// content embedded in the client build, not a runtime Flash or retail WDF dependency.
/// </summary>
public static class LoginArtwork
{
    private const string BackdropResourceName = "OpenConquer.Content.Assets.Login.Backdrop.bmp";
    private const int MaximumBackdropBytes = 4 * 1024 * 1024;

    public const int ReferenceWidth = 1024;
    public const int ReferenceHeight = 768;


    private const string TopDragonResourceName = "OpenConquer.Content.Assets.Login.TopDragon.rgba.br";
    private const string ServerSlotResourceName = "OpenConquer.Content.Assets.Login.ServerSlot.rgba.br";

    public static RgbaImage LoadTopDragon() => LoadEmbeddedLayer(TopDragonResourceName, width: 494, height: 140);

    public static RgbaImage LoadServerSlot() => LoadEmbeddedLayer(ServerSlotResourceName, width: 158, height: 58);

    private static RgbaImage LoadEmbeddedLayer(string resourceName, int width, int height)
    {
        using Stream source = typeof(LoginArtwork).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded login resource '{resourceName}' is missing.");
        using BrotliStream decoder = new(source, CompressionMode.Decompress);
        byte[] pixels = GC.AllocateUninitializedArray<byte>(checked(width * height * 4));

        try
        {
            decoder.ReadExactly(pixels);
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException($"Embedded login resource '{resourceName}' is truncated.", exception);
        }

        if (decoder.ReadByte() != -1)
        {
            throw new InvalidDataException($"Embedded login resource '{resourceName}' contains trailing decoded pixels.");
        }

        return new RgbaImage(width, height, pixels);
    }

    public static RgbaImage LoadBackdrop()
    {
        using Stream stream = typeof(LoginArtwork).Assembly.GetManifestResourceStream(BackdropResourceName)
            ?? throw new InvalidOperationException($"Embedded login artwork '{BackdropResourceName}' is missing.");

        byte[] payload = ContentReader.ReadBytes(stream, BackdropResourceName, MaximumBackdropBytes);
        RgbaImage image = WindowsBitmapReader.Decode24Bit(payload);

        if (image.Width != ReferenceWidth || image.Height != ReferenceHeight)
        {
            throw new InvalidDataException($"Embedded login artwork must be {ReferenceWidth}x{ReferenceHeight}, but is {image.Width}x{image.Height}.");
        }

        return image;
    }
}
