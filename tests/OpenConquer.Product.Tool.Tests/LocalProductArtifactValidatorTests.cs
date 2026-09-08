namespace OpenConquer.Product.Tool.Tests;

public sealed class LocalProductArtifactValidatorTests
{
    [Fact]
    public void ValidateClientPublishAcceptsCleanPublish()
    {
        using TemporaryDirectory temporary = new();

        string clientRoot = temporary.CreateDirectory("client");
        File.WriteAllText(Path.Combine(clientRoot, "OpenConquer.Client"), "client");

        LocalProductArtifactValidator.ValidateClientPublish(clientRoot);
    }

    [Fact]
    public void ValidateClientPublishRejectsNestedServerDatCaseInsensitively()
    {
        using TemporaryDirectory temporary = new();

        string clientRoot = temporary.CreateDirectory("client");
        string nestedRoot = Path.Combine(clientRoot, "legacy", "config");

        Directory.CreateDirectory(nestedRoot);
        File.WriteAllText(Path.Combine(nestedRoot, "server.DAT"), "legacy");

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            LocalProductArtifactValidator.ValidateClientPublish(clientRoot)
        );

        Assert.Contains("Server.dat", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateClientPublishRejectsLinkedEntry()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using TemporaryDirectory temporary = new();

        string clientRoot = temporary.CreateDirectory("client");
        string targetPath = Path.Combine(temporary.RootPath, "target");

        File.WriteAllText(targetPath, "target");
        File.CreateSymbolicLink(Path.Combine(clientRoot, "linked-file"), targetPath);

        Assert.Throws<InvalidDataException>(() =>
            LocalProductArtifactValidator.ValidateClientPublish(clientRoot)
        );
    }

    [Fact]
    public void ValidateLauncherPublishAcceptsIsolatedLauncher()
    {
        using TemporaryDirectory temporary = new();

        string launcherRoot = temporary.CreateDirectory("launcher");
        CreateLauncherExecutable(launcherRoot);

        LocalProductArtifactValidator.ValidateLauncherPublish(
            launcherRoot,
            RequireCurrentTargetRuntime()
        );
    }

    [Fact]
    public void ValidateLauncherPublishRejectsLooseReleaseTrust()
    {
        using TemporaryDirectory temporary = new();

        string launcherRoot = temporary.CreateDirectory("launcher");

        CreateLauncherExecutable(launcherRoot);
        File.WriteAllText(Path.Combine(launcherRoot, ProductReleaseTrust.FileName), "{}");

        Assert.Throws<InvalidDataException>(() =>
            LocalProductArtifactValidator.ValidateLauncherPublish(
                launcherRoot,
                RequireCurrentTargetRuntime()
            )
        );
    }

    [Fact]
    public void ValidateLauncherPublishRejectsRetailRuntimeContent()
    {
        using TemporaryDirectory temporary = new();

        string launcherRoot = temporary.CreateDirectory("launcher");
        string contentRoot = Path.Combine(launcherRoot, "content", "retail-5517");

        CreateLauncherExecutable(launcherRoot);
        Directory.CreateDirectory(contentRoot);
        File.WriteAllText(Path.Combine(contentRoot, "manifest.json"), "{}");

        Assert.Throws<InvalidDataException>(() =>
            LocalProductArtifactValidator.ValidateLauncherPublish(
                launcherRoot,
                RequireCurrentTargetRuntime()
            )
        );
    }

    [Fact]
    public void ValidateLauncherPublishRejectsMissingExecutable()
    {
        using TemporaryDirectory temporary = new();

        string launcherRoot = temporary.CreateDirectory("launcher");

        Assert.Throws<FileNotFoundException>(() =>
            LocalProductArtifactValidator.ValidateLauncherPublish(
                launcherRoot,
                RequireCurrentTargetRuntime()
            )
        );
    }

    [Fact]
    public void ValidateManagedProductAcceptsProductionMetadata()
    {
        using TemporaryDirectory temporary = new();

        string productRoot = temporary.CreateDirectory("product");

        File.WriteAllText(Path.Combine(productRoot, ManagedProductDescriptor.FileName), "{}");
        File.WriteAllText(Path.Combine(productRoot, ProductReleaseManifest.FileName), "{}");
        File.WriteAllText(Path.Combine(productRoot, ProductReleaseSignature.FileName), "{}");

        LocalProductArtifactValidator.ValidateManagedProduct(productRoot);
    }

    [Fact]
    public void ValidateManagedProductRejectsNestedPublisherPublicKey()
    {
        using TemporaryDirectory temporary = new();

        string productRoot = temporary.CreateDirectory("product");
        string nestedRoot = Path.Combine(productRoot, "unexpected");

        Directory.CreateDirectory(nestedRoot);
        File.WriteAllText(
            Path.Combine(nestedRoot, LocalProductPaths.PublicKeyFileName),
            "publisher"
        );

        Assert.Throws<InvalidDataException>(() =>
            LocalProductArtifactValidator.ValidateManagedProduct(productRoot)
        );
    }

    [Fact]
    public void ValidateManagedProductRejectsDevelopmentPrivateKey()
    {
        using TemporaryDirectory temporary = new();

        string productRoot = temporary.CreateDirectory("product");

        File.WriteAllText(
            Path.Combine(productRoot, DevelopmentPublisherIdentityPaths.PrivateKeyFileName),
            "private"
        );

        Assert.Throws<InvalidDataException>(() =>
            LocalProductArtifactValidator.ValidateManagedProduct(productRoot)
        );
    }

    private static void CreateLauncherExecutable(string launcherRoot)
    {
        string executable = RequireCurrentTargetRuntime()
            .StartsWith("win-", StringComparison.Ordinal)
            ? "OpenConquer.Launcher.exe"
            : "OpenConquer.Launcher";

        File.WriteAllText(Path.Combine(launcherRoot, executable), "launcher");
    }

    private static string RequireCurrentTargetRuntime()
    {
        return ProductTargetRuntime.Current
            ?? throw new InvalidOperationException(
                "The test host is not a supported OpenConquer product runtime."
            );
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private readonly string _path = Path.Combine(
            Path.GetTempPath(),
            $"openconquer-local-product-validation-{Guid.NewGuid():N}"
        );

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
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
