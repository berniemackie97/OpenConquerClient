namespace OpenConquer.Product.Tool;

/// <summary>Locates the OpenConquer Client repository containing the product-build inputs.</summary>
internal static class ProductRepositoryLocator
{
    public static string Find(string startPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startPath);

        DirectoryInfo? current = new(Path.GetFullPath(startPath));

        if (!current.Exists)
        {
            throw new DirectoryNotFoundException($"Repository discovery start directory does not exist: '{current.FullName}'.");
        }

        while (current is not null)
        {
            if (IsRepositoryRoot(current.FullName))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException($"Could not locate the OpenConquer Client repository from '{Path.GetFullPath(startPath)}'.");
    }

    private static bool IsRepositoryRoot(string path)
    {
        return File.Exists(Path.Combine(path, "OpenConquer.Client.slnx")) && File.Exists(Path.Combine(path, "global.json")) && File.Exists(Path.Combine(path, "src", "OpenConquer.Client", "OpenConquer.Client.csproj"))
               && File.Exists(Path.Combine(path, "src", "OpenConquer.Launcher", "OpenConquer.Launcher.csproj"))
               && File.Exists(Path.Combine(path, "tools", "OpenConquer.Product.Tool", "OpenConquer.Product.Tool.csproj"));
    }
}
