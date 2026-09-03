using System.Diagnostics.CodeAnalysis;
using System.Text;
using OpenConquer.Content.Wdf;

namespace OpenConquer.Content;

public sealed class RetailClientContentSource : IClientContentSource
{
    private const string PackageConfigurationPath = "ini/package.ini";
    private const int MaximumPackageConfigurationLength = 64 * 1024;

    private readonly ClientContentRoot _looseFiles;
    private readonly Dictionary<string, WdfArchive> _packages;

    private RetailClientContentSource(
        ClientContentRoot looseFiles,
        Dictionary<string, WdfArchive> packages,
        IReadOnlyList<string> missingPackageNames)
    {
        _looseFiles = looseFiles;
        _packages = packages;
        MissingPackageNames = missingPackageNames.ToArray();
    }

    public IReadOnlyList<string> MissingPackageNames
    {
        get;
    }

    public static RetailClientContentSource Open(string rootPath)
    {
        ClientContentRoot looseFiles = new(rootPath);
        Dictionary<string, WdfArchive> packages = new(StringComparer.OrdinalIgnoreCase);
        List<string> missingPackageNames = [];

        if (!looseFiles.TryOpenRead(PackageConfigurationPath, out Stream? packageConfigurationStream))
        {
            return new RetailClientContentSource(looseFiles, packages, missingPackageNames);
        }

        using (packageConfigurationStream)
        {
            byte[] bytes = ContentRead.ReadBytes(
                packageConfigurationStream,
                PackageConfigurationPath,
                MaximumPackageConfigurationLength
            );

            string text = Encoding.Latin1.GetString(bytes);

            foreach (string packageName in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                string prefix = GetPackagePrefix(packageName);

                if (packages.ContainsKey(prefix))
                {
                    throw new InvalidDataException($"'{PackageConfigurationPath}' declares package prefix '{prefix}' more than once.");
                }

                if (!looseFiles.TryResolveFile(packageName, out string? packagePath))
                {
                    missingPackageNames.Add(packageName);
                    continue;
                }

                packages.Add(prefix, WdfArchive.Open(packagePath));
            }
        }

        return new RetailClientContentSource(looseFiles, packages, missingPackageNames);
    }

    public bool TryOpenRead(string contentPath, [NotNullWhen(true)] out Stream? stream)
    {
        if (_looseFiles.TryOpenRead(contentPath, out stream))
        {
            return true;
        }

        string normalizedPath = NormalizeVirtualPath(contentPath);
        int separatorIndex = normalizedPath.IndexOf('/');
        string prefix = separatorIndex < 0 ? normalizedPath : normalizedPath[..separatorIndex];

        return _packages.TryGetValue(prefix, out WdfArchive? package)
            && package.TryOpenRead(normalizedPath, out stream);
    }

    public Stream OpenRequiredRead(string contentPath)
    {
        if (TryOpenRead(contentPath, out Stream? stream))
        {
            return stream;
        }

        throw new FileNotFoundException($"Client content file '{contentPath}' was not found in the retail content source.");
    }

    private static string GetPackagePrefix(string packageName)
    {
        string normalizedName = NormalizeVirtualPath(packageName);
        int separatorIndex = normalizedName.LastIndexOf('/');
        string fileName = separatorIndex < 0 ? normalizedName : normalizedName[(separatorIndex + 1)..];

        if (!fileName.EndsWith(".wdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"'{PackageConfigurationPath}' contains unsupported package name '{packageName}'.");
        }

        int extensionIndex = fileName.IndexOf('.');
        string prefix = extensionIndex < 0 ? fileName : fileName[..extensionIndex];

        if (prefix.Length == 0)
        {
            throw new InvalidDataException($"'{PackageConfigurationPath}' contains invalid package name '{packageName}'.");
        }

        return prefix;
    }

    private static string NormalizeVirtualPath(string contentPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentPath);

        string path = contentPath.Trim().Replace('\\', '/');

        if (path.Length > 255 || path[0] == '/' || Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException("Client content paths must be relative.", nameof(contentPath));
        }

        string[] segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0
            || segments.Any(segment =>
                segment is "." or ".."
                || segment.Contains(':', StringComparison.Ordinal)
                || segment.Contains('\0', StringComparison.Ordinal)))
        {
            throw new ArgumentException($"Client content path '{contentPath}' is invalid.", nameof(contentPath));
        }

        return string.Join('/', segments);
    }
}
