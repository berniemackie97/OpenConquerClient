using System.Reflection;
using System.Runtime.Loader;
using OpenConquer.Launcher.Installation;

namespace OpenConquer.Launcher.Tests.Installation;

public sealed class InstallationInspectorTests
{
    [Fact]
    public async Task Inspect_IdentifiesActualGameAssemblyWithoutLoadingGameCode()
    {
        using InstallationFixture fixture = new();
        string?[] before = AssemblyLoadContext.Default.Assemblies.Select(assembly => assembly.GetName().Name).ToArray();

        InstallationInspection.Located result = Assert.IsType<InstallationInspection.Located>(await fixture.InspectAsync(TestContext.Current.CancellationToken));

        Assert.Equal(AssemblyName.GetAssemblyName(Path.Combine(fixture.Path, "OpenConquer.Client.dll")).Version, result.AssemblyVersion);
        Assert.DoesNotContain("OpenConquer.Client", before);
        Assert.DoesNotContain(AssemblyLoadContext.Default.Assemblies, assembly => assembly.GetName().Name == "OpenConquer.Client");
    }

    [Theory]
    [InlineData("OpenConquer.Client.dll")]
    [InlineData("OpenConquer.Client.runtimeconfig.json")]
    [InlineData("OpenConquer.Client.deps.json")]
    [InlineData("content/retail-5517/manifest.json")]
    public async Task Inspect_MissingFileIsActionable(string relativePath)
    {
        using InstallationFixture fixture = new();
        File.Delete(Path.Combine(fixture.Path, relativePath));
        await AssertIssueAsync(fixture, InstallationIssue.MissingFiles);
    }

    [Fact]
    public async Task Inspect_RejectsAnotherProductRenamedAsTheGame()
    {
        using InstallationFixture fixture = new();
        File.Copy(typeof(Program).Assembly.Location, Path.Combine(fixture.Path, "OpenConquer.Client.dll"), overwrite: true);
        await AssertIssueAsync(fixture, InstallationIssue.InvalidLayout);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{")]
    [InlineData("{\"runtimeOptions\":{\"tfm\":\"net10.0\",\"tfm\":\"net10.0\"}}")]
    public async Task Inspect_RejectsMalformedOrAmbiguousJson(string json)
    {
        using InstallationFixture fixture = new();
        File.WriteAllText(Path.Combine(fixture.Path, "OpenConquer.Client.runtimeconfig.json"), json);
        await AssertIssueAsync(fixture, InstallationIssue.InvalidLayout);
    }

    [Fact]
    public async Task Inspect_ReportsUnsupportedFrameworkLayout()
    {
        using InstallationFixture fixture = new();
        File.WriteAllText(Path.Combine(fixture.Path, "OpenConquer.Client.runtimeconfig.json"), "{\"runtimeOptions\":{\"tfm\":\"net99.0\"}}");
        await AssertIssueAsync(fixture, InstallationIssue.UnsupportedLayout);
    }

    [Fact]
    public async Task Inspect_RejectsMissingGameRuntimeAsset()
    {
        using InstallationFixture fixture = new();
        File.WriteAllText(Path.Combine(fixture.Path, "OpenConquer.Client.deps.json"), "{\"runtimeTarget\":{\"name\":\"target\"},\"targets\":{\"target\":{}}}");
        await AssertIssueAsync(fixture, InstallationIssue.InvalidLayout);
    }

    [Theory]
    [InlineData("OpenConquer.Client.dll", 16777217)]
    [InlineData("OpenConquer.Client.runtimeconfig.json", 1048577)]
    [InlineData("OpenConquer.Client.deps.json", 1048577)]
    [InlineData("OpenConquer.Client.dll", 0)]
    public async Task Inspect_RejectsUnboundedOrEmptyInput(string name, int length)
    {
        using InstallationFixture fixture = new();
        using (FileStream file = File.OpenWrite(Path.Combine(fixture.Path, name)))
        {
            file.SetLength(length);
        }

        await AssertIssueAsync(fixture, InstallationIssue.InvalidLayout);
    }

    [Fact]
    public async Task Inspect_RejectsDamagedPeImage()
    {
        using InstallationFixture fixture = new();
        File.WriteAllBytes(Path.Combine(fixture.Path, "OpenConquer.Client.dll"), [0, 1, 2, 3]);
        await AssertIssueAsync(fixture, InstallationIssue.InvalidLayout);
    }

    [Fact]
    public async Task Inspect_RejectsDirectoryInPlaceOfAFile()
    {
        using InstallationFixture fixture = new();
        string path = Path.Combine(fixture.Path, "OpenConquer.Client.dll");
        File.Delete(path);
        Directory.CreateDirectory(path);
        await AssertIssueAsync(fixture, InstallationIssue.InvalidLayout);
    }

    [Fact]
    public async Task Inspect_RejectsLinkedGameFile()
    {
        using InstallationFixture fixture = new();
        string path = Path.Combine(fixture.Path, "OpenConquer.Client.dll");
        string target = Path.Combine(fixture.Path, "actual-game.dll");
        File.Move(path, target);
        File.CreateSymbolicLink(path, target);
        await AssertIssueAsync(fixture, InstallationIssue.LinkedPath);
    }

    [Fact]
    public async Task Inspect_RejectsLinkedContentDirectory()
    {
        using InstallationFixture fixture = new();
        string path = Path.Combine(fixture.Path, "content");
        string target = Path.Combine(fixture.Path, "actual-content");
        Directory.Move(path, target);
        Directory.CreateSymbolicLink(path, target);
        await AssertIssueAsync(fixture, InstallationIssue.LinkedPath);
    }

    [Fact]
    public async Task Inspect_CancelledRequestDoesNotBecomeAFileFailure()
    {
        using InstallationFixture fixture = new();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.InspectAsync(cancellation.Token));
    }

    [Fact]
    public async Task Inspect_ReleasesFilesSoAnInstallationCanBeReplaced()
    {
        using InstallationFixture fixture = new();
        await fixture.InspectAsync(TestContext.Current.CancellationToken);
        using FileStream exclusive = File.Open(Path.Combine(fixture.Path, "OpenConquer.Client.dll"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(exclusive.CanWrite);
    }

    private static async Task AssertIssueAsync(InstallationFixture fixture, InstallationIssue issue)
    {
        InstallationInspection.Rejected result = Assert.IsType<InstallationInspection.Rejected>(await fixture.InspectAsync());
        Assert.Equal(issue, result.Issue);
    }

    private sealed class InstallationFixture : IDisposable
    {
        private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("OpenConquer-installation-");

        public InstallationFixture()
        {
            try
            {
                File.Copy(System.IO.Path.Combine(AppContext.BaseDirectory, "TestData", "installation", "OpenConquer.Client.dll"), System.IO.Path.Combine(Path, "OpenConquer.Client.dll"));
                File.WriteAllText(System.IO.Path.Combine(Path, "OpenConquer.Client.runtimeconfig.json"), "{\"runtimeOptions\":{\"tfm\":\"net10.0\"}}");
                File.WriteAllText(System.IO.Path.Combine(Path, "OpenConquer.Client.deps.json"), "{\"runtimeTarget\":{\"name\":\"target\"},\"targets\":{\"target\":{\"OpenConquer.Client/1.0.0\":{\"runtime\":{\"OpenConquer.Client.dll\":{}}}}}}");
                Directory.CreateDirectory(System.IO.Path.Combine(Path, "content", "retail-5517", "payload"));
                File.WriteAllText(System.IO.Path.Combine(Path, "content", "retail-5517", "manifest.json"), "{}");
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public string Path => _directory.FullName;

        public Task<InstallationInspection> InspectAsync(CancellationToken cancellationToken = default)
        {
            Assert.True(InstallationRoot.TryCreate(Path, out InstallationRoot? root));
            return new InstallationInspector().InspectAsync(root, cancellationToken);
        }

        public void Dispose() => _directory.Delete(recursive: true);
    }
}
