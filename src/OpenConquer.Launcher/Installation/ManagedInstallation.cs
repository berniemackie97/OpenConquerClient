namespace OpenConquer.Launcher.Installation;

/// <summary>Resolved product paths owned by the installed OpenConquer package.</summary>
internal sealed class ManagedInstallation
{
    private ManagedInstallation(string rootPath, string releaseRootPath, string clientRootPath, string manifestPath, string releaseManifestPath, string releaseSignaturePath, string clientExecutablePath, string? activeReleaseId, string? fallbackReleaseId, ManagedReleaseIdentity release)
    {
        RootPath = rootPath;
        ReleaseRootPath = releaseRootPath;
        ClientRootPath = clientRootPath;
        ManifestPath = manifestPath;
        ReleaseManifestPath = releaseManifestPath;
        ReleaseSignaturePath = releaseSignaturePath;
        ClientExecutablePath = clientExecutablePath;
        ActiveReleaseId = activeReleaseId;
        FallbackReleaseId = fallbackReleaseId;
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

    public string ReleaseRootPath
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

    public string? ActiveReleaseId
    {
        get;
    }

    public string? FallbackReleaseId
    {
        get;
    }

    public ManagedReleaseIdentity Release
    {
        get;
    }

    public static ManagedInstallation Create(string rootPath, string clientRootPath, string manifestPath, string releaseManifestPath, string releaseSignaturePath, string clientExecutablePath, ManagedReleaseIdentity release) => Create(rootPath, rootPath, clientRootPath, manifestPath, releaseManifestPath, releaseSignaturePath, clientExecutablePath, release);
    public static ManagedInstallation Create(string rootPath, string releaseRootPath, string clientRootPath, string manifestPath, string releaseManifestPath, string releaseSignaturePath, string clientExecutablePath, ManagedReleaseIdentity release, string? activeReleaseId = null, string? fallbackReleaseId = null)
    {
        ArgumentNullException.ThrowIfNull(release);
        string root = NormalizeAbsolute(rootPath, nameof(rootPath));
        string releaseRoot = NormalizeAbsolute(releaseRootPath, nameof(releaseRootPath));
        string client = NormalizeAbsolute(clientRootPath, nameof(clientRootPath));
        string manifest = NormalizeAbsolute(manifestPath, nameof(manifestPath));
        string releaseManifest = NormalizeAbsolute(releaseManifestPath, nameof(releaseManifestPath));
        string releaseSignature = NormalizeAbsolute(releaseSignaturePath, nameof(releaseSignaturePath));
        string clientExecutable = NormalizeAbsolute(clientExecutablePath, nameof(clientExecutablePath));

        if (activeReleaseId is null && fallbackReleaseId is not null || activeReleaseId is not null && (!ManagedInstallationManifest.IsValidReleaseId(activeReleaseId)
                || fallbackReleaseId is not null && !ManagedInstallationManifest.IsValidReleaseId(fallbackReleaseId) || string.Equals(activeReleaseId, fallbackReleaseId, StringComparison.Ordinal)))
        {
            throw new ArgumentException("Managed installation release identities do not match the product layout.");
        }

        string expectedReleaseRoot = activeReleaseId is null
            ? root
            : Path.Combine(root, ManagedInstallationManifest.ReleasesRoot, activeReleaseId);
        string expectedClientRoot = Path.Combine(expectedReleaseRoot, ManagedInstallationManifest.ExpectedClientRoot);
        if (!ReleaseTargetRuntime.IsSupported(release.TargetRuntime))
        {
            throw new ArgumentException("The managed release target runtime is invalid.", nameof(release));
        }

        string expectedClientExecutable = Path.Combine(expectedClientRoot, ReleaseTargetRuntime.ClientExecutable(release.TargetRuntime));
        if (!string.Equals(releaseRoot, expectedReleaseRoot, PathComparison)
            || !string.Equals(client, expectedClientRoot, PathComparison) || !string.Equals(manifest, Path.Combine(root, ManagedInstallationManifest.FileName), PathComparison)
            || !string.Equals(releaseManifest, Path.Combine(expectedReleaseRoot, ManagedReleaseManifest.FileName), PathComparison)
            || !string.Equals(releaseSignature, Path.Combine(expectedReleaseRoot, ManagedReleaseSignature.FileName), PathComparison)
            || !string.Equals(clientExecutable, expectedClientExecutable, PathComparison))
        {
            throw new ArgumentException("Managed installation paths do not match the managed product layout.");
        }

        return new ManagedInstallation(root, releaseRoot, client, manifest, releaseManifest, releaseSignature, clientExecutable, activeReleaseId, fallbackReleaseId, release);
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

    private static StringComparison PathComparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
