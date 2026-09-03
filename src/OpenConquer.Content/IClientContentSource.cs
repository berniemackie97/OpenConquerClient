using System.Diagnostics.CodeAnalysis;

namespace OpenConquer.Content;

/// <summary>
/// Opens client content by its Windows-era virtual path without exposing host filesystem paths to
/// consumers.
/// </summary>
public interface IClientContentSource
{
    /// <summary>
    /// Opens <paramref name="contentPath"/> under the requested <paramref name="mode"/>.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the mode could be satisfied. A source that owns no packages
    /// reports <see langword="false"/> for <see cref="ContentLookupMode.PackageOnly"/> rather than
    /// silently widening the request.
    /// </returns>
    bool TryOpenRead(string contentPath, ContentLookupMode mode, [NotNullWhen(true)] out Stream? stream);

    /// <summary>
    /// Opens <paramref name="contentPath"/> under the requested <paramref name="mode"/> or throws.
    /// </summary>
    /// <exception cref="FileNotFoundException">The mode could not be satisfied.</exception>
    Stream OpenRequiredRead(string contentPath, ContentLookupMode mode);
}
