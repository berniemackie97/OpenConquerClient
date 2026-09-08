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
            if (segment.Length == 0 || segment is "." or ".." || segment.EndsWith(' ') || segment.EndsWith('.'))
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
}
