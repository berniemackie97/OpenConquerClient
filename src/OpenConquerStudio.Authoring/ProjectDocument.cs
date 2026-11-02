using System.Collections.Immutable;
using OpenConquerStudio.Runtime;

namespace OpenConquerStudio.Authoring;

public sealed record TextureAsset(Guid Id, string Name, int Width, int Height, string PixelDigest,
    string SourceFormat, string SourceDigest, bool FlipHorizontal, int ClockwiseQuarterTurns, string? SourceKey = null);

public sealed record ProjectDocument(int SchemaVersion, Guid Id, string Name, ImmutableArray<TextureAsset> Assets)
{
    public const int CurrentVersion = 1;
    public const int MaximumAssets = 64;
    public const int MaximumTotalPixelBytes = 64 * 1024 * 1024;

    public void Validate()
    {
        if (SchemaVersion != CurrentVersion)
        {
            throw new InvalidDataException($"Project version {SchemaVersion} is unsupported; expected {CurrentVersion}. No migration is available.");
        }

        ValidateName(Name);
        if (Id == Guid.Empty || Assets.IsDefault || Assets.Length > MaximumAssets)
        {
            throw new InvalidDataException("Project identity or asset count is invalid.");
        }

        HashSet<Guid> ids = [];
        long total = 0;
        foreach (TextureAsset asset in Assets)
        {
            if (asset is null || asset.Id == Guid.Empty || !ids.Add(asset.Id))
            {
                throw new InvalidDataException("Asset identities must be nonempty and unique.");
            }

            ValidateName(asset.Name);
            ValidateDigest(asset.PixelDigest);
            ValidateDigest(asset.SourceDigest);
            if (asset.SourceKey is not null && (asset.SourceKey.Length is < 1 or > 256 ||
                asset.SourceKey.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '/' or '-' or '_'))))
            {
                throw new InvalidDataException("The image source key is invalid.");
            }
            total += TextureData.GetByteLength(asset.Width, asset.Height);
            if (asset.ClockwiseQuarterTurns is < 0 or > 3 || asset.SourceFormat is not ("tga" or "dds" or "rgba" or "created"))
            {
                throw new InvalidDataException($"Asset '{asset.Name}' has unsupported import or edit settings.");
            }
        }

        if (total > MaximumTotalPixelBytes)
        {
            throw new InvalidDataException("The project exceeds its 64 MiB aggregate pixel budget.");
        }
    }

    public static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 128 || name != name.Trim() || name.Any(char.IsControl))
        {
            throw new InvalidDataException("Names must contain 1–128 characters, without surrounding whitespace or control characters.");
        }
    }

    internal static void ValidateDigest(string digest)
    {
        if (digest is null || digest.Length != 64 || digest.Any(c => c is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
        {
            throw new InvalidDataException("Expected a lowercase SHA-256 digest.");
        }
    }
}
