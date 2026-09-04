using System.Buffers;

namespace OpenConquer.Content.Tool;

internal static class ContentPath
{
    private const int MaximumLength = 260;


    private static readonly SearchValues<char> s_invalidSegmentCharacters = SearchValues.Create("\u0000\u0001\u0002\u0003\u0004\u0005\u0006\u0007\u0008\u0009\u000A\u000B\u000C\u000D\u000E\u000F\u0010\u0011\u0012\u0013\u0014\u0015\u0016\u0017\u0018\u0019\u001A\u001B\u001C\u001D\u001E\u001F\"<>|:*?\\/");

    public static void Validate(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || sourcePath.Length > MaximumLength || sourcePath.Contains('\\', StringComparison.Ordinal) || sourcePath[0] == '/')
        {
            throw new InvalidDataException($"Content path '{sourcePath}' is not a valid relative payload path.");
        }

        foreach (string segment in sourcePath.Split('/'))
        {
            if (segment.Length == 0 || segment is "." or ".." || segment.EndsWith('.') || segment.EndsWith(' ') || segment.AsSpan().IndexOfAny(s_invalidSegmentCharacters) >= 0)
            {
                throw new InvalidDataException($"Content path '{sourcePath}' is not a valid relative payload path.");
            }
        }
    }

    /// <summary>
    /// Produces the case folded lookup key used to detect case insensitive collisions.
    /// </summary>
    public static string ToKey(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        return sourcePath.ToLowerInvariant();
    }

    /// <summary>
    /// Converts a validated content path into a host relative path for the current platform.
    /// </summary>
    public static string ToHostRelativePath(string sourcePath)
    {
        Validate(sourcePath);

        return sourcePath.Replace('/', Path.DirectorySeparatorChar);
    }
}
