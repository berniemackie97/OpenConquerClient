using System.Diagnostics.CodeAnalysis;
using System.Text;
using OpenConquer.Content.Wdf;

namespace OpenConquer.Content;

/// <summary>
/// Composes loose retail files with the WDF packages declared by <c>ini/package.ini</c>.
/// </summary>
/// <remarks>
/// Registration follows <c>GraphicData.dll!GraphicData_OpenPackagesFromPackageIni</c>
/// (<c>0x1001A390</c>) and <c>TqPackageWdf.dll!TqPackagesOpen</c> (<c>0x10003D30</c>).
/// Prefix ownership is established before the declared archive is opened. Missing, unreadable, or
/// structurally invalid archives therefore retain their prefixes without an available
/// <see cref="WdfArchive"/>, and later declarations with the same prefix remain duplicates. This
/// preserves the verified native first-wins registration behavior while validating legacy archive
/// bytes as untrusted modern input.
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
    private const int MaximumVirtualPathLength = 255;

    private readonly ClientContentRoot _looseFiles;
    private readonly Dictionary<string, WdfArchive> _packagesByPrefix;

    private PackagedClientContentSource(ClientContentRoot looseFiles, Dictionary<string, WdfArchive> packagesByPrefix, IReadOnlyList<WdfPackageRegistration> packageRegistrations)
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

    public static PackagedClientContentSource Open(string rootPath)
    {
        ClientContentRoot looseFiles = new(rootPath);

        // Native registration identity is independent of whether the declared WDF file opens
        // successfully. A missing or unavailable package still owns its prefix and therefore
        // blocks later declarations with the same prefix.
        HashSet<string> registeredPrefixes = new(StringComparer.Ordinal);
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
                registrations.Add(RegisterPackage(looseFiles, registeredPrefixes, packagesByPrefix, declaredName));
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

        if (mode != ContentLookupMode.PackageOnly && _looseFiles.TryOpenRead(contentPath, ContentLookupMode.LooseOnly, out stream))
        {
            return true;
        }

        if (mode == ContentLookupMode.LooseOnly)
        {
            stream = null;
            return false;
        }

        string normalizedPath = ClientContentPath.NormalizeVirtualPath(contentPath, nameof(contentPath), MaximumVirtualPathLength);
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

        throw new FileNotFoundException($"Client content file '{contentPath}' was not found in the packaged content source using {mode} lookup.");
    }

    private static string[] ReadDeclaredPackageNames(Stream declarationStream)
    {
        byte[] bytes = ContentRead.ReadBytes(declarationStream, PackageConfigurationPath, MaximumPackageConfigurationLength);

        return Encoding.Latin1.GetString(bytes).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static WdfPackageRegistration RegisterPackage(ClientContentRoot looseFiles, HashSet<string> registeredPrefixes, Dictionary<string, WdfArchive> packagesByPrefix, string declaredName)
    {
        string prefix = WdfPackagePrefix.FromDeclaredPackageName(declaredName);

        // TqPackagesOpen looks the prefix up at 0x10003DE1 and returns at 0x10003DEF when it is
        // already registered. Prefix ownership is established before the package file is opened.
        if (!registeredPrefixes.Add(prefix))
        {
            return new WdfPackageRegistration(declaredName, prefix, WdfPackageRegistrationOutcome.DuplicatePrefix);
        }

        // sub_100014F0 keeps the native package object registered even when WdfHandler_OpenFile
        // fails. Represent that by retaining prefix ownership without an available archive.
        if (!looseFiles.TryResolveFile(declaredName, out string? packagePath))
        {
            return new WdfPackageRegistration(declaredName, prefix, WdfPackageRegistrationOutcome.FileNotFound);
        }

        WdfArchive archive;

        try
        {
            archive = WdfArchive.Open(packagePath);
        }
        catch (InvalidDataException)
        {
            return new WdfPackageRegistration(declaredName, prefix, WdfPackageRegistrationOutcome.ArchiveUnavailable);
        }
        catch (IOException)
        {
            return new WdfPackageRegistration(declaredName, prefix, WdfPackageRegistrationOutcome.ArchiveUnavailable);
        }
        catch (UnauthorizedAccessException)
        {
            return new WdfPackageRegistration(declaredName, prefix, WdfPackageRegistrationOutcome.ArchiveUnavailable);
        }

        packagesByPrefix.Add(prefix, archive);

        return new WdfPackageRegistration(declaredName, prefix, WdfPackageRegistrationOutcome.Registered);
    }
}
