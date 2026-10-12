using System.IO.Compression;
using OpenConquer.Content.Images;

namespace OpenConquer.Content.Startup;

/// <summary>Closed set of retail-derived server images; never resolves catalog-provided paths.</summary>
public static class LoginRealmIconAssets
{
    private const string ResourcePrefix = "OpenConquer.Content.Assets.RealmIcons.";
    private const int StandardWidth = 156;
    private const int StandardHeight = 56;
    private static readonly int[] s_serversPerGroup = [12, 13, 12, 12, 12, 12, 12, 12, 5];

    public static int IconCount => 102;

    public static bool IsKnownId(string? iconId)
    {
        if (iconId is null || iconId.Length != 6 || iconId[0] != 'g' || iconId[3] != 's' ||
            iconId[1] is < '0' or > '9' || iconId[2] is < '0' or > '9' ||
            iconId[4] is < '0' or > '9' || iconId[5] is < '0' or > '9')
        {
            return false;
        }

        int group = (iconId[1] - '0') * 10 + iconId[2] - '0';
        int server = (iconId[4] - '0') * 10 + iconId[5] - '0';
        return group >= 1 && group <= s_serversPerGroup.Length && server >= 1 && server <= s_serversPerGroup[group - 1];
    }

    public static string ValidateId(string iconId) => IsKnownId(iconId) ? iconId : throw new ArgumentException("Unsupported bundled realm icon identifier.", nameof(iconId));

    public static RgbaImage Load(string iconId)
    {
        ValidateId(iconId);

        (int width, int height) = iconId switch
        {
            "g09s01" => (164, 85),
            "g09s02" => (154, 54),
            _ => (StandardWidth, StandardHeight),
        };

        string resourceName = ResourcePrefix + iconId + ".rgba.br";
        using Stream source = typeof(LoginRealmIconAssets).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidDataException("Bundled realm icon resource is missing.");
        using BrotliStream decoder = new(source, CompressionMode.Decompress);
        byte[] pixels = GC.AllocateUninitializedArray<byte>(checked(width * height * 4));

        try
        {
            decoder.ReadExactly(pixels);
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException("Bundled realm icon is truncated.", exception);
        }

        if (decoder.ReadByte() != -1)
        {
            throw new InvalidDataException("Bundled realm icon has trailing decoded pixels.");
        }

        return new RgbaImage(width, height, pixels);
    }
}
