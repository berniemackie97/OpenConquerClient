using System.Text.Json;
using OpenConquer.Launcher.Installation;

namespace OpenConquer.Launcher.Tests;

public sealed class ManagedReleaseManifestTests
{
    [Theory]
    [InlineData("Foo/a.dat", "foo/b.dat")]
    [InlineData("content", "CONTENT/file.dat")]
    public async Task ReadRejectsCrossPlatformPathTopologyCollisions(
        string firstPath,
        string secondPath)
    {
        using TemporaryDirectory temporary = new();
        string path = Path.Combine(temporary.RootPath, ManagedReleaseManifest.FileName);
        await File.WriteAllBytesAsync(path, CreateManifestBytes([firstPath, secondPath]),
            TestContext.Current.CancellationToken);

        ReleaseManifestReadResult result = await ManagedReleaseManifest.ReadAsync(
            path, TestContext.Current.CancellationToken);

        Assert.Equal(
            new ReleaseManifestReadResult.Rejected(
                ManagedInstallationIssue.ReleaseManifestInvalid),
            result);
    }

    [Fact]
    public async Task ReadRejectsExcessiveDirectoryTopology()
    {
        using TemporaryDirectory temporary = new();
        string path = Path.Combine(temporary.RootPath, ManagedReleaseManifest.FileName);
        string[] paths = Enumerable.Range(0, 68)
            .Select(index => $"d{index:D2}/{string.Join('/', Enumerable.Repeat("a", 240))}/f.dat")
            .ToArray();
        await File.WriteAllBytesAsync(path, CreateManifestBytes(paths),
            TestContext.Current.CancellationToken);

        ReleaseManifestReadResult result = await ManagedReleaseManifest.ReadAsync(
            path, TestContext.Current.CancellationToken);

        Assert.Equal(
            new ReleaseManifestReadResult.Rejected(
                ManagedInstallationIssue.ReleaseManifestInvalid),
            result);
    }

    [Fact]
    public async Task ReadRejectsAlternateExecutableForTargetRuntime()
    {
        using TemporaryDirectory temporary = new();
        string path = Path.Combine(temporary.RootPath, ManagedReleaseManifest.FileName);
        string runtime = ReleaseTargetRuntime.Current ?? "linux-x64";
        File.WriteAllText(path, JsonSerializer.Serialize(new
        {
            schemaVersion = ManagedReleaseManifest.CurrentSchemaVersion,
            productId = ManagedReleaseManifest.ExpectedProductId,
            releaseSequence = 1,
            releaseVersion = "test-release",
            minimumLauncherVersion = ManagedReleaseManifest.CurrentLauncherVersion,
            targetRuntime = runtime,
            clientExecutable = "alternate-client",
            files = new[]
            {
                new
                {
                    path = "alternate-client",
                    length = 0,
                    sha256 = new string('0', 64),
                },
            },
        }));

        ReleaseManifestReadResult result = await ManagedReleaseManifest.ReadAsync(
            path, TestContext.Current.CancellationToken);

        Assert.Equal(
            new ReleaseManifestReadResult.Rejected(
                ManagedInstallationIssue.ReleaseManifestInvalid),
            result);
    }

    private static byte[] CreateManifestBytes(IReadOnlyCollection<string> additionalPaths)
    {
        string targetRuntime = ReleaseTargetRuntime.Current ?? "linux-x64";
        string clientExecutable = ReleaseTargetRuntime.ClientExecutable(targetRuntime);
        string[] paths = additionalPaths
            .Append(clientExecutable)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = ManagedReleaseManifest.CurrentSchemaVersion,
            productId = ManagedReleaseManifest.ExpectedProductId,
            releaseSequence = 1,
            releaseVersion = "test-release",
            minimumLauncherVersion = ManagedReleaseManifest.CurrentLauncherVersion,
            targetRuntime,
            clientExecutable,
            files = paths.Select(path => new
            {
                path,
                length = 0,
                sha256 = new string('0', 64),
            }),
        });
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            RootPath = Path.Combine(Path.GetTempPath(), "OpenConquer.ReleaseManifest.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootPath);
        }

        public string RootPath
        {
            get;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(RootPath, recursive: true);
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
