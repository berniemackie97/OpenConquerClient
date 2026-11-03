namespace OpenConquer.Tests;

/// <summary>Reports an unavailable local retail fixture as skipped, without substituting generated game content.</summary>
internal static class RetailContentFixture
{
    internal static void RequireFile(string path) =>
        Xunit.Assert.SkipUnless(File.Exists(path), "Local retail content is unavailable. Restore content/retail-5517 and rebuild to run this asset-dependent test.");

    internal static byte[] ReadAllBytes(string path)
    {
        RequireFile(path);
        return File.ReadAllBytes(path);
    }
}
