using OpenConquer.Rendering.Conformance.Support;

namespace OpenConquer.Rendering.Conformance;

internal static class Program
{
    private static int Main(string[] args)
    {
        ConformanceOptions options = ConformanceOptions.Parse(args);

        ConformanceRunner.Run(options);

        return 0;
    }
}
