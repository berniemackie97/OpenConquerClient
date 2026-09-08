namespace OpenConquer.Product.Tool.Tests;

public sealed class ProductRepositoryLocatorTests
{
    [Fact]
    public void FindReturnsRepositoryRoot()
    {
        using TemporaryDirectory temporary = new();

        CreateRepositoryMarkers(temporary.RootPath);

        Assert.Equal(temporary.RootPath, ProductRepositoryLocator.Find(temporary.RootPath));
    }

    [Fact]
    public void FindWalksUpFromNestedDirectory()
    {
        using TemporaryDirectory temporary = new();

        CreateRepositoryMarkers(temporary.RootPath);

        string nestedPath = Directory
            .CreateDirectory(Path.Combine(temporary.RootPath, "tools", "nested", "working"))
            .FullName;

        Assert.Equal(temporary.RootPath, ProductRepositoryLocator.Find(nestedPath));
    }

    [Fact]
    public void FindRejectsIncompleteRepositoryMarkers()
    {
        using TemporaryDirectory temporary = new();

        File.WriteAllText(
            Path.Combine(temporary.RootPath, "OpenConquer.Client.slnx"),
            string.Empty
        );
        File.WriteAllText(Path.Combine(temporary.RootPath, "global.json"), "{}");

        Assert.Throws<InvalidOperationException>(() =>
            ProductRepositoryLocator.Find(temporary.RootPath)
        );
    }

    [Fact]
    public void FindRejectsRepositoryWithoutContentTool()
    {
        using TemporaryDirectory temporary = new();

        CreateRepositoryMarkers(temporary.RootPath);

        File.Delete(
            Path.Combine(
                temporary.RootPath,
                "tools",
                "OpenConquer.Content.Tool",
                "OpenConquer.Content.Tool.csproj"
            )
        );

        Assert.Throws<InvalidOperationException>(() =>
            ProductRepositoryLocator.Find(temporary.RootPath)
        );
    }

    [Fact]
    public void FindRejectsMissingStartDirectory()
    {
        using TemporaryDirectory temporary = new();

        string missingPath = Path.Combine(temporary.RootPath, "missing");

        Assert.Throws<DirectoryNotFoundException>(() => ProductRepositoryLocator.Find(missingPath));
    }

    private static void CreateRepositoryMarkers(string rootPath)
    {
        File.WriteAllText(Path.Combine(rootPath, "OpenConquer.Client.slnx"), string.Empty);
        File.WriteAllText(Path.Combine(rootPath, "global.json"), "{}");

        WriteProject(rootPath, "src", "OpenConquer.Client", "OpenConquer.Client.csproj");
        WriteProject(rootPath, "src", "OpenConquer.Launcher", "OpenConquer.Launcher.csproj");
        WriteProject(
            rootPath,
            "tools",
            "OpenConquer.Content.Tool",
            "OpenConquer.Content.Tool.csproj"
        );
        WriteProject(
            rootPath,
            "tools",
            "OpenConquer.Product.Tool",
            "OpenConquer.Product.Tool.csproj"
        );
    }

    private static void WriteProject(string rootPath, params string[] relativePath)
    {
        string path = Path.Combine([rootPath, .. relativePath]);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "<Project />");
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private readonly string _path = Path.Combine(
            Path.GetTempPath(),
            $"openconquer-product-repository-{Guid.NewGuid():N}"
        );

        public TemporaryDirectory()
        {
            Directory.CreateDirectory(_path);
        }

        public string RootPath => _path;

        public void Dispose()
        {
            try
            {
                Directory.Delete(_path, recursive: true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
