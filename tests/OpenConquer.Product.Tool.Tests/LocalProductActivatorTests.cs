using System.Security.Cryptography;
using System.Text.Json;

namespace OpenConquer.Product.Tool.Tests;

public sealed class LocalProductActivatorTests
{
    [Fact]
    public void ActivatePromotesFirstCandidate()
    {
        using TemporaryDirectory temporary = new();

        LocalProductPaths paths = CreatePaths(temporary);

        CreateManagedProduct(paths.CandidateProductPath, releaseSequence: 1);

        LocalProductActivator.Activate(paths);

        Assert.False(Directory.Exists(paths.CandidateProductPath));
        Assert.True(Directory.Exists(paths.ProductPath));
        Assert.False(Directory.Exists(paths.PreviousProductPath));
        Assert.Equal(1UL, ReadReleaseSequence(paths.ProductPath));
    }

    [Fact]
    public void ActivateReplacesCurrentProductAndPreservesPreviousProduct()
    {
        using TemporaryDirectory temporary = new();

        LocalProductPaths paths = CreatePaths(temporary);

        CreateManagedProduct(paths.ProductPath, releaseSequence: 10);
        CreateManagedProduct(paths.PreviousProductPath, releaseSequence: 9);
        CreateManagedProduct(paths.CandidateProductPath, releaseSequence: 11);

        LocalProductActivator.Activate(paths);

        Assert.False(Directory.Exists(paths.CandidateProductPath));
        Assert.Equal(11UL, ReadReleaseSequence(paths.ProductPath));
        Assert.Equal(10UL, ReadReleaseSequence(paths.PreviousProductPath));
    }

    [Fact]
    public void ActivateReplacesLegacyCurrentProductAndPreservesItForRecovery()
    {
        using TemporaryDirectory temporary = new();

        LocalProductPaths paths = CreatePaths(temporary);

        CreateManagedProduct(paths.ProductPath, releaseSequence: 10, legacyLayout: true);
        CreateManagedProduct(paths.CandidateProductPath, releaseSequence: 11);

        LocalProductActivator.Activate(paths);

        Assert.Equal(11UL, ReadReleaseSequence(paths.ProductPath));
        Assert.Equal(10UL, ReadReleaseSequence(paths.PreviousProductPath));
        Assert.Null(ManagedProductDescriptor.Read(paths.PreviousProductPath).ActiveRelease);
    }

    [Theory]
    [InlineData(10UL)]
    [InlineData(9UL)]
    public void ActivateRejectsReleaseSequenceRollbackWithoutChangingProducts(
        ulong candidateSequence
    )
    {
        using TemporaryDirectory temporary = new();

        LocalProductPaths paths = CreatePaths(temporary);

        CreateManagedProduct(paths.ProductPath, releaseSequence: 10);
        CreateManagedProduct(paths.CandidateProductPath, candidateSequence);

        Assert.Throws<InvalidOperationException>(() => LocalProductActivator.Activate(paths));

        Assert.Equal(10UL, ReadReleaseSequence(paths.ProductPath));
        Assert.Equal(candidateSequence, ReadReleaseSequence(paths.CandidateProductPath));
        Assert.False(Directory.Exists(paths.PreviousProductPath));
    }

    [Fact]
    public void ActivateRejectsInvalidCandidateWithoutChangingCurrentProduct()
    {
        using TemporaryDirectory temporary = new();

        LocalProductPaths paths = CreatePaths(temporary);

        CreateManagedProduct(paths.ProductPath, releaseSequence: 10);

        Directory.CreateDirectory(paths.CandidateProductPath);
        File.WriteAllText(
            Path.Combine(paths.CandidateProductPath, ManagedProductDescriptor.FileName),
            "{}"
        );

        Assert.ThrowsAny<Exception>(() => LocalProductActivator.Activate(paths));

        Assert.Equal(10UL, ReadReleaseSequence(paths.ProductPath));
        Assert.True(Directory.Exists(paths.CandidateProductPath));
        Assert.False(Directory.Exists(paths.PreviousProductPath));
    }

    [Fact]
    public void ActivateRejectsLegacyCandidate()
    {
        using TemporaryDirectory temporary = new();

        LocalProductPaths paths = CreatePaths(temporary);
        CreateManagedProduct(paths.CandidateProductPath, releaseSequence: 1,
            legacyLayout: true);

        Assert.Throws<InvalidDataException>(() => LocalProductActivator.Activate(paths));

        Assert.True(Directory.Exists(paths.CandidateProductPath));
        Assert.False(Directory.Exists(paths.ProductPath));
    }

    [Fact]
    public void ActivateRejectsConcurrentActivation()
    {
        using TemporaryDirectory temporary = new();

        LocalProductPaths paths = CreatePaths(temporary);

        CreateManagedProduct(paths.CandidateProductPath, releaseSequence: 1);

        string lockPath = Path.Combine(paths.RootPath, LocalProductActivator.LockFileName);

        using FileStream activationLock = new(
            lockPath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None
        );

        Assert.Throws<InvalidOperationException>(() => LocalProductActivator.Activate(paths));

        Assert.True(Directory.Exists(paths.CandidateProductPath));
        Assert.False(Directory.Exists(paths.ProductPath));
    }

    [Fact]
    public void ActivateRejectsLinkedActiveProduct()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using TemporaryDirectory temporary = new();

        LocalProductPaths paths = CreatePaths(temporary);

        CreateManagedProduct(paths.CandidateProductPath, releaseSequence: 2);

        string targetPath = Path.Combine(paths.RootPath, "linked-product-target");

        CreateManagedProduct(targetPath, releaseSequence: 1);
        Directory.CreateSymbolicLink(paths.ProductPath, targetPath);

        Assert.Throws<InvalidDataException>(() => LocalProductActivator.Activate(paths));

        Assert.Equal(1UL, ReadReleaseSequence(targetPath));
        Assert.Equal(2UL, ReadReleaseSequence(paths.CandidateProductPath));
    }

    private static LocalProductPaths CreatePaths(TemporaryDirectory temporary)
    {
        LocalProductPaths paths = LocalProductPaths.Create(
            temporary.RootPath,
            Guid.Parse("00112233-4455-6677-8899-aabbccddeeff")
        );

        Directory.CreateDirectory(paths.RootPath);
        Directory.CreateDirectory(paths.WorkRootPath);
        Directory.CreateDirectory(paths.WorkspacePath);

        return paths;
    }

    private static void CreateManagedProduct(
        string productPath,
        ulong releaseSequence,
        bool legacyLayout = false)
    {
        Directory.CreateDirectory(productPath);
        byte[] executable = [0x01, 0x02, 0x03, 0x04];
        string temporaryManifestPath = Path.Combine(productPath, "release.tmp");

        using (
            FileStream stream = new(
                temporaryManifestPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None
            )
        )
        using (Utf8JsonWriter writer = new(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", ProductReleaseManifest.CurrentSchemaVersion);
            writer.WriteString("productId", ProductReleaseManifest.ExpectedProductId);
            writer.WriteNumber("releaseSequence", releaseSequence);
            writer.WriteString("releaseVersion", $"local-{releaseSequence}");
            writer.WriteNumber("minimumLauncherVersion", 1);
            writer.WriteString("targetRuntime", "linux-x64");
            writer.WriteString("clientExecutable", "OpenConquer.Client");

            writer.WriteStartArray("files");
            writer.WriteStartObject();
            writer.WriteString("path", "OpenConquer.Client");
            writer.WriteNumber("length", executable.Length);
            writer.WriteString("sha256", Convert.ToHexStringLower(SHA256.HashData(executable)));
            writer.WriteEndObject();
            writer.WriteEndArray();

            writer.WriteEndObject();
        }

        byte[] manifestBytes = File.ReadAllBytes(temporaryManifestPath);
        string? releaseId = legacyLayout
            ? null
            : ProductReleaseIdentity.Create(releaseSequence, manifestBytes);
        string releasePath = legacyLayout
            ? productPath
            : Path.Combine(productPath, ManagedProductDescriptor.ReleasesRoot, releaseId!);
        string clientPath = Path.Combine(releasePath, ManagedProductDescriptor.ClientRoot);
        Directory.CreateDirectory(clientPath);
        File.WriteAllBytes(Path.Combine(clientPath, "OpenConquer.Client"), executable);
        File.Move(temporaryManifestPath, Path.Combine(releasePath, ProductReleaseManifest.FileName));

        using (
            FileStream stream = new(
                Path.Combine(releasePath, ProductReleaseSignature.FileName),
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None
            )
        )
        using (Utf8JsonWriter writer = new(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteString("keyId", $"sha256:{new string('0', 64)}");
            writer.WriteString("algorithm", ProductReleaseSignature.Algorithm);
            writer.WriteBase64String("signature", new byte[64]);
            writer.WriteEndObject();
        }

        if (legacyLayout)
        {
            File.WriteAllText(Path.Combine(productPath, ManagedProductDescriptor.FileName),
                JsonSerializer.Serialize(new
                {
                    schemaVersion = 1,
                    productId = ProductReleaseManifest.ExpectedProductId,
                    clientRoot = ManagedProductDescriptor.ClientRoot,
                }));
        }
        else
        {
            ManagedProductDescriptor.Write(productPath, releaseId!);
        }
    }

    private static ulong ReadReleaseSequence(string productPath)
    {
        return ProductReleaseManifest
            .Read(Path.Combine(ManagedProductDescriptor.GetActiveReleaseRoot(productPath),
                ProductReleaseManifest.FileName))
            .ReleaseSequence;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private readonly string _path = Path.Combine(
            Path.GetTempPath(),
            $"openconquer-local-product-activation-{Guid.NewGuid():N}"
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
