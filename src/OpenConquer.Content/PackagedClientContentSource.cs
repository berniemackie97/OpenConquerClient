using System.Diagnostics.CodeAnalysis;
using System.Text;
using OpenConquer.Content.Wdf;

namespace OpenConquer.Content;

/// <summary>
/// Composes loose retail files with the WDF packages declared by <c>ini/package.ini</c>.
/// </summary>
/// <remarks>
/// Registration follows <c>GraphicData.dll!GraphicData_OpenPackagesFromPackageIni</c>
/// (<c>0x1001A390</c>) and <c>TqPackageWdf.dll!TqPackagesOpen</c> (<c>0x10003D30</c>). Every
/// declaration outcome retail tolerates is tolerated here and surfaced through
/// <see cref="PackageRegistrations"/>; none of them is fatal, because the native reader returns
/// <see langword="void"/> and its caller discards <c>TqPackagesOpen</c>'s result at
/// <c>0x1001A406</c>.
/// </remarks>
/// <remarks>
/// This type holds no operating-system handles and is therefore not disposable: a
/// <see cref="WdfArchive"/> reads its index once at open time and then opens a fresh stream per
/// request, which the caller owns. A future archive implementation that retains a handle must make
/// both types disposable rather than relying on finalization.
/// </remarks>
public sealed class PackagedClientContentSource : IClientContentSource
{
    /// <summary>The declaration file this source registers packages from.</summary>
    public const string PackageConfigurationPath = "ini/package.ini";

    private const int MaximumPackageConfigurationLength = 64 * 1024;

    private readonly ClientContentRoot _looseFiles;
    private readonly Dictionary<string, WdfArchive> _packagesByPrefix;

    private PackagedClientContentSource(
        ClientContentRoot looseFiles,
        Dictionary<string, WdfArchive> packagesByPrefix,
        IReadOnlyList<WdfPackageRegistration> packageRegistrations)
    {
        _looseFiles = looseFiles;
        _packagesByPrefix = packagesByPrefix;
        PackageRegistrations = packageRegistrations;
    }

    /// <summary>
    /// Every <c>ini/package.ini</c> declaration in file order with its resolved outcome.
    /// </summary>
    /// <remarks>
    /// Empty when the declaration file is absent. Native logs and continues with zero packages in
    /// that case (<c>0x1001A3B0</c>).
    /// </remarks>
    public IReadOnlyList<WdfPackageRegistration> PackageRegistrations
    {
        get;
    }

    /// <summary>
    /// Opens a retail content root and registers the packages it declares.
    /// </summary>
    public static PackagedClientContentSource Open(string rootPath)
    {
        ClientContentRoot looseFiles = new(rootPath);
        Dictionary<string, WdfArchive> packagesByPrefix = new(StringComparer.Ordinal);
        List<WdfPackageRegistration> registrations = [];

        if (!looseFiles.TryOpenRead(PackageConfigurationPath, ContentLookupMode.LooseOnly, out Stream? declarationStream))
        {
            return new PackagedClientContentSource(looseFiles, packagesByPrefix, registrations);
        }

        using (declarationStream)
        {
            foreach (string declaredName in ReadDeclaredPackageNames(declarationStream))
            {
                registrations.Add(RegisterPackage(looseFiles, packagesByPrefix, declaredName));
            }
        }

        return new PackagedClientContentSource(looseFiles, packagesByPrefix, registrations);
    }

    /// <inheritdoc />
    public bool TryOpenRead(string contentPath, ContentLookupMode mode, [NotNullWhen(true)] out Stream? stream)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown content lookup mode.");
        }

        if (mode != ContentLookupMode.PackageOnly
            && _looseFiles.TryOpenRead(contentPath, ContentLookupMode.LooseOnly, out stream))
        {
            return true;
        }

        if (mode == ContentLookupMode.LooseOnly)
        {
            stream = null;
            return false;
        }

        string normalizedPath = NormalizeVirtualPath(contentPath);
        string prefix = WdfPackagePrefix.FromVirtualPath(normalizedPath);

        if (_packagesByPrefix.TryGetValue(prefix, out WdfArchive? package))
        {
            return package.TryOpenRead(normalizedPath, out stream);
        }

        stream = null;
        return false;
    }

    /// <inheritdoc />
    public Stream OpenRequiredRead(string contentPath, ContentLookupMode mode)
    {
        if (TryOpenRead(contentPath, mode, out Stream? stream))
        {
            return stream;
        }

        throw new FileNotFoundException(
            $"Client content file '{contentPath}' was not found in the packaged content source using {mode} lookup."
        );
    }

    /// <summary>
    /// Reads <c>ini/package.ini</c> the way retail does.
    /// </summary>
    /// <remarks>
    /// Native parses with <c>fscanf(stream, "%s\n", buffer)</c> at <c>0x1001A3F7</c>, so the file
    /// is a stream of whitespace-delimited tokens rather than lines or INI sections. Two names on
    /// one line declare two packages.
    /// </remarks>
    private static string[] ReadDeclaredPackageNames(Stream declarationStream)
    {
        byte[] bytes = ContentRead.ReadBytes(
            declarationStream,
            PackageConfigurationPath,
            MaximumPackageConfigurationLength
        );

        return Encoding.Latin1
            .GetString(bytes)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static WdfPackageRegistration RegisterPackage(
        ClientContentRoot looseFiles,
        Dictionary<string, WdfArchive> packagesByPrefix,
        string declaredName)
    {
        string prefix = WdfPackagePrefix.FromDeclaredPackageName(declaredName);

        // TqPackagesOpen looks the prefix up at 0x10003DE1 and returns at 0x10003DEF when it is
        // already registered. The first declaration wins; the duplicate is discarded silently.
        if (packagesByPrefix.ContainsKey(prefix))
        {
            return new WdfPackageRegistration(declaredName, prefix, WdfPackageRegistrationOutcome.DuplicatePrefix);
        }

        if (!looseFiles.TryResolveFile(declaredName, out string? packagePath))
        {
            return new WdfPackageRegistration(declaredName, prefix, WdfPackageRegistrationOutcome.FileNotFound);
        }

        packagesByPrefix.Add(prefix, WdfArchive.Open(packagePath));

        return new WdfPackageRegistration(declaredName, prefix, WdfPackageRegistrationOutcome.Registered);
    }

    /// <summary>
    /// Validates and normalizes a virtual path before it is hashed or routed.
    /// </summary>
    /// <remarks>
    /// Retail normalization (<c>0x10009890</c>) only folds ASCII case and maps <c>'\'</c> to
    /// <c>'/'</c>; it performs no structural validation. The additional rejections here are a
    /// deliberate containment guard. They only ever reject inputs, so no path retail would have
    /// resolved is silently rerouted.
    /// </remarks>
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
