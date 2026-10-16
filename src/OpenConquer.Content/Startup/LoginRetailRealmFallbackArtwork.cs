using System.IO.Compression;
using System.Security.Cryptography;
using OpenConquer.Content.Images;

namespace OpenConquer.Content.Startup;

/// <summary>5517 movie-authored fallback frames for empty and unpictured realm slots.</summary>
public static class LoginRetailRealmFallbackArtwork
{
    private const string ResourcePrefix = "OpenConquer.Content.Assets.Login.RetailFallback.";

    private readonly record struct Asset(string Id, int Width, int Height, string Sha256);
    private static readonly Asset s_unopened = new("Unopened", 170, 90, "64511F3C4B1A4B072F7F09B243C88A22A70F19CB8441A586D4B512871DE7F9DE");
    private static readonly Asset s_default = new("Default", 170, 90, "37A300257303589F9E1052D73805708807A9F1CCD466A8D927D4F491F3976C3B");

    public static RgbaImage LoadUnopened() => Load(s_unopened);
    public static RgbaImage LoadDefault() => Load(s_default);

    private static RgbaImage Load(Asset asset)
    {
        using Stream resource = typeof(LoginRetailRealmFallbackArtwork).Assembly.GetManifestResourceStream(
            ResourcePrefix + asset.Id + ".rgba.br")
            ?? throw new InvalidDataException($"Missing verified 5517 realm fallback: {asset.Id}.");
        using BrotliStream decoder = new(resource, CompressionMode.Decompress);
        byte[] pixels = GC.AllocateUninitializedArray<byte>(checked(asset.Width * asset.Height * 4));
        try
        {
            decoder.ReadExactly(pixels);
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException($"Truncated 5517 realm fallback: {asset.Id}.", exception);
        }

        if (decoder.ReadByte() != -1 ||
            !SHA256.HashData(pixels).AsSpan().SequenceEqual(Convert.FromHexString(asset.Sha256)))
        {
            throw new InvalidDataException($"5517 realm fallback failed integrity verification: {asset.Id}.");
        }

        return new RgbaImage(asset.Width, asset.Height, pixels);
    }
}
