using System.IO.Compression;
using OpenConquer.Content.Images;

namespace OpenConquer.Content.Startup;

/// <summary>Loads curated retail-derived login artwork embedded in the content assembly.</summary>
public static class LoginArtwork
{
    private const string BackdropResourceName = "OpenConquer.Content.Assets.Login.Backdrop.bmp";
    private const string HeaderResourceName = "OpenConquer.Content.Assets.Login.Header.rgba.br";
    private const string FooterResourceName = "OpenConquer.Content.Assets.Login.Footer.rgba.br";
    private const string AccountPanelResourceName = "OpenConquer.Content.Assets.Login.AccountPanel.rgba.br";
    private const string TopDragonResourceName = "OpenConquer.Content.Assets.Login.TopDragon.rgba.br";
    private const string RegionCellResourceName = "OpenConquer.Content.Assets.Login.RegionCell.rgba.br";
    private const string ServerCellResourceName = "OpenConquer.Content.Assets.Login.ServerCell.rgba.br";
    private const int MaximumBackdropBytes = 4 * 1024 * 1024;

    public const int ReferenceWidth = 1024;
    public const int ReferenceHeight = 768;
    public const int BackdropHeight = 637;

    public static LoginArtworkSet Load() => new(LoadBackdrop(), LoadHeader(), LoadFooter(), LoadAccountPanel(), LoadTopDragon(), LoadRegionCell(), LoadServerCell());

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

    public static RgbaImage LoadHeader() => LoadEmbeddedLayer(HeaderResourceName, width: 1024, height: 116);

    public static RgbaImage LoadFooter() => LoadEmbeddedLayer(FooterResourceName, width: 1024, height: 140);

    public static RgbaImage LoadAccountPanel() => LoadEmbeddedLayer(AccountPanelResourceName, width: 508, height: 98);

    public static RgbaImage LoadTopDragon() => LoadEmbeddedLayer(TopDragonResourceName, width: 494, height: 140);

    public static RgbaImage LoadRegionCell() => LoadEmbeddedLayer(RegionCellResourceName, width: 154, height: 54);

    public static RgbaImage LoadServerCell() => LoadEmbeddedLayer(ServerCellResourceName, width: 158, height: 58);

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
