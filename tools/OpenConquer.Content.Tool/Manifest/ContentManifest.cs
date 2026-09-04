namespace OpenConquer.Content.Tool.Manifest;

/// <summary>
/// The deterministic identity and integrity catalog for a generated content set.
/// </summary>
internal sealed class ContentManifest
{
    public const int SupportedSchemaVersion = 2;

    public const string SourceSetName = "retail-5517";

    public ContentManifest(string clientVersion, string versionMarkerSha256, IReadOnlyList<ContentManifestEntry> entries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(versionMarkerSha256);
        ArgumentNullException.ThrowIfNull(entries);

        ClientVersion = clientVersion;
        VersionMarkerSha256 = versionMarkerSha256;
        Entries = entries;
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

    public int FileCount => Entries.Count;

    public long Length => Entries.Sum(entry => entry.Length);
}
