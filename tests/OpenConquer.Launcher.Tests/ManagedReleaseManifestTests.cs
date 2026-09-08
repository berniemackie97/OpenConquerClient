using System.Text.Json;
using OpenConquer.Launcher.Installation;

namespace OpenConquer.Launcher.Tests;

public sealed class ManagedReleaseManifestTests
{
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
