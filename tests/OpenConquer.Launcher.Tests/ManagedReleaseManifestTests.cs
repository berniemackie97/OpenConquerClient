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
        string[] paths = Enumerable.Range(0, 137)
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
    public async Task ReadAcceptsExpandedReleaseAndRejectsLegacyLauncherRequirement()
    {
        using TemporaryDirectory temporary = new();
        string path = Path.Combine(temporary.RootPath, ManagedReleaseManifest.FileName);
        string[] paths = Enumerable.Range(0, 16 * 1024)
            .Select(index => $"data/f{index:D5}.bin")
            .ToArray();

        await File.WriteAllBytesAsync(path, CreateManifestBytes(paths, minimumLauncherVersion: 2),
            TestContext.Current.CancellationToken);
        ReleaseManifestReadResult result = await ManagedReleaseManifest.ReadAsync(path,
            TestContext.Current.CancellationToken);
        ReleaseManifestReadResult.Accepted accepted =
            Assert.IsType<ReleaseManifestReadResult.Accepted>(result);
        Assert.Equal(paths.Length + 1, accepted.Manifest.Files.Count);

        await File.WriteAllBytesAsync(path, CreateManifestBytes(paths, minimumLauncherVersion: 1),
            TestContext.Current.CancellationToken);
        Assert.IsType<ReleaseManifestReadResult.Rejected>(
            await ManagedReleaseManifest.ReadAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReadRejectsReleaseBeyondFileCapacity()
    {
        using TemporaryDirectory temporary = new();
        string path = Path.Combine(temporary.RootPath, ManagedReleaseManifest.FileName);
        string[] paths = Enumerable.Range(0, ManagedReleaseManifest.MaximumFileCount)
            .Select(index => $"data/f{index:D5}.bin")
            .ToArray();

        await File.WriteAllBytesAsync(path, CreateManifestBytes(paths),
            TestContext.Current.CancellationToken);
        Assert.Equal(
            new ReleaseManifestReadResult.Rejected(ManagedInstallationIssue.ReleaseManifestInvalid),
            await ManagedReleaseManifest.ReadAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReadRejectsLegacyManifestWithExpandedDirectoryTopology()
    {
        using TemporaryDirectory temporary = new();
        string path = Path.Combine(temporary.RootPath, ManagedReleaseManifest.FileName);
        string[] paths = Enumerable.Range(0, 68)
            .Select(index => $"d{index:D2}/{string.Join('/', Enumerable.Repeat("a", 240))}/f.dat")
            .ToArray();

        await File.WriteAllBytesAsync(path, CreateManifestBytes(paths, minimumLauncherVersion: 1),
            TestContext.Current.CancellationToken);
        Assert.Equal(
            new ReleaseManifestReadResult.Rejected(ManagedInstallationIssue.ReleaseManifestInvalid),
            await ManagedReleaseManifest.ReadAsync(path, TestContext.Current.CancellationToken));

        await File.WriteAllBytesAsync(path, CreateManifestBytes(paths, minimumLauncherVersion: 2),
            TestContext.Current.CancellationToken);
        Assert.IsType<ReleaseManifestReadResult.Accepted>(
            await ManagedReleaseManifest.ReadAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReadRejectsLegacyManifestBeyondMetadataCapacity()
    {
        using TemporaryDirectory temporary = new();
        string path = Path.Combine(temporary.RootPath, ManagedReleaseManifest.FileName);
        string prefix = $"{new string('a', 240)}/{new string('b', 240)}/";
        string[] paths = Enumerable.Range(0, 15_000)
            .Select(index => $"{prefix}f{index:D5}")
            .ToArray();

        byte[] current = CreateManifestBytes(paths, minimumLauncherVersion: 2);
        Assert.InRange(current.Length, 8 * 1024 * 1024 + 1, ManagedReleaseManifest.MaximumLength);
        await File.WriteAllBytesAsync(path, current, TestContext.Current.CancellationToken);
        Assert.IsType<ReleaseManifestReadResult.Accepted>(
            await ManagedReleaseManifest.ReadAsync(path, TestContext.Current.CancellationToken));

        await File.WriteAllBytesAsync(path, CreateManifestBytes(paths, minimumLauncherVersion: 1),
            TestContext.Current.CancellationToken);
        Assert.Equal(
            new ReleaseManifestReadResult.Rejected(ManagedInstallationIssue.ReleaseManifestInvalid),
            await ManagedReleaseManifest.ReadAsync(path, TestContext.Current.CancellationToken));
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

    private static byte[] CreateManifestBytes(IReadOnlyCollection<string> additionalPaths, int minimumLauncherVersion = 2)
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
            minimumLauncherVersion,
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
}
