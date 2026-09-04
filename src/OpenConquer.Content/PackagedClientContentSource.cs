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
/// Native package identity is the 32-bit hash of the derived prefix, not the prefix string itself.
/// Hash ownership is established before the declared archive is opened. Missing, unreadable, or
/// structurally invalid archives therefore retain their routing hashes without an available
/// <see cref="WdfArchive"/>, and later declarations whose prefixes hash to the same value are
/// duplicates. This preserves the verified native first-wins registration behavior while
/// validating legacy archive bytes as untrusted modern input.
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
    private readonly Dictionary<uint, WdfArchive> _packagesByPrefixHash;

    private PackagedClientContentSource(ClientContentRoot looseFiles, Dictionary<uint, WdfArchive> packagesByPrefixHash, List<WdfPackageRegistration> packageRegistrations)
    {
        _looseFiles = looseFiles;
        _packagesByPrefixHash = packagesByPrefixHash;
        PackageRegistrations = Array.AsReadOnly(packageRegistrations.ToArray());
    }

    /// <summary>
    /// Every <c>ini/package.ini</c> declaration in file order with its resolved outcome.
    /// </summary>
    /// <remarks>
    /// Empty when the declaration file is absent, unavailable through the safe host-filesystem
    /// boundary, or rejected by the modern bounded-read policy. Native treats failure to open the
    /// declaration file as non-fatal and continues with zero registered packages
    /// (<c>0x1001A3B0</c>).
    /// </remarks>
    public IReadOnlyList<WdfPackageRegistration> PackageRegistrations
    {
        get;
    }

    public static PackagedClientContentSource Open(string rootPath)
    {
        ClientContentRoot looseFiles = new(rootPath);

        // TqPackagesOpen stores and compares only WdfHash_Core(prefix). The source string remains
        // useful for diagnostics, but routing ownership must follow the native 32-bit identity.
        HashSet<uint> registeredPrefixHashes = [];
        Dictionary<uint, WdfArchive> packagesByPrefixHash = [];
        List<WdfPackageRegistration> registrations = [];

        string[] declaredPackageNames;

        try
        {
            if (!looseFiles.TryOpenRead(PackageConfigurationPath, ContentLookupMode.LooseOnly, out Stream? declarationStream))
            {
                return new PackagedClientContentSource(looseFiles, packagesByPrefixHash, registrations);
            }

            using (declarationStream)
            {
                declaredPackageNames = ReadDeclaredPackageNames(declarationStream);
            }
        }
        catch (InvalidDataException)
        {
            // The native registration routine does not gate client initialization. Our bounded
            // read replaces unsafe legacy behavior with a safe zero-package result.
            return new PackagedClientContentSource(looseFiles, packagesByPrefixHash, registrations);
        }
        catch (IOException)
        {
            return new PackagedClientContentSource(looseFiles, packagesByPrefixHash, registrations);
        }
        catch (UnauthorizedAccessException)
        {
            return new PackagedClientContentSource(looseFiles, packagesByPrefixHash, registrations);
        }

        foreach (string declaredName in declaredPackageNames)
        {
            registrations.Add(RegisterPackage(looseFiles, registeredPrefixHashes, packagesByPrefixHash, declaredName));
        }

        return new PackagedClientContentSource(looseFiles, packagesByPrefixHash, registrations);
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
        uint prefixHash = WdfPathHash.Compute(prefix);

        if (_packagesByPrefixHash.TryGetValue(prefixHash, out WdfArchive? package))
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

    private static WdfPackageRegistration RegisterPackage(ClientContentRoot looseFiles, HashSet<uint> registeredPrefixHashes, Dictionary<uint, WdfArchive> packagesByPrefixHash, string declaredName)
    {
        string prefix = WdfPackagePrefix.FromDeclaredPackageName(declaredName);
        uint prefixHash = WdfPathHash.Compute(prefix);

        // TqPackagesOpen computes WdfHash_Core(prefix) at 0x10003DE1 and searches the registered
        // package vector by that hash. Distinct prefix strings with the same hash are therefore
        // duplicates, and the first registration wins.
        if (!registeredPrefixHashes.Add(prefixHash))
        {
            return new WdfPackageRegistration(declaredName, prefix, WdfPackageRegistrationOutcome.DuplicatePrefix);
        }

        WdfArchive archive;

        try
        {
            // sub_100014F0 keeps the native package object registered even when
            // WdfHandler_OpenFile fails. Resolution and archive opening therefore share the same
            // expected availability boundary after routing-hash ownership has been established.
            if (!looseFiles.TryResolveFile(declaredName, out string? packagePath))
            {
                return new WdfPackageRegistration(declaredName, prefix, WdfPackageRegistrationOutcome.FileNotFound);
            }

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

        packagesByPrefixHash.Add(prefixHash, archive);

        return new WdfPackageRegistration(declaredName, prefix, WdfPackageRegistrationOutcome.Registered);
    }
}
