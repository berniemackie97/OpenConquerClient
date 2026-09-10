namespace OpenConquer.Rendering.Conformance.Support;

internal sealed class ConformanceOptions
{
    private ConformanceOptions(string contentRoot)
    {
        ContentRoot = contentRoot;
    }

    public string ContentRoot
    {
        get;
    }

    public static ConformanceOptions Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length != 2 || !string.Equals(args[0], "--content-root", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(args[1]))
        {
            throw new ArgumentException("Usage: OpenConquer.Rendering.Conformance --content-root <retail-5517-root>");
        }

        string contentRoot = Path.GetFullPath(args[1]);

        if (!Directory.Exists(contentRoot))
        {
            throw new DirectoryNotFoundException($"Retail 5517 content root '{contentRoot}' does not exist.");
        }

        return new ConformanceOptions(contentRoot);
    }
}
