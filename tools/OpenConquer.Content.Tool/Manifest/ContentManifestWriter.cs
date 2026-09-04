using System.Text.Json;

namespace OpenConquer.Content.Tool.Manifest;

/// <summary>
/// Serializes a <see cref="ContentManifest"/> to canonical JSON.
/// </summary>
internal static class ContentManifestWriter
{
    private static readonly JsonWriterOptions s_writerOptions = new()
    {
        Indented = true,
        IndentCharacter = ' ',
        IndentSize = 2,
        NewLine = "\n",
        SkipValidation = false,
    };

    public static void Write(Stream destination, ContentManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(manifest);

        using Utf8JsonWriter writer = new(destination, s_writerOptions);

        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", ContentManifest.SupportedSchemaVersion);
        writer.WriteString("sourceSet", ContentManifest.SourceSetName);
        writer.WriteString("clientVersion", manifest.ClientVersion);
        writer.WriteString("versionMarkerSha256", manifest.VersionMarkerSha256);
        writer.WriteNumber("fileCount", manifest.FileCount);
        writer.WriteNumber("length", manifest.Length);
        writer.WriteStartArray("entries");

        foreach (ContentManifestEntry entry in manifest.Entries)
        {
            writer.WriteStartObject();
            writer.WriteString("sourcePath", entry.SourcePath);
            writer.WriteString("pathKey", entry.PathKey);
            writer.WriteNumber("length", entry.Length);
            writer.WriteString("sha256", entry.Sha256);
            writer.WriteString("signature", entry.Signature);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();

        destination.WriteByte((byte)'\n');
    }
}
