using OpenConquer.Rendering.Conformance.Cases;
using OpenConquer.Rendering.Conformance.Support;

namespace OpenConquer.Rendering.Conformance;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args is ["--indexed-mesh"])
        {
            OpenGLConformanceHost.Run(IndexedMeshConformance.Run);
            return 0;
        }

        if (args is ["--world-map-residency", .. string[] contentArguments])
        {
            ConformanceOptions residencyOptions = ConformanceOptions.Parse(contentArguments);
            await WorldMapResidencyConformance.RunAsync(residencyOptions.ContentRoot).ConfigureAwait(false);
            return 0;
        }

        ConformanceOptions options = ConformanceOptions.Parse(args);

        ConformanceRunner.Run(options);

        return 0;
    }
}
