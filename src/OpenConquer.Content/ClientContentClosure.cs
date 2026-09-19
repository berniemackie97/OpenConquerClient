using OpenConquer.Content.Configuration;

namespace OpenConquer.Content;

/// <summary>
/// Resolves the content paths required by the client at runtime.
/// </summary>
public static class ClientContentClosure
{
    private static readonly int[] s_startupLogoVariantIndexes = [1, 2];

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
