using OpenConquer.Content.Configuration;

namespace OpenConquer.Content;

/// <summary>
/// The exact set of retail content paths the implemented runtime slices read.
/// </summary>
public static class ClientContentClosure
{
    private static readonly int[] s_startupLogoVariantIndexes = [1, 2];

    /// <summary>
    /// Resolves the closure against <paramref name="contentSource"/>.
    /// </summary>
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
