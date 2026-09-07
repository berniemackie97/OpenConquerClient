namespace OpenConquer.Launcher.Installation;

/// <summary>Resolved product paths owned by the installed OpenConquer package.</summary>
internal sealed class ManagedInstallation
{
    private ManagedInstallation(string rootPath, string clientRootPath, string manifestPath, string releaseManifestPath, string releaseSignaturePath, string clientExecutablePath, ManagedReleaseIdentity release)
    {
        RootPath = rootPath;
        ClientRootPath = clientRootPath;
        ManifestPath = manifestPath;
        ReleaseManifestPath = releaseManifestPath;
        ReleaseSignaturePath = releaseSignaturePath;
        ClientExecutablePath = clientExecutablePath;
        Release = release;
    }

    public string RootPath
    {
        get;
    }

    public string ClientRootPath
    {
        get;
    }

    public string ManifestPath
    {
        get;
    }

    public string ReleaseManifestPath
    {
        get;
    }

    public string ReleaseSignaturePath
    {
        get;
    }

    public string ClientExecutablePath
    {
        get;
    }

    public ManagedReleaseIdentity Release
    {
        get;
    }

    public static ManagedInstallation Create(string rootPath, string clientRootPath, string manifestPath, string releaseManifestPath, string releaseSignaturePath, string clientExecutablePath, ManagedReleaseIdentity release)
    {
        ArgumentNullException.ThrowIfNull(release);
        string root = NormalizeAbsolute(rootPath, nameof(rootPath));
        string client = NormalizeAbsolute(clientRootPath, nameof(clientRootPath));
        string manifest = NormalizeAbsolute(manifestPath, nameof(manifestPath));
        string releaseManifest = NormalizeAbsolute(releaseManifestPath, nameof(releaseManifestPath));
        string releaseSignature = NormalizeAbsolute(releaseSignaturePath, nameof(releaseSignaturePath));
        string clientExecutable = NormalizeAbsolute(clientExecutablePath, nameof(clientExecutablePath));

        if (!IsContainedChild(root, client) || !IsContainedChild(root, manifest) ||
            !IsContainedChild(root, releaseManifest) || !IsContainedChild(root, releaseSignature) ||
            !IsContainedChild(client, clientExecutable))
        {
            throw new ArgumentException("Managed installation paths must remain inside the product root.");
        }

        return new ManagedInstallation(root, client, manifest, releaseManifest, releaseSignature, clientExecutable, release);
    }

    private static string NormalizeAbsolute(string path, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, parameterName);
        if (!Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException("Managed installation paths must be fully qualified.", parameterName);
        }

        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException("Managed installation path is invalid.", parameterName, exception);
        }
        catch (NotSupportedException exception)
        {
            throw new ArgumentException("Managed installation path is unsupported.", parameterName, exception);
        }
        catch (PathTooLongException exception)
        {
            throw new ArgumentException("Managed installation path is too long.", parameterName, exception);
        }
    }

    private static bool IsContainedChild(string root, string child)
    {
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        string rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar) || root.EndsWith(Path.AltDirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        return !string.Equals(root, child, comparison) && child.StartsWith(rootWithSeparator, comparison);
    }
}
