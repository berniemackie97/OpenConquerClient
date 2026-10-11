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
