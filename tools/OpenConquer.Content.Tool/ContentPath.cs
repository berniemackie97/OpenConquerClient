using System.Buffers;

namespace OpenConquer.Content.Tool;

/// <summary>
/// Rules for the slash-normalized relative paths that identify content-set payload files.
/// </summary>
/// <remarks>
/// Manifests and payload trees are untrusted input, so a path is validated before it is ever joined
/// to a host directory. Rejecting rooted, traversing, and drive-qualified forms is what keeps an
/// extracted payload inside its own root.
/// </remarks>
internal static class ContentPath
{
    /// <summary>Upper bound on a payload path, matching the Windows-era MAX_PATH budget.</summary>
    private const int MaximumLength = 260;

    /// <summary>
    /// Characters refused inside a path segment.
    /// </summary>
    /// <remarks>
    /// Fixed rather than taken from <see cref="Path.GetInvalidFileNameChars"/>, whose result differs
    /// between Windows and Unix. A platform-dependent rule would let a content set import on one host
    /// and fail verification on another, which is exactly the non-determinism this catalog exists to
    /// prevent. The set is the Windows-invalid characters, so validation is the stricter of the two
    /// everywhere.
    /// </remarks>
    private static readonly SearchValues<char> s_invalidSegmentCharacters =
        SearchValues.Create("\u0000\u0001\u0002\u0003\u0004\u0005\u0006\u0007\u0008\u0009\u000A\u000B\u000C\u000D\u000E\u000F\u0010\u0011\u0012\u0013\u0014\u0015\u0016\u0017\u0018\u0019\u001A\u001B\u001C\u001D\u001E\u001F\"<>|:*?\\/");

    public static void Validate(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath)
            || sourcePath.Length > MaximumLength
            || sourcePath.Contains('\\', StringComparison.Ordinal)
            || sourcePath[0] == '/')
        {
            throw new InvalidDataException($"Content path '{sourcePath}' is not a valid relative payload path.");
        }

        foreach (string segment in sourcePath.Split('/'))
        {
            if (segment.Length == 0
                || segment is "." or ".."
                || segment.EndsWith('.')
                || segment.EndsWith(' ')
                || segment.AsSpan().IndexOfAny(s_invalidSegmentCharacters) >= 0)
            {
                throw new InvalidDataException($"Content path '{sourcePath}' is not a valid relative payload path.");
            }
        }
    }

    /// <summary>
    /// Produces the case-folded lookup key used to detect case-insensitive collisions.
    /// </summary>
    public static string ToKey(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        return sourcePath.ToLowerInvariant();
    }

    /// <summary>
    /// Converts a validated content path into a host-relative path for the current platform.
    /// </summary>
    public static string ToHostRelativePath(string sourcePath)
    {
        Validate(sourcePath);

        return sourcePath.Replace('/', Path.DirectorySeparatorChar);
    }
}
