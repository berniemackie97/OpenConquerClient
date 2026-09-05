namespace OpenConquer.Product.Tool.Tests;

public sealed class ManagedProductStagerTests
{
    [Fact]
    public void Stage_ComposesIndependentPublishesUnderTheManagedRoot()
    {
        using TemporaryDirectory temporary = new();
        string launcherRoot = temporary.CreateDirectory("launcher");
        string clientRoot = temporary.CreateDirectory("client");
        string outputRoot = Path.Combine(temporary.RootPath, "output");

        File.WriteAllText(Path.Combine(launcherRoot, "openconquer.installation.json"), "descriptor");
        File.WriteAllText(Path.Combine(launcherRoot, "OpenConquer.Launcher"), "launcher");
        Directory.CreateDirectory(Path.Combine(clientRoot, "content", "retail-5517"));
        File.WriteAllText(Path.Combine(clientRoot, "OpenConquer.Client"), "client");

        ManagedProductStager.Stage(new ProductStageOptions(launcherRoot, clientRoot, outputRoot));

        Assert.Equal("descriptor", File.ReadAllText(Path.Combine(outputRoot, "openconquer.installation.json")));
        Assert.Equal("launcher", File.ReadAllText(Path.Combine(outputRoot, "OpenConquer.Launcher")));
        Assert.Equal("client", File.ReadAllText(Path.Combine(outputRoot, "client", "OpenConquer.Client")));
        Assert.True(Directory.Exists(Path.Combine(outputRoot, "client", "content", "retail-5517")));
    }

    [Fact]
    public void Stage_RejectsExistingOutputWithoutDeletingExistingFiles()
    {
        using TemporaryDirectory temporary = new();
        string launcherRoot = temporary.CreateDirectory("launcher");
        string clientRoot = temporary.CreateDirectory("client");
        string outputRoot = temporary.CreateDirectory("output");
        string existingFile = Path.Combine(outputRoot, "keep.txt");

        File.WriteAllText(Path.Combine(launcherRoot, "openconquer.installation.json"), "descriptor");
        File.WriteAllText(Path.Combine(clientRoot, "OpenConquer.Client"), "client");
        File.WriteAllText(existingFile, "keep");

        Assert.Throws<InvalidOperationException>(() =>
            ManagedProductStager.Stage(new ProductStageOptions(launcherRoot, clientRoot, outputRoot)));

        Assert.Equal("keep", File.ReadAllText(existingFile));
    }

    [Fact]
    public void Stage_RejectsLinkedEntriesBeforeCopyingThem()
    {
        using TemporaryDirectory temporary = new();
        string launcherRoot = temporary.CreateDirectory("launcher");
        string clientRoot = temporary.CreateDirectory("client");
        string outputRoot = Path.Combine(temporary.RootPath, "output");
        string linkedFile = Path.Combine(launcherRoot, "linked");

        File.WriteAllText(Path.Combine(launcherRoot, "openconquer.installation.json"), "descriptor");
        File.WriteAllText(Path.Combine(clientRoot, "OpenConquer.Client"), "client");

        try
        {
            File.CreateSymbolicLink(linkedFile, Path.Combine(clientRoot, "OpenConquer.Client"));
        }
        catch (PlatformNotSupportedException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        Assert.Throws<InvalidDataException>(() =>
            ManagedProductStager.Stage(new ProductStageOptions(launcherRoot, clientRoot, outputRoot)));
        Assert.False(Directory.Exists(outputRoot));
    }

    [Fact]
    public void Stage_RejectsAClientDirectoryAlreadyPresentInTheLauncherPublish()
    {
        using TemporaryDirectory temporary = new();
        string launcherRoot = temporary.CreateDirectory("launcher");
        string clientRoot = temporary.CreateDirectory("client");
        string outputRoot = Path.Combine(temporary.RootPath, "output");

        File.WriteAllText(Path.Combine(launcherRoot, "openconquer.installation.json"), "descriptor");
        Directory.CreateDirectory(Path.Combine(launcherRoot, "client"));
        File.WriteAllText(Path.Combine(clientRoot, "OpenConquer.Client"), "client");

        Assert.Throws<InvalidDataException>(() =>
            ManagedProductStager.Stage(new ProductStageOptions(launcherRoot, clientRoot, outputRoot)));
        Assert.False(Directory.Exists(outputRoot));
    }

    [Fact]
    public void Stage_RemovesTemporaryOutputWhenValidationFails()
    {
        using TemporaryDirectory temporary = new();
        string launcherRoot = temporary.CreateDirectory("launcher");
        string clientRoot = temporary.CreateDirectory("client");
        string outputRoot = Path.Combine(temporary.RootPath, "output");

        File.WriteAllText(Path.Combine(launcherRoot, "OpenConquer.Launcher"), "launcher");
        File.WriteAllText(Path.Combine(clientRoot, "OpenConquer.Client"), "client");

        Assert.Throws<InvalidDataException>(() =>
            ManagedProductStager.Stage(new ProductStageOptions(launcherRoot, clientRoot, outputRoot)));

        Assert.False(Directory.Exists(outputRoot));
        Assert.Empty(Directory.EnumerateDirectories(temporary.RootPath, "output.staging-*"));
    }

    [Fact]
    public void Stage_RejectsLinkedPublishRoots()
    {
        using TemporaryDirectory temporary = new();
        string launcherRoot = temporary.CreateDirectory("launcher");
        string clientRoot = temporary.CreateDirectory("client");
        string outputRoot = Path.Combine(temporary.RootPath, "output");
        string linkedClientRoot = Path.Combine(temporary.RootPath, "linked-client");

        File.WriteAllText(Path.Combine(launcherRoot, "openconquer.installation.json"), "descriptor");
        File.WriteAllText(Path.Combine(clientRoot, "OpenConquer.Client"), "client");

        try
        {
            Directory.CreateSymbolicLink(linkedClientRoot, clientRoot);
        }
        catch (PlatformNotSupportedException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        Assert.Throws<InvalidDataException>(() =>
            ManagedProductStager.Stage(new ProductStageOptions(launcherRoot, linkedClientRoot, outputRoot)));
        Assert.False(Directory.Exists(outputRoot));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"openconquer-product-tool-{Guid.NewGuid():N}");

        public TemporaryDirectory()
        {
            Directory.CreateDirectory(_path);
        }

        public string RootPath => _path;

        public string CreateDirectory(string name)
        {
            string path = Path.Combine(_path, name);
            Directory.CreateDirectory(path);
            return path;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_path, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
