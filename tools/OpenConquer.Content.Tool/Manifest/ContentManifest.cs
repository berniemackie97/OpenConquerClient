namespace OpenConquer.Content.Tool.Manifest;

/// <summary>
/// The deterministic identity and integrity catalog for a generated content set.
/// </summary>
internal sealed class ContentManifest
{
    /// <summary>
    /// The schema this tool reads and writes.
    /// </summary>
    /// <remarks>
    /// Version 2 replaced the family-scoped inventory with a closure-scoped one: a content set now
    /// holds exactly the paths <see cref="ClientContentClosure"/> resolves, so the per-family
    /// summary and the per-entry <c>disposition</c> field no longer describe anything.
    /// </remarks>
    public const int SupportedSchemaVersion = 2;

    /// <summary>The immutable source-set identifier.</summary>
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

    /// <summary>The retail build string read from <c>version.dat</c>.</summary>
    public string ClientVersion
    {
        get;
    }

    /// <summary>SHA-256 of <c>version.dat</c>, binding the set to one retail snapshot.</summary>
    public string VersionMarkerSha256
    {
        get;
    }

    /// <summary>Payload entries ordered ordinally by <see cref="ContentManifestEntry.SourcePath"/>.</summary>
    public IReadOnlyList<ContentManifestEntry> Entries
    {
        get;
    }

    public int FileCount => Entries.Count;

    public long Length => Entries.Sum(entry => entry.Length);
}
