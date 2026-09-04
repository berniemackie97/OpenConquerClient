using OpenConquer.Content.Configuration;
using OpenConquer.Content.Startup;
using OpenConquer.Content.Wdf;

namespace OpenConquer.Content.Tool.Startup;

/// <summary>
/// Describes what the implemented startup slice resolves from a content root.
/// </summary>
internal sealed record StartupContentReport(int ScreenMode, int LogicalWidthPixels, int LogicalHeightPixels, IReadOnlyList<StartupLogo> Logos, IReadOnlyList<WdfPackageRegistration> PackageRegistrations, IReadOnlyList<string> ClosurePaths)
{
    public static StartupContentReport Create(string contentRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);

        PackagedClientContentSource contentSource = PackagedClientContentSource.Open(contentRootPath);
        GameSetupConfiguration gameSetup = GameSetupConfiguration.Load(contentSource);

        return new StartupContentReport(gameSetup.ScreenMode, gameSetup.LogicalWidthPixels, gameSetup.LogicalHeightPixels,
            [StartupLogo.Load(contentSource, monotonicTickMilliseconds: 0), StartupLogo.Load(contentSource, monotonicTickMilliseconds: 1),],
            contentSource.PackageRegistrations, ClientContentClosure.Resolve(contentSource));
    }

    public IEnumerable<string> ToReportLines()
    {
        yield return $"Screen mode: {ScreenMode} ({LogicalWidthPixels}x{LogicalHeightPixels})";

        foreach (StartupLogo logo in Logos)
        {
            string description = logo.Image is { } image ? $"{image.Width}x{image.Height}" : $"unavailable ({logo.UnavailableReason})";

            yield return $"Startup logo {logo.VariantIndex}: {logo.ContentPath} ({description})";
        }

        foreach (WdfPackageRegistration registration in PackageRegistrations)
        {
            yield return $"Package '{registration.DeclaredName}' -> prefix '{registration.Prefix}': {registration.Outcome}";
        }

        foreach (string contentPath in ClosurePaths)
        {
            yield return $"Closure: {contentPath}";
        }
    }
}
