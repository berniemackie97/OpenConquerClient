using System.IO.Compression;
using OpenConquer.Content.Images;

namespace OpenConquer.Content.Startup;

/// <summary>Loads curated retail-derived login artwork embedded in the content assembly.</summary>
public static class LoginArtwork
{
    private const string BackdropResourceName = "OpenConquer.Content.Assets.Login.Backdrop.bmp";
    private const string AccountPanelResourceName = "OpenConquer.Content.Assets.Login.AccountPanel.rgba.br";
    private const string EnterUpResourceName = "OpenConquer.Content.Assets.Login.EnterUp.rgba.br";
    private const string EnterHoverResourceName = "OpenConquer.Content.Assets.Login.EnterHover.rgba.br";
    private const string EnterPressedResourceName = "OpenConquer.Content.Assets.Login.EnterPressed.rgba.br";
    private const string ExitUpResourceName = "OpenConquer.Content.Assets.Login.ExitUp.rgba.br";
    private const string ExitHoverResourceName = "OpenConquer.Content.Assets.Login.ExitHover.rgba.br";
    private const string ExitPressedResourceName = "OpenConquer.Content.Assets.Login.ExitPressed.rgba.br";
    private const int MaximumBackdropBytes = 4 * 1024 * 1024;

    public const int ReferenceWidth = 1024;
    public const int ReferenceHeight = 768;
    public const int BackdropHeight = 637;

    public static LoginArtworkSet Load() => new(LoadBackdrop(), LoadAccountPanel(),
        LoadEnterUp(), LoadEnterHover(), LoadEnterPressed(), LoadExitUp(), LoadExitHover(), LoadExitPressed());

    public static RgbaImage LoadBackdrop()
    {
        using Stream stream = typeof(LoginArtwork).Assembly.GetManifestResourceStream(BackdropResourceName)
            ?? throw new InvalidOperationException($"Embedded login artwork '{BackdropResourceName}' is missing.");

        byte[] payload = ContentReader.ReadBytes(stream, BackdropResourceName, MaximumBackdropBytes);
        RgbaImage image = WindowsBitmapReader.Decode24Bit(payload);

        if (image.Width != ReferenceWidth || image.Height != BackdropHeight)
        {
            throw new InvalidDataException($"Embedded login backdrop must be {ReferenceWidth}x{BackdropHeight}, but is {image.Width}x{image.Height}.");
        }

        return image;
    }

    public static RgbaImage LoadAccountPanel() => LoadEmbeddedLayer(AccountPanelResourceName, width: 508, height: 98);

    public static RgbaImage LoadEnterUp() => LoadEmbeddedLayer(EnterUpResourceName, width: 132, height: 40);

    public static RgbaImage LoadEnterHover() => LoadEmbeddedLayer(EnterHoverResourceName, width: 132, height: 40);

    public static RgbaImage LoadEnterPressed() => LoadEmbeddedLayer(EnterPressedResourceName, width: 132, height: 40);

    public static RgbaImage LoadExitUp() => LoadEmbeddedLayer(ExitUpResourceName, width: 132, height: 40);

    public static RgbaImage LoadExitHover() => LoadEmbeddedLayer(ExitHoverResourceName, width: 132, height: 40);

    public static RgbaImage LoadExitPressed() => LoadEmbeddedLayer(ExitPressedResourceName, width: 132, height: 40);

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
}
