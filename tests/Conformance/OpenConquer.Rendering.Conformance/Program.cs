using OpenConquer.Rendering.Conformance.Cases;
using OpenConquer.Rendering.Conformance.Support;

namespace OpenConquer.Rendering.Conformance;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args is ["--indexed-mesh"])
        {
            OpenGLConformanceHost.Run(IndexedMeshConformance.Run);
            return 0;
        }

        ConformanceOptions options = ConformanceOptions.Parse(args);

        ConformanceRunner.Run(options);

        return 0;
    }
}
