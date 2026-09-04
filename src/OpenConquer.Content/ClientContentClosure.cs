using OpenConquer.Content.Configuration;

namespace OpenConquer.Content;

/// <summary>
/// The exact set of retail content paths the implemented runtime slices read.
/// </summary>
/// <remarks>
/// <para>
/// This is the single definition of what a shipped content set must contain. The import tool
/// resolves it against an authorized retail source so the tracked payload cannot drift from what
/// the readers actually open, and the client resolves the same paths at runtime.
/// </para>
/// <para>
/// Packages declared by <c>ini/package.ini</c> are deliberately outside the closure. They are
/// hundreds of megabytes, no implemented reader needs an entry from one, and their absence is
/// non-fatal in retail: the declaration reader returns <see langword="void"/> and its caller
/// discards <c>TqPackagesOpen</c>'s result at <c>0x1001A406</c>. The declaration file itself is
/// inside the closure, so the omission stays visible to a reviewer.
/// </para>
/// <para>
/// Historical artifacts retained only for compatibility research, parity testing, or offline
/// tooling are not part of this runtime closure and are not shipped as client runtime content.
/// </para>
/// </remarks>
public static class ClientContentClosure
{
    /// <summary>The retail startup-logo variants, matching <c>(timeGetTime() &amp; 1) + 1</c>.</summary>
    private static readonly int[] s_startupLogoVariantIndexes = [1, 2];

    /// <summary>
    /// Resolves the closure against <paramref name="contentSource"/>.
    /// </summary>
    /// <remarks>
    /// The startup-logo entries are data-driven: they come from <c>[DlgLogo] BgFormat</c> in
    /// <c>ini/info.ini</c>, falling back to the retail default when that file or key is absent.
    /// Resolution is therefore performed against a source rather than hard-coded.
    /// </remarks>
    /// <returns>Distinct content paths ordered ordinally, so a caller sees a stable sequence.</returns>
    public static IReadOnlyList<string> Resolve(IClientContentSource contentSource)
    {
        ArgumentNullException.ThrowIfNull(contentSource);

        StartupLogoConfiguration startupLogo = StartupLogoConfiguration.LoadOrDefault(contentSource);

        List<string> contentPaths =
        [
            GameSetupConfiguration.RelativePath,
            StartupLogoConfiguration.RelativePath,
            PackagedClientContentSource.PackageConfigurationPath,
        ];

        foreach (int variantIndex in s_startupLogoVariantIndexes)
        {
            contentPaths.Add(startupLogo.GetLogoPath(variantIndex));
        }

        return contentPaths.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal).ToArray();
    }
}
