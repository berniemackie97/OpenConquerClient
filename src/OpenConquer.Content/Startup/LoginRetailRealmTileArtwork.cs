using System.IO.Compression;
using System.Security.Cryptography;
using OpenConquer.Content.Images;

namespace OpenConquer.Content.Startup;

/// <summary>Original retail 5517 server-tile frame; clipped from DefineSprite 179 frame 1.</summary>
public static class LoginRetailRealmTileArtwork
{
    private const string Resource = "OpenConquer.Content.Assets.Login.RetailSelection.RealmIdleTile.rgba.br";
    private const string RgbaSha256 = "74400D45893D67AC71868A1A106C8C4BB611EA31DF5DB84EF258320B8A1F10C4";

    public static RgbaImage Load()
    {
        using Stream source = typeof(LoginRetailRealmTileArtwork).Assembly.GetManifestResourceStream(Resource)
            ?? throw new InvalidDataException("Missing verified retail realm tile.");
        using BrotliStream decoder = new(source, CompressionMode.Decompress);
        byte[] pixels = GC.AllocateUninitializedArray<byte>(155 * 59 * 4);
        try
        {
            decoder.ReadExactly(pixels);
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException("Truncated retail realm tile.", exception);
        }
        if (decoder.ReadByte() != -1 ||
            !SHA256.HashData(pixels).AsSpan().SequenceEqual(Convert.FromHexString(RgbaSha256)))
        {
            throw new InvalidDataException("Retail realm tile failed integrity verification.");
        }
        return new RgbaImage(155, 59, pixels);
    }
}
