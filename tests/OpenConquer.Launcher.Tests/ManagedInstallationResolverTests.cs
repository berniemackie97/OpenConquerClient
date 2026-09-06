using System.Text.Json;
using OpenConquer.Launcher.Installation;

namespace OpenConquer.Launcher.Tests;

public sealed class ManagedInstallationResolverTests
{
    [Fact]
    public async Task ResolveAsyncUsesTheLauncherPackageContextAndManagedClientRoot()
    {
        string root = CreateTemporaryDirectory();
        string clientRoot = Path.Combine(root, "client");
        Directory.CreateDirectory(clientRoot);
        WriteManifest(root, "client");

        try
        {
            ManagedInstallationResolution resolution = await new ManagedInstallationResolver(
                root
            ).ResolveAsync(CancellationToken.None);

            ManagedInstallationResolution.Resolved resolved =
                Assert.IsType<ManagedInstallationResolution.Resolved>(resolution);
            Assert.Equal(Path.GetFullPath(root), resolved.Installation.RootPath);
            Assert.Equal(Path.GetFullPath(clientRoot), resolved.Installation.ClientRootPath);
            Assert.Equal(
                Path.Combine(Path.GetFullPath(root), ManagedInstallationManifest.FileName),
                resolved.Installation.ManifestPath
            );
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task ResolveAsyncRejectsMissingManagedManifest()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            ManagedInstallationResolution resolution = await new ManagedInstallationResolver(
                root
            ).ResolveAsync(CancellationToken.None);

            Assert.Equal(
                new ManagedInstallationResolution.Rejected(
                    ManagedInstallationIssue.ManifestMissing
                ),
                resolution
            );
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Theory]
    [InlineData("{\"schemaVersion\":1,\"productId\":\"Other\",\"clientRoot\":\"client\"}")]
    [InlineData("{\"schemaVersion\":1,\"productId\":\"OpenConquer\",\"clientRoot\":\"../client\"}")]
    [InlineData(
        "{\"schemaVersion\":1,\"productId\":\"OpenConquer\",\"clientRoot\":\"/tmp/client\"}"
    )]
    [InlineData(
        "{\"schemaVersion\":1,\"productId\":\"OpenConquer\",\"clientRoot\":\"client\",\"unexpected\":true}"
    )]
    [InlineData(
        "{\"schemaVersion\":1,\"productId\":\"OpenConquer\",\"clientRoot\":\"client\",\"clientRoot\":\"other\"}"
    )]
    [InlineData(
        "{\"schemaVersion\":1,\"productId\":\"OpenConquer\",\"clientRoot\":\"components/game\"}"
    )]
    public async Task ResolveAsyncRejectsManifestValuesOutsideTheManagedContract(string manifest)
    {
        string root = CreateTemporaryDirectory();
        try
        {
            File.WriteAllText(Path.Combine(root, ManagedInstallationManifest.FileName), manifest);

            ManagedInstallationResolution resolution = await new ManagedInstallationResolver(
                root
            ).ResolveAsync(CancellationToken.None);

            Assert.Equal(
                new ManagedInstallationResolution.Rejected(
                    ManagedInstallationIssue.ManifestInvalid
                ),
                resolution
            );
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task ResolveAsyncRejectsLinkedClientComponent()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string root = CreateTemporaryDirectory();
        string target = Path.Combine(root, "real-client");
        string linked = Path.Combine(root, "client");
        Directory.CreateDirectory(target);
        Directory.CreateSymbolicLink(linked, target);
        WriteManifest(root, "client");

        try
        {
            ManagedInstallationResolution resolution = await new ManagedInstallationResolver(
                root
            ).ResolveAsync(CancellationToken.None);

            Assert.Equal(
                new ManagedInstallationResolution.Rejected(ManagedInstallationIssue.LinkedPath),
                resolution
            );
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task ResolveAsyncReportsUnsupportedFutureManifest()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            WriteManifest(root, "client", ManagedInstallationManifest.CurrentSchemaVersion + 1);

            ManagedInstallationResolution resolution = await new ManagedInstallationResolver(
                root
            ).ResolveAsync(CancellationToken.None);

            Assert.Equal(
                new ManagedInstallationResolution.Rejected(
                    ManagedInstallationIssue.UnsupportedManifest
                ),
                resolution
            );
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task ResolveAsyncReportsMissingClientComponentWithoutSearchingElsewhere()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            WriteManifest(root, "client");
            Directory.CreateDirectory(Path.Combine(root, "unrelated-client"));

            ManagedInstallationResolution resolution = await new ManagedInstallationResolver(
                root
            ).ResolveAsync(CancellationToken.None);

            Assert.Equal(
                new ManagedInstallationResolution.Rejected(
                    ManagedInstallationIssue.ClientComponentMissing
                ),
                resolution
            );
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task ResolveAsyncHonorsCancellationBeforeReadingManifest()
    {
        string root = CreateTemporaryDirectory();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                new ManagedInstallationResolver(root).ResolveAsync(cancellation.Token)
            );
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static void WriteManifest(
        string root,
        string clientRoot,
        int schemaVersion = ManagedInstallationManifest.CurrentSchemaVersion
    )
    {
        string manifest = JsonSerializer.Serialize(
            new
            {
                schemaVersion,
                productId = ManagedInstallationManifest.ExpectedProductId,
                clientRoot,
            }
        );

        File.WriteAllText(Path.Combine(root, ManagedInstallationManifest.FileName), manifest);
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "OpenConquer.Launcher.Tests",
            Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
