using System.Security.Cryptography;
using System.Text;

namespace OpenConquer.Content.Tool.Import;

/// <summary>
/// An authorized retail snapshot, validated as a 5517 source before anything is read from it.
/// </summary>
internal sealed class ImportSourceRoot
{
    private const string ExpectedClientVersion = "5517";
    private const string VersionMarkerFileName = "version.dat";

    private readonly ClientContentRoot _contentRoot;

    private ImportSourceRoot(ClientContentRoot contentRoot, string clientVersion, string versionMarkerSha256)
    {
        _contentRoot = contentRoot;
        RootPath = contentRoot.RootPath;
        ClientVersion = clientVersion;
        VersionMarkerSha256 = versionMarkerSha256;
    }

    public string RootPath
    {
        get;
    }

    public string ClientVersion
    {
        get;
    }

    public string VersionMarkerSha256
    {
        get;
    }

    /// <summary>
    /// Validates <paramref name="rootPath"/> and reads its identity marker.
    /// </summary>
    public static ImportSourceRoot Open(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        string normalizedRootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        HostFileSystemGuard.RequireDirectory(normalizedRootPath, "retail source root");

        string versionMarkerPath = Path.Combine(normalizedRootPath, VersionMarkerFileName);
        FileInfo versionMarker = HostFileSystemGuard.RequireFile(versionMarkerPath, "retail version marker");

        if (versionMarker.Length != ExpectedClientVersion.Length)
        {
            throw new InvalidDataException($"The retail version marker is {versionMarker.Length} bytes; expected the {ExpectedClientVersion.Length}-byte '{ExpectedClientVersion}' value.");
        }

        byte[] versionBytes = File.ReadAllBytes(versionMarkerPath);
        string clientVersion = Encoding.ASCII.GetString(versionBytes);

        if (!string.Equals(clientVersion, ExpectedClientVersion, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Expected retail version {ExpectedClientVersion}, but the source declares '{clientVersion}'.");
        }

        return new ImportSourceRoot(new ClientContentRoot(normalizedRootPath), clientVersion, Convert.ToHexStringLower(SHA256.HashData(versionBytes)));
    }

    /// <summary>
    /// Resolves a closure content path to a validated absolute source file.
    /// </summary>
    public FileInfo ResolveRequiredFile(string contentPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentPath);

        return HostFileSystemGuard.RequireFile(_contentRoot.ResolveRequiredFile(contentPath), "client content file");
    }

    /// <summary>
    /// Returns the retail path with the case the source actually uses.
    /// </summary>
    public string GetSourceRelativePath(FileInfo file)
    {
        ArgumentNullException.ThrowIfNull(file);

        return Path.GetRelativePath(RootPath, file.FullName).Replace('\\', '/');
    }
}
