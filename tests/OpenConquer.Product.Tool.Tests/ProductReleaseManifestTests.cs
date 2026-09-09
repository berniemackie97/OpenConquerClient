using System.Text.Json;

namespace OpenConquer.Product.Tool.Tests;

public sealed class ProductReleaseManifestTests
{
    [Theory]
    [InlineData("Foo/a.dat", "foo/b.dat")]
    [InlineData("content", "CONTENT/file.dat")]
    public void ReadRejectsCrossPlatformPathTopologyCollisions(
        string firstPath,
        string secondPath)
    {
        byte[] manifest = CreateManifestBytes([firstPath, secondPath]);

        Assert.Throws<InvalidDataException>(() => ProductReleaseManifest.Read(manifest));
    }

    [Fact]
    public void ReadRejectsExcessiveDirectoryTopology()
    {
        string[] paths = Enumerable.Range(0, 68)
            .Select(index => $"d{index:D2}/{string.Join('/', Enumerable.Repeat("a", 240))}/f.dat")
            .ToArray();

        byte[] manifest = CreateManifestBytes(paths);

        Assert.Throws<InvalidDataException>(() => ProductReleaseManifest.Read(manifest));
    }

    [Fact]
    public void ReadRejectsAlternateExecutableForTargetRuntime()
    {
        byte[] manifest = CreateManifestBytes(["alternate-client"], "alternate-client");

        Assert.Throws<InvalidDataException>(() => ProductReleaseManifest.Read(manifest));
    }

    [Fact]
    public void CreateOrdersFilesOrdinallyAfterStreamingTraversal()
    {
        using TemporaryDirectory temporary = new();

        string clientRoot = Path.Combine(temporary.RootPath, "client");
        string zetaRoot = Path.Combine(clientRoot, "zeta");
        string alphaRoot = Path.Combine(clientRoot, "alpha");

        Directory.CreateDirectory(zetaRoot);
        Directory.CreateDirectory(alphaRoot);

        string targetRuntime =
            ProductTargetRuntime.Current
            ?? throw new InvalidOperationException("The current runtime is unsupported.");
        string executable = ProductTargetRuntime.ClientExecutable(targetRuntime);

        File.WriteAllText(Path.Combine(zetaRoot, "last.dat"), "zeta");
        File.WriteAllText(Path.Combine(clientRoot, executable), "client");
        File.WriteAllText(Path.Combine(alphaRoot, "first.dat"), "alpha");

        string outputPath = Path.Combine(temporary.RootPath, ProductReleaseManifest.FileName);

        ProductReleaseManifest.Create(
            new ReleaseManifestOptions(clientRoot, targetRuntime, "test-release", 1, 1, outputPath)
        );

        ProductReleaseManifest manifest = ProductReleaseManifest.Read(outputPath);

        string[] expected = [executable, "alpha/first.dat", "zeta/last.dat"];

        Array.Sort(expected, StringComparer.Ordinal);

        Assert.Equal(expected, manifest.Files.Select(file => file.Path));
    }

    [Fact]
    public void CreateRejectsLinkedDirectoryEntry()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using TemporaryDirectory temporary = new();

        string clientRoot = Path.Combine(temporary.RootPath, "client");
        string targetRoot = Path.Combine(temporary.RootPath, "linked-target");

        Directory.CreateDirectory(clientRoot);
        Directory.CreateDirectory(targetRoot);

        string targetRuntime =
            ProductTargetRuntime.Current
            ?? throw new InvalidOperationException("The current runtime is unsupported.");
        string executable = ProductTargetRuntime.ClientExecutable(targetRuntime);

        File.WriteAllText(Path.Combine(clientRoot, executable), "client");
        File.WriteAllText(Path.Combine(targetRoot, "payload.dat"), "payload");

        string linkedRoot = Path.Combine(clientRoot, "linked");
        string outputPath = Path.Combine(temporary.RootPath, ProductReleaseManifest.FileName);

        Directory.CreateSymbolicLink(linkedRoot, targetRoot);

        try
        {
            Assert.Throws<InvalidDataException>(() =>
                ProductReleaseManifest.Create(
                    new ReleaseManifestOptions(
                        clientRoot,
                        targetRuntime,
                        "test-release",
                        1,
                        1,
                        outputPath
                    )
                )
            );

            Assert.False(File.Exists(outputPath));
        }
        finally
        {
            File.Delete(linkedRoot);
        }
    }

    private static byte[] CreateManifestBytes(
        IReadOnlyCollection<string> additionalPaths,
        string? clientExecutable = null)
    {
        string targetRuntime = ProductTargetRuntime.Current ?? "linux-x64";
        string expectedExecutable = ProductTargetRuntime.ClientExecutable(targetRuntime);
        clientExecutable ??= expectedExecutable;
        string[] paths = additionalPaths
            .Append(clientExecutable)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = ProductReleaseManifest.CurrentSchemaVersion,
            productId = ProductReleaseManifest.ExpectedProductId,
            releaseSequence = 1,
            releaseVersion = "test-release",
            minimumLauncherVersion = 1,
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
        private readonly string _path = Path.Combine(
            Path.GetTempPath(),
            $"openconquer-release-manifest-{Guid.NewGuid():N}"
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
