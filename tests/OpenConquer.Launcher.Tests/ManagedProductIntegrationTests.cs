using OpenConquer.Launcher.Installation;
using OpenConquer.Product.Tool;

namespace OpenConquer.Launcher.Tests;

public sealed class ManagedProductIntegrationTests
{
    [Fact]
    public async Task InstallationCheck_RecoversAfterProductCompositionWithoutRestart()
    {
        using TemporaryDirectory temporary = new();
        string launcherPublish = temporary.CreateDirectory("launcher");
        string clientPublish = temporary.CreateDirectory("client");
        string productRoot = Path.Combine(temporary.RootPath, "managed");
        File.WriteAllText(Path.Combine(launcherPublish, "OpenConquer.Launcher"), "launcher");
        File.WriteAllText(Path.Combine(clientPublish, "OpenConquer.Client"), "client");
        await using LauncherApplication application = new(new ManagedInstallationResolver(productRoot));

        await application.StartAsync();
        Assert.Equal(ManagedInstallationIssue.ManifestMissing,
            Assert.IsType<LauncherState.InstallationUnavailable>(application.State).Issue);

        ManagedProductStager.Stage(new ProductStageOptions(launcherPublish, clientPublish, productRoot));
        await application.RetryInstallationAsync();

        Assert.Equal(productRoot,
            Assert.IsType<LauncherState.InstallationResolved>(application.State).Installation.RootPath);
    }

    [Fact]
    public async Task StagedManagedProductIsAcceptedByLauncherResolver()
    {
        using TemporaryDirectory temporary = new();

        string launcherPublishPath = temporary.CreateDirectory("launcher");

        string clientPublishPath = temporary.CreateDirectory("client");

        string managedProductPath = Path.Combine(temporary.RootPath, "managed");

        File.WriteAllText(Path.Combine(launcherPublishPath, "OpenConquer.Launcher"), "launcher");

        File.WriteAllText(Path.Combine(clientPublishPath, "OpenConquer.Client"), "client");

        ManagedProductStager.Stage(
            new ProductStageOptions(launcherPublishPath, clientPublishPath, managedProductPath)
        );

        ManagedInstallationResolution resolution = await new ManagedInstallationResolver(
            managedProductPath
        ).ResolveAsync(CancellationToken.None);

        ManagedInstallationResolution.Resolved resolved =
            Assert.IsType<ManagedInstallationResolution.Resolved>(resolution);

        Assert.Equal(Path.GetFullPath(managedProductPath), resolved.Installation.RootPath);

        Assert.Equal(
            Path.GetFullPath(Path.Combine(managedProductPath, ManagedProductDescriptor.ClientRoot)),
            resolved.Installation.ClientRootPath
        );

        Assert.Equal(
            Path.GetFullPath(Path.Combine(managedProductPath, ManagedProductDescriptor.FileName)),
            resolved.Installation.ManifestPath
        );
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private readonly string _path = Path.Combine(
            Path.GetTempPath(),
            $"openconquer-managed-product-{Guid.NewGuid():N}"
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
