using System.Diagnostics.CodeAnalysis;

namespace OpenConquer.Launcher.Installation;

/// <summary>A normalized, explicitly selected absolute installation directory.</summary>
internal sealed class InstallationRoot
{
    private InstallationRoot(string path)
    {
        Path = path;
    }

    public string Path
    {
        get;
    }

    public static bool TryCreate(string? path, [NotNullWhen(true)] out InstallationRoot? root)
    {
        root = null;
        if (string.IsNullOrWhiteSpace(path) || !System.IO.Path.IsPathFullyQualified(path) ||
            path.AsSpan().ContainsAny(System.IO.Path.GetInvalidPathChars()))
        {
            return false;
        }

        try
        {
            root = new InstallationRoot(System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(path)));
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (PathTooLongException)
        {
            return false;
        }
    }
}
