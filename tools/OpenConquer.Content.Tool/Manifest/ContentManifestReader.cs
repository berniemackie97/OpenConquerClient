using System.Text.Json;

namespace OpenConquer.Content.Tool.Manifest;

internal static class ContentManifestReader
{
    public const int MaximumLength = 8 * 1024 * 1024;
    private const int Sha256HexLength = 64;

    public static ContentManifest Read(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);

        using JsonDocument document = JsonDocument.Parse(source);
        JsonElement root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("The content-set manifest is not a JSON object.");
        }

        int schemaVersion = ReadInt32(root, "schemaVersion");

        if (schemaVersion != ContentManifest.SupportedSchemaVersion)
        {
            throw new InvalidDataException($"The content-set manifest declares schema version {schemaVersion}; this tool supports {ContentManifest.SupportedSchemaVersion}.");
        }

        if (!string.Equals(ReadString(root, "sourceSet"), ContentManifest.SourceSetName, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The content-set manifest declares an unsupported source set.");
        }

        ContentManifest manifest = new(ReadString(root, "clientVersion"), ReadSha256(root, "versionMarkerSha256"), ReadEntries(root));

        int declaredFileCount = ReadInt32(root, "fileCount");
        long declaredLength = ReadInt64(root, "length");

        if (declaredFileCount != manifest.FileCount || declaredLength != manifest.Length)
        {
            throw new InvalidDataException("The content-set manifest summary does not match its entries.");
        }

        return manifest;
    }

    private static ContentManifestEntry[] ReadEntries(JsonElement root)
    {
        if (!root.TryGetProperty("entries", out JsonElement encodedEntries) || encodedEntries.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("The content-set manifest has no entry array.");
        }

        List<ContentManifestEntry> entries = [];
        HashSet<string> sourcePaths = new(StringComparer.Ordinal);
        HashSet<string> pathKeys = new(StringComparer.Ordinal);
        string? previousSourcePath = null;

        foreach (JsonElement encodedEntry in encodedEntries.EnumerateArray())
        {
            if (encodedEntry.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("A content-set manifest entry is not a JSON object.");
            }

            string sourcePath = ReadString(encodedEntry, "sourcePath");
            ContentPath.Validate(sourcePath);

            string pathKey = ReadString(encodedEntry, "pathKey");

            if (!string.Equals(pathKey, ContentPath.ToKey(sourcePath), StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Manifest entry '{sourcePath}' has an inconsistent path key.");
            }

            long length = ReadInt64(encodedEntry, "length");

            if (length < 0)
            {
                throw new InvalidDataException($"Manifest entry '{sourcePath}' declares a negative length.");
            }

            if (previousSourcePath is not null && string.CompareOrdinal(previousSourcePath, sourcePath) >= 0)
            {
                throw new InvalidDataException($"Manifest entries must be ordered ordinally by source path; '{sourcePath}' follows '{previousSourcePath}'.");
            }

            if (!sourcePaths.Add(sourcePath))
            {
                throw new InvalidDataException($"Manifest contains duplicate source path '{sourcePath}'.");
            }

            if (!pathKeys.Add(pathKey))
            {
                throw new InvalidDataException($"Manifest contains a case-insensitive path collision on '{sourcePath}'.");
            }

            entries.Add(new ContentManifestEntry(sourcePath, pathKey, length, ReadSha256(encodedEntry, "sha256"), ReadString(encodedEntry, "signature")));

            previousSourcePath = sourcePath;
        }

        return [.. entries];
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value) || value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"The content-set manifest has no string property '{propertyName}'.");
        }

        return value.GetString() ?? throw new InvalidDataException($"Manifest property '{propertyName}' is null.");
    }

    private static string ReadSha256(JsonElement element, string propertyName)
    {
        string value = ReadString(element, propertyName);

        if (value.Length != Sha256HexLength || !value.All(static character => char.IsAsciiDigit(character) || character is >= 'a' and <= 'f'))
        {
            throw new InvalidDataException($"Manifest property '{propertyName}' is not a lowercase SHA-256 hex digest.");
        }

        return value;
    }

    private static int ReadInt32(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value) || value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int parsed))
        {
            throw new InvalidDataException($"The content-set manifest has no 32-bit integer property '{propertyName}'.");
        }

        return parsed;
    }

    private static long ReadInt64(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value) || value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out long parsed))
        {
            throw new InvalidDataException($"The content-set manifest has no 64-bit integer property '{propertyName}'.");
        }

        return parsed;
    }
}
