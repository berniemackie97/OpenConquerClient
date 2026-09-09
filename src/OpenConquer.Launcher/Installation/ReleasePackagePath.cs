using System.Text;

namespace OpenConquer.Launcher.Installation;

/// <summary>Validates manifest paths using a conservative cross-platform identity.</summary>
internal static class ReleasePackagePath
{
    public const int MaximumLength = 512;

    public static bool IsValid(string? path)
    {
        if (string.IsNullOrEmpty(path) || path.Length > MaximumLength || path[0] == '/' || path[^1] == '/')
        {
            return false;
        }

        foreach (char character in path)
        {
            if (character == '\0' || character == '\\' || char.IsControl(character))
            {
                return false;
            }
        }

        foreach (string segment in path.Split('/'))
        {
            if (!IsPortableSegment(segment))
            {
                return false;
            }
        }

        return !Path.IsPathFullyQualified(path);
    }

    public static string PortableIdentity(string path)
    {
        if (!IsValid(path))
        {
            throw new ArgumentException("The release package path is invalid.", nameof(path));
        }

        return path.Normalize(NormalizationForm.FormC).ToUpperInvariant();
    }

    public static string Combine(string rootPath, string relativePath)
    {
        if (!IsValid(relativePath))
        {
            throw new ArgumentException("The release package path is invalid.", nameof(relativePath));
        }

        string path = rootPath;
        foreach (string segment in relativePath.Split('/'))
        {
            path = Path.Combine(path, segment);
        }

        return path;
    }

    private static bool IsPortableSegment(string segment)
    {
        if (segment.Length is 0 or > 255 || Encoding.UTF8.GetByteCount(segment) > 255 || ContainsInvalidSurrogate(segment)
            || segment is "." or ".." || segment.EndsWith(' ') || segment.EndsWith('.') || !segment.IsNormalized(NormalizationForm.FormC)
            || segment.IndexOfAny(['<', '>', ':', '"', '|', '?', '*']) >= 0)
        {
            return false;
        }

        string baseName = segment.Split('.')[0].ToUpperInvariant();
        if (baseName is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$")
        {
            return false;
        }

        return baseName.Length != 4 || !(baseName.StartsWith("COM", StringComparison.Ordinal) || baseName.StartsWith("LPT", StringComparison.Ordinal))
                                    || baseName[3] is not (>= '1' and <= '9') and not ('¹' or '²' or '³');
    }

    private static bool ContainsInvalidSurrogate(string value)
    {
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (!char.IsSurrogate(character))
            {
                continue;
            }

            if (!char.IsHighSurrogate(character) || index + 1 >= value.Length || !char.IsLowSurrogate(value[++index]))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>Tracks a bounded package tree using the same identity on every platform.</summary>
internal sealed class ReleasePackagePathTopology
{
    private readonly int _maximumDirectoryCount;
    private readonly Node _root = new();
    private int _directoryCount;

    public ReleasePackagePathTopology(int maximumDirectoryCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumDirectoryCount);
        _maximumDirectoryCount = maximumDirectoryCount;
    }

    public bool TryAddFile(string path)
    {
        if (!ReleasePackagePath.IsValid(path))
        {
            return false;
        }

        string[] segments = path.Split('/');
        Node current = _root;
        for (int index = 0; index < segments.Length; index++)
        {
            string segment = segments[index];
            string identity = segment.ToUpperInvariant();
            bool isFile = index == segments.Length - 1;
            if (isFile)
            {
                return !current.Directories.ContainsKey(identity) &&
                    current.Files.Add(identity);
            }

            if (current.Files.Contains(identity))
            {
                return false;
            }

            if (!current.Directories.TryGetValue(identity, out DirectoryEntry? directory))
            {
                if (_directoryCount == _maximumDirectoryCount)
                {
                    return false;
                }

                directory = new DirectoryEntry(segment);
                current.Directories.Add(identity, directory);
                _directoryCount++;
            }
            else if (!string.Equals(directory.Name, segment, StringComparison.Ordinal))
            {
                return false;
            }

            current = directory.Children;
        }

        throw new InvalidOperationException("A validated release path has no segments.");
    }

    private sealed class Node
    {
        public Dictionary<string, DirectoryEntry> Directories
        {
            get;
        } =
            new(StringComparer.Ordinal);

        public HashSet<string> Files { get; } = new(StringComparer.Ordinal);
    }

    private sealed class DirectoryEntry(string name)
    {
        public string Name { get; } = name;

        public Node Children { get; } = new();
    }
}
