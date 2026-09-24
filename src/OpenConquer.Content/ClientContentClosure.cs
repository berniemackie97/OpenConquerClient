using OpenConquer.Content.Ani;
using OpenConquer.Content.Configuration;

namespace OpenConquer.Content;

/// <summary>
/// Resolves the content requirements required by the client at runtime.
/// </summary>
public static class ClientContentClosure
{
    private const string ControlAniPath = "ani/Control.ani";
    private const string ProgressSectionName = "Progress45";
    private const string DialogSectionName = "Dialog4";

    private static readonly int[] s_startupLogoVariantIndexes = [1, 2];

    public static IReadOnlyList<ClientContentRequirement> Resolve(IClientContentSource contentSource)
    {
        ArgumentNullException.ThrowIfNull(contentSource);

        StartupLogoConfiguration startupLogo = StartupLogoConfiguration.LoadOrDefault(contentSource);

        List<ClientContentRequirement> requirements =
        [
            new(GameSetupConfiguration.RelativePath, ContentLookupMode.LooseOnly),
            new(StartupLogoConfiguration.RelativePath, ContentLookupMode.LooseOnly),
            new(PackagedClientContentSource.PackageConfigurationPath, ContentLookupMode.LooseOnly),
            new(ControlAniPath, ContentLookupMode.LooseOnly),
        ];

        foreach (int variantIndex in s_startupLogoVariantIndexes)
        {
            requirements.Add(new ClientContentRequirement(startupLogo.GetLogoPath(variantIndex), ContentLookupMode.LooseOnly));
        }

        AniIndexFile controlAni = AniIndexFile.Load(contentSource, ControlAniPath, ContentLookupMode.LooseOnly);

        AddFrameRequirements(requirements, controlAni.GetRequiredSection(ProgressSectionName), expectedFrameCount: 1);
        AddFrameRequirements(requirements, controlAni.GetRequiredSection(DialogSectionName), expectedFrameCount: 2);

        return Normalize(requirements);
    }

    private static void AddFrameRequirements(List<ClientContentRequirement> requirements, AniIndexSection section, int expectedFrameCount)
    {
        if (section.FrameCount != expectedFrameCount)
        {
            throw new InvalidDataException($"ANI section [{section.Name}] must contain exactly {expectedFrameCount} frame(s); found {section.FrameCount}.");
        }

        foreach (string framePath in section.FramePaths)
        {
            requirements.Add(new ClientContentRequirement(framePath, ContentLookupMode.LooseThenPackage));
        }
    }

    private static ClientContentRequirement[] Normalize(IEnumerable<ClientContentRequirement> requirements)
    {
        Dictionary<string, ClientContentRequirement> requirementsByPath = new(StringComparer.OrdinalIgnoreCase);

        foreach (ClientContentRequirement requirement in requirements)
        {
            if (requirementsByPath.TryGetValue(requirement.ContentPath, out ClientContentRequirement? existing))
            {
                if (existing.LookupMode != requirement.LookupMode)
                {
                    throw new InvalidOperationException($"Client content path '{requirement.ContentPath}' is required with conflicting lookup modes {existing.LookupMode} and {requirement.LookupMode}.");
                }

                continue;
            }

            requirementsByPath.Add(requirement.ContentPath, requirement);
        }

        return requirementsByPath.Values.OrderBy(static requirement => requirement.ContentPath, StringComparer.Ordinal).ToArray();
    }
}
