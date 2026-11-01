namespace OpenConquer.Content.Ani;

/// <summary>
/// Resolves animation sections by exact name, loading each ANI index file once per content path.
/// </summary>
public sealed class AniSectionResolver
{
    private const int MaximumIndexPathLength = 260;

    private readonly IClientContentSource _source;
    private readonly ContentLookupMode _lookupMode;
    private readonly Dictionary<string, AniIndexFile> _indexes = new(StringComparer.OrdinalIgnoreCase);

    public AniSectionResolver(IClientContentSource source, ContentLookupMode lookupMode)
    {
        ArgumentNullException.ThrowIfNull(source);

        _source = source;
        _lookupMode = lookupMode;
    }

    /// <summary>
    /// Returns the section named <paramref name="sectionName"/> in the index at <paramref name="indexPath"/>.
    /// </summary>
    /// <exception cref="InvalidDataException">
    /// The index has no section with exactly that name, or the section has no frames.
    /// </exception>
    public AniIndexSection Resolve(string indexPath, string sectionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);

        string normalizedPath = ClientContentPath.NormalizeVirtualPath(indexPath, nameof(indexPath), MaximumIndexPathLength);
        if (!_indexes.TryGetValue(normalizedPath, out AniIndexFile? index))
        {
            index = AniIndexFile.Load(_source, normalizedPath, _lookupMode);
            _indexes.Add(normalizedPath, index);
        }

        if (!index.TryGetSection(sectionName, out AniIndexSection? section))
        {
            throw new InvalidDataException($"Animation index '{normalizedPath}' does not define section [{sectionName}].");
        }

        // Index lookup is by name hash, so a different name with the same hash must not satisfy the reference.
        if (!string.Equals(section.Name, sectionName, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Animation section [{sectionName}] in '{normalizedPath}' resolves to the colliding section [{section.Name}].");
        }

        if (section.FrameCount == 0)
        {
            throw new InvalidDataException($"Animation section [{sectionName}] in '{normalizedPath}' has no frames.");
        }

        return section;
    }
}
