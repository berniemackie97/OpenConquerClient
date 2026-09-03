using System.Diagnostics.CodeAnalysis;

namespace OpenConquer.Content;

/// <summary>
/// Opens client content by its Windows-era virtual path without exposing host filesystem paths to
/// consumers.
/// </summary>
public interface IClientContentSource
{
    bool TryOpenRead(string contentPath, [NotNullWhen(true)] out Stream? stream);

    Stream OpenRequiredRead(string contentPath);
}
