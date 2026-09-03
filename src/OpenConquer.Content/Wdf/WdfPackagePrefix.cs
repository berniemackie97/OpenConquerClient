namespace OpenConquer.Content.Wdf;

/// <summary>
/// Derives the routing prefixes that bind a declared retail package to virtual paths.
/// </summary>
/// <remarks>
/// Both halves are verified against <c>TqPackageWdf.dll</c>. Registration and lookup must agree
/// exactly, so they live together rather than being reimplemented at each call site.
/// </remarks>
internal static class WdfPackagePrefix
{
    /// <summary>
    /// Derives the prefix a declared package registers under.
    /// </summary>
    /// <remarks>
    /// <c>TqPackagesOpen</c> (<c>0x10003D30</c>) normalizes the whole declared name at
    /// <c>0x10003D5D</c>, locates the <b>last</b> <c>'.'</c> with <c>strrchr</c> at
    /// <c>0x10003D86</c>, and copies everything before it (<c>0x10003DB4</c>). A name without a
    /// <c>'.'</c> becomes the prefix verbatim (<c>0x10003DC6</c>).
    /// <para>
    /// There is no basename extraction: <c>folder/data.wdf</c> registers under
    /// <c>folder/data</c>, which no virtual path can match because a lookup key never contains
    /// <c>'/'</c>. Retail <c>ini/package.ini</c> declares bare names only.
    /// </para>
    /// </remarks>
    public static string FromDeclaredPackageName(string declaredName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(declaredName);

        string normalizedName = Normalize(declaredName);
        int lastDotIndex = normalizedName.LastIndexOf('.');

        return lastDotIndex < 0 ? normalizedName : normalizedName[..lastDotIndex];
    }

    /// <summary>
    /// Derives the prefix a virtual path routes through.
    /// </summary>
    /// <remarks>
    /// <c>sub_10003C30</c> (<c>0x10003C30</c>) copies characters up to the first <c>'/'</c> or the
    /// terminator, lowercasing as it goes, and <c>sub_10003C80</c> hashes that. A path without a
    /// <c>'/'</c> therefore routes on the whole string.
    /// </remarks>
    public static string FromVirtualPath(string virtualPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(virtualPath);

        string normalizedPath = Normalize(virtualPath);
        int separatorIndex = normalizedPath.IndexOf('/', StringComparison.Ordinal);

        return separatorIndex < 0 ? normalizedPath : normalizedPath[..separatorIndex];
    }

    /// <summary>
    /// Applies the native path normalization: ASCII <c>A-Z</c> folded to lowercase and <c>'\'</c>
    /// mapped to <c>'/'</c>. Nothing else is altered.
    /// </summary>
    /// <remarks>
    /// Mirrors <c>WdfHash_NormalizePath</c> (<c>0x10009890</c>). Retail performs no
    /// <c>.</c>/<c>..</c> collapsing and no length rejection here; structural validation is the
    /// caller's separate responsibility.
    /// </remarks>
    private static string Normalize(string value)
    {
        return string.Create(value.Length, value, static (destination, source) =>
        {
            for (int index = 0; index < source.Length; index++)
            {
                char character = source[index];

                destination[index] = character switch
                {
                    >= 'A' and <= 'Z' => (char)(character + ('a' - 'A')),
                    '\\' => '/',
                    _ => character,
                };
            }
        });
    }
}
