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
    private const int StreamBufferLength = 81920;

    private readonly ClientContentRoot _contentRoot;
    private readonly PackagedClientContentSource _packagedContentSource;

    private ImportSourceRoot(ClientContentRoot contentRoot, PackagedClientContentSource packagedContentSource, string clientVersion, string versionMarkerSha256)
    {
        _contentRoot = contentRoot;
        _packagedContentSource = packagedContentSource;
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

        ClientContentRoot contentRoot = new(normalizedRootPath);
        PackagedClientContentSource packagedContentSource = PackagedClientContentSource.Open(normalizedRootPath);

        return new ImportSourceRoot(contentRoot, packagedContentSource, clientVersion, Convert.ToHexStringLower(SHA256.HashData(versionBytes)));
    }

    /// <summary>
    /// Opens one runtime content requirement using its declared loose/package lookup contract.
    /// </summary>
    public Stream OpenRequiredRead(ClientContentRequirement requirement, out string sourcePath)
    {
        ArgumentNullException.ThrowIfNull(requirement);

        if (requirement.LookupMode != ContentLookupMode.PackageOnly && _contentRoot.TryResolveFile(requirement.ContentPath, out string? loosePath))
        {
            FileInfo looseFile = HostFileSystemGuard.RequireFile(loosePath, "client content file");
            sourcePath = GetSourceRelativePath(looseFile);

            return new FileStream(looseFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, StreamBufferLength, FileOptions.SequentialScan);
        }

        if (requirement.LookupMode == ContentLookupMode.LooseOnly)
        {
            throw new FileNotFoundException($"Client content file '{requirement.ContentPath}' was not found as a loose file under '{RootPath}'.");
        }

        sourcePath = requirement.ContentPath.Replace('\\', '/');

        return _packagedContentSource.OpenRequiredRead(requirement.ContentPath, ContentLookupMode.PackageOnly);
    }

    private string GetSourceRelativePath(FileInfo file)
    {
        return Path.GetRelativePath(RootPath, file.FullName).Replace('\\', '/');
    }
}
