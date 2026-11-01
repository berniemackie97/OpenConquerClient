namespace OpenConquer.Content.Tool.Manifest;

/// <summary>
/// Identifies a generated content set and lists the files in it with their hashes.
/// </summary>
internal sealed class ContentManifest
{
    public const int SupportedSchemaVersion = 2;
    public const int AvailableMapsSchemaVersion = 3;

    public const string SourceSetName = "retail-5517";

    public ContentManifest(string clientVersion, string versionMarkerSha256, IReadOnlyList<ContentManifestEntry> entries,
        IReadOnlyList<MissingMapArchive>? missingMapArchives = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(versionMarkerSha256);
        ArgumentNullException.ThrowIfNull(entries);

        ClientVersion = clientVersion;
        VersionMarkerSha256 = versionMarkerSha256;
        Entries = entries;
        MissingMapArchives = missingMapArchives;
    }

    public string ClientVersion
    {
        get;
    }

    public string VersionMarkerSha256
    {
        get;
    }

    public IReadOnlyList<ContentManifestEntry> Entries
    {
        get;
    }

    public IReadOnlyList<MissingMapArchive>? MissingMapArchives { get; }

    public int SchemaVersion
    {
        get
        {
            if (MissingMapArchives is null)
            {
                return SupportedSchemaVersion;
            }

            return AvailableMapsSchemaVersion;
        }
    }

    public int FileCount => Entries.Count;

    public long Length => Entries.Sum(entry => entry.Length);
}
