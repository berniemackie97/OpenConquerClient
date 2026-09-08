using System.Text.Json;

namespace OpenConquer.Product.Tool.Tests;

public sealed class ManagedProductStagerTests
{
    private const string TestTargetRuntime = "linux-x64";
    private const string TestClientExecutable = "OpenConquer.Client";

    [Fact]
    public void StageComposesManagedProductAndWritesDescriptorAndReleaseMetadata()
    {
        using TemporaryDirectory temporary = new();

        string launcherRoot = temporary.CreateDirectory("launcher");
        string clientRoot = temporary.CreateDirectory("client");
        string outputRoot = Path.Combine(temporary.RootPath, "output");

        File.WriteAllText(Path.Combine(launcherRoot, "OpenConquer.Launcher"), "launcher");

        File.WriteAllText(Path.Combine(clientRoot, TestClientExecutable), "client");

        ProductStageOptions options = CreateStageOptions(
            temporary,
            launcherRoot,
            clientRoot,
            outputRoot
        );

        byte[] expectedManifest = File.ReadAllBytes(options.ReleaseManifestPath);

        byte[] expectedSignature = File.ReadAllBytes(options.ReleaseSignaturePath);

        ManagedProductStager.Stage(options);

        Assert.Equal(
            "launcher",
            File.ReadAllText(Path.Combine(outputRoot, "OpenConquer.Launcher"))
        );

        string descriptorPath = Path.Combine(outputRoot, ManagedProductDescriptor.FileName);

        Assert.True(File.Exists(descriptorPath));

        using JsonDocument descriptor = JsonDocument.Parse(File.ReadAllBytes(descriptorPath));

        JsonElement root = descriptor.RootElement;

        Assert.Equal(2, root.GetProperty("schemaVersion").GetInt32());

        Assert.Equal("OpenConquer", root.GetProperty("productId").GetString());

        string releaseId = Assert.IsType<string>(root.GetProperty("activeRelease").GetString());
        Assert.True(ProductReleaseIdentity.IsValid(releaseId));
        Assert.Equal(JsonValueKind.Null, root.GetProperty("fallbackRelease").ValueKind);

        Assert.Equal(4, root.EnumerateObject().Count());

        string activeReleaseRoot = ManagedProductDescriptor.GetActiveReleaseRoot(outputRoot);

        Assert.Equal(
            "client",
            File.ReadAllText(Path.Combine(activeReleaseRoot,
                ManagedProductDescriptor.ClientRoot, TestClientExecutable))
        );

        Assert.Equal(
            expectedManifest,
            File.ReadAllBytes(Path.Combine(activeReleaseRoot, ProductReleaseManifest.FileName))
        );

        Assert.Equal(
            expectedSignature,
            File.ReadAllBytes(Path.Combine(activeReleaseRoot, ProductReleaseSignature.FileName))
        );
    }

    [Fact]
    public void StageDoesNotPublishInternalCopySentinels()
    {
        using TemporaryDirectory temporary = new();

        string launcherRoot = temporary.CreateDirectory("launcher");

        string launcherNestedRoot = Directory
            .CreateDirectory(Path.Combine(launcherRoot, "nested"))
            .FullName;

        string clientRoot = temporary.CreateDirectory("client");

        string clientNestedRoot = Directory
            .CreateDirectory(Path.Combine(clientRoot, "nested"))
            .FullName;

        string outputRoot = Path.Combine(temporary.RootPath, "output");

        File.WriteAllText(Path.Combine(launcherNestedRoot, "launcher.txt"), "launcher");

        File.WriteAllText(Path.Combine(clientRoot, TestClientExecutable), "client");

        File.WriteAllText(Path.Combine(clientNestedRoot, "client.txt"), "client-data");

        ManagedProductStager.Stage(
            CreateStageOptions(temporary, launcherRoot, clientRoot, outputRoot)
        );

        Assert.Empty(
            Directory.EnumerateFileSystemEntries(
                outputRoot,
                ".openconquer-copy-guard-*",
                SearchOption.AllDirectories
            )
        );
    }

    [Fact]
    public void StageRejectsLauncherPublishContainingInstallationDescriptor()
    {
        using TemporaryDirectory temporary = new();

        string launcherRoot = temporary.CreateDirectory("launcher");

        string clientRoot = temporary.CreateDirectory("client");

        string outputRoot = Path.Combine(temporary.RootPath, "output");

        File.WriteAllText(Path.Combine(launcherRoot, ManagedProductDescriptor.FileName), "{}");

        File.WriteAllText(Path.Combine(clientRoot, TestClientExecutable), "client");

        ProductStageOptions options = CreateStageOptions(
            temporary,
            launcherRoot,
            clientRoot,
            outputRoot
        );

        Assert.Throws<InvalidDataException>(() => ManagedProductStager.Stage(options));

        Assert.False(Directory.Exists(outputRoot));
    }

    [Fact]
    public void StageRejectsLauncherPublishContainingReservedClientComponent()
    {
        using TemporaryDirectory temporary = new();

        string launcherRoot = temporary.CreateDirectory("launcher");

        string clientRoot = temporary.CreateDirectory("client-publish");

        string outputRoot = Path.Combine(temporary.RootPath, "output");

        Directory.CreateDirectory(Path.Combine(launcherRoot, ManagedProductDescriptor.ClientRoot));

        File.WriteAllText(Path.Combine(clientRoot, TestClientExecutable), "client");

        ProductStageOptions options = CreateStageOptions(
            temporary,
            launcherRoot,
            clientRoot,
            outputRoot
        );

        Assert.Throws<InvalidDataException>(() => ManagedProductStager.Stage(options));

        Assert.False(Directory.Exists(outputRoot));
    }

    [Fact]
    public void StageRejectsLauncherPublishContainingReservedReleaseGenerations()
    {
        using TemporaryDirectory temporary = new();

        string launcherRoot = temporary.CreateDirectory("launcher");
        string clientRoot = temporary.CreateDirectory("client-publish");
        string outputRoot = Path.Combine(temporary.RootPath, "output");

        Directory.CreateDirectory(Path.Combine(launcherRoot,
            ManagedProductDescriptor.ReleasesRoot));
        File.WriteAllText(Path.Combine(clientRoot, TestClientExecutable), "client");

        ProductStageOptions options = CreateStageOptions(
            temporary,
            launcherRoot,
            clientRoot,
            outputRoot
        );

        Assert.Throws<InvalidDataException>(() => ManagedProductStager.Stage(options));
        Assert.False(Directory.Exists(outputRoot));
    }

    [Fact]
    public void StageRejectsLauncherPublishContainingReservedReleaseMetadata()
    {
        using TemporaryDirectory temporary = new();

        string launcherRoot = temporary.CreateDirectory("launcher");

        string clientRoot = temporary.CreateDirectory("client");

        string outputRoot = Path.Combine(temporary.RootPath, "output");

        File.WriteAllText(Path.Combine(launcherRoot, ProductReleaseManifest.FileName), "{}");

        File.WriteAllText(Path.Combine(clientRoot, TestClientExecutable), "client");

        ProductStageOptions options = CreateStageOptions(
            temporary,
            launcherRoot,
            clientRoot,
            outputRoot
        );

        Assert.Throws<InvalidDataException>(() => ManagedProductStager.Stage(options));

        Assert.False(Directory.Exists(outputRoot));
    }

    [Fact]
    public void StageRejectsExistingOutputWithoutDeletingExistingFiles()
    {
        using TemporaryDirectory temporary = new();

        string launcherRoot = temporary.CreateDirectory("launcher");

        string clientRoot = temporary.CreateDirectory("client");

        string outputRoot = temporary.CreateDirectory("output");

        string existingFile = Path.Combine(outputRoot, "keep.txt");

        File.WriteAllText(Path.Combine(clientRoot, TestClientExecutable), "client");

        File.WriteAllText(existingFile, "keep");

        ProductStageOptions options = CreateStageOptions(
            temporary,
            launcherRoot,
            clientRoot,
            outputRoot
        );

        Assert.Throws<InvalidOperationException>(() => ManagedProductStager.Stage(options));

        Assert.Equal("keep", File.ReadAllText(existingFile));
    }

    [Fact]
    public void StageRejectsEmptyClientPublish()
    {
        using TemporaryDirectory temporary = new();

        string launcherRoot = temporary.CreateDirectory("launcher");

        string clientRoot = temporary.CreateDirectory("client");

        string outputRoot = Path.Combine(temporary.RootPath, "output");

        File.WriteAllText(Path.Combine(launcherRoot, "OpenConquer.Launcher"), "launcher");

        ProductStageOptions options = CreateStageOptions(
            temporary,
            launcherRoot,
            clientRoot,
            outputRoot,
            createManifestFromClient: false
        );

        Assert.Throws<InvalidDataException>(() => ManagedProductStager.Stage(options));

        Assert.False(Directory.Exists(outputRoot));

        Assert.Empty(Directory.EnumerateDirectories(temporary.RootPath, "output.staging-*"));
    }

    [Fact]
    public void StageRejectsLinkedEntriesBeforeCopyingThem()
    {
        using TemporaryDirectory temporary = new();

        string launcherRoot = temporary.CreateDirectory("launcher");

        string clientRoot = temporary.CreateDirectory("client");

        string outputRoot = Path.Combine(temporary.RootPath, "output");

        string clientExecutable = Path.Combine(clientRoot, TestClientExecutable);

        File.WriteAllText(clientExecutable, "client");

        string linkedFile = Path.Combine(launcherRoot, "linked");

        try
        {
            File.CreateSymbolicLink(linkedFile, clientExecutable);
        }
        catch (PlatformNotSupportedException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        ProductStageOptions options = CreateStageOptions(
            temporary,
            launcherRoot,
            clientRoot,
            outputRoot
        );

        Assert.Throws<InvalidDataException>(() => ManagedProductStager.Stage(options));

        Assert.False(Directory.Exists(outputRoot));
    }

    [Fact]
    public void StageRemovesTemporaryOutputWhenClientCopyFails()
    {
        using TemporaryDirectory temporary = new();

        string launcherRoot = temporary.CreateDirectory("launcher");

        string clientRoot = temporary.CreateDirectory("client");

        string outputRoot = Path.Combine(temporary.RootPath, "output");

        string clientFile = Path.Combine(clientRoot, TestClientExecutable);

        File.WriteAllText(clientFile, "client");

        string linkedFile = Path.Combine(clientRoot, "linked");

        try
        {
            File.CreateSymbolicLink(linkedFile, clientFile);
        }
        catch (PlatformNotSupportedException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        ProductStageOptions options = CreateStageOptions(
            temporary,
            launcherRoot,
            clientRoot,
            outputRoot,
            createManifestFromClient: false
        );

        Assert.Throws<InvalidDataException>(() => ManagedProductStager.Stage(options));

        Assert.False(Directory.Exists(outputRoot));

        Assert.Empty(Directory.EnumerateDirectories(temporary.RootPath, "output.staging-*"));
    }

    [Fact]
    public void StageRejectsLinkedPublishRoots()
    {
        using TemporaryDirectory temporary = new();

        string launcherRoot = temporary.CreateDirectory("launcher");

        string clientRoot = temporary.CreateDirectory("client");

        string outputRoot = Path.Combine(temporary.RootPath, "output");

        string linkedClientRoot = Path.Combine(temporary.RootPath, "linked-client");

        File.WriteAllText(Path.Combine(clientRoot, TestClientExecutable), "client");

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

        ProductStageOptions options = CreateStageOptions(
            temporary,
            launcherRoot,
            linkedClientRoot,
            outputRoot,
            createManifestFromClient: false
        );

        Assert.Throws<InvalidDataException>(() => ManagedProductStager.Stage(options));

        Assert.False(Directory.Exists(outputRoot));
    }

    [Fact]
    public void StageAllowsPublishRootThroughResolvedAncestor()
    {
        using TemporaryDirectory temporary = new();

        string actualParent = temporary.CreateDirectory("actual-source-parent");

        string launcherRoot = Directory
            .CreateDirectory(Path.Combine(actualParent, "launcher"))
            .FullName;

        string clientRoot = temporary.CreateDirectory("client");

        string linkedParent = Path.Combine(temporary.RootPath, "linked-source-parent");

        string linkedLauncherRoot = Path.Combine(linkedParent, "launcher");

        string outputRoot = Path.Combine(temporary.RootPath, "output");

        try
        {
            Directory.CreateSymbolicLink(linkedParent, actualParent);
        }
        catch (PlatformNotSupportedException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        File.WriteAllText(Path.Combine(launcherRoot, "OpenConquer.Launcher"), "launcher");

        File.WriteAllText(Path.Combine(clientRoot, TestClientExecutable), "client");

        ManagedProductStager.Stage(
            CreateStageOptions(temporary, linkedLauncherRoot, clientRoot, outputRoot)
        );

        Assert.Equal(
            "launcher",
            File.ReadAllText(Path.Combine(outputRoot, "OpenConquer.Launcher"))
        );
    }

    [Fact]
    public void RejectOverlappingRootsPreservesResolvedSpellingAcrossNestedAliases()
    {
        using TemporaryDirectory temporary = new();

        string unicodeParent = temporary.CreateDirectory("cafe\u0301");

        string actualLauncherParent = temporary.CreateDirectory("actual-launcher-parent");

        string actualLauncherRoot = Directory
            .CreateDirectory(Path.Combine(actualLauncherParent, "publish"))
            .FullName;

        string clientRoot = temporary.CreateDirectory("client");

        string outerAlias = Path.Combine(temporary.RootPath, "outer-alias");

        string nestedAlias = Path.Combine(unicodeParent, "nested-alias");

        try
        {
            Directory.CreateSymbolicLink(outerAlias, unicodeParent);

            Directory.CreateSymbolicLink(nestedAlias, actualLauncherParent);
        }
        catch (PlatformNotSupportedException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        string launcherThroughAliases = Path.Combine(outerAlias, "nested-alias", "publish");

        string outputRoot = Path.Combine(actualLauncherRoot, "output");

        Assert.Throws<InvalidOperationException>(() =>
            ProductStagingPathGuard.RejectOverlappingRoots(
                launcherThroughAliases,
                clientRoot,
                outputRoot
            )
        );
    }

    [Fact]
    public void StageRejectsOutputAncestorResolvingInsideLauncherPublish()
    {
        using TemporaryDirectory temporary = new();

        string launcherRoot = temporary.CreateDirectory("launcher");

        string clientRoot = temporary.CreateDirectory("client");

        string linkedOutputParent = Path.Combine(temporary.RootPath, "linked-output-parent");

        string outputRoot = Path.Combine(linkedOutputParent, "output");

        try
        {
            Directory.CreateSymbolicLink(linkedOutputParent, launcherRoot);
        }
        catch (PlatformNotSupportedException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        File.WriteAllText(Path.Combine(launcherRoot, "OpenConquer.Launcher"), "launcher");

        File.WriteAllText(Path.Combine(clientRoot, TestClientExecutable), "client");

        ProductStageOptions options = CreateStageOptions(
            temporary,
            launcherRoot,
            clientRoot,
            outputRoot
        );

        Assert.Throws<InvalidOperationException>(() => ManagedProductStager.Stage(options));

        Assert.False(Directory.Exists(outputRoot));
    }

    [Fact]
    public void StageRejectsDanglingLinkedOutput()
    {
        using TemporaryDirectory temporary = new();

        string launcherRoot = temporary.CreateDirectory("launcher");

        string clientRoot = temporary.CreateDirectory("client");

        string outputRoot = Path.Combine(temporary.RootPath, "output");

        string missingTarget = Path.Combine(temporary.RootPath, "missing-output-target");

        try
        {
            Directory.CreateSymbolicLink(outputRoot, missingTarget);
        }
        catch (PlatformNotSupportedException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        File.WriteAllText(Path.Combine(launcherRoot, "OpenConquer.Launcher"), "launcher");

        File.WriteAllText(Path.Combine(clientRoot, TestClientExecutable), "client");

        ProductStageOptions options = CreateStageOptions(
            temporary,
            launcherRoot,
            clientRoot,
            outputRoot
        );

        Assert.Throws<InvalidDataException>(() => ManagedProductStager.Stage(options));

        Assert.False(Directory.Exists(missingTarget));
    }

    [Fact]
    public void RejectOverlappingRootsUsesPortableCaseInsensitiveIdentity()
    {
        string temporaryRoot = Path.Combine(Path.GetTempPath(), "OpenConquer-Portable-Identity");

        string launcherRoot = Path.Combine(temporaryRoot, "Launcher");

        string clientRoot = Path.Combine(temporaryRoot, "Client");

        string outputRoot = Path.Combine(temporaryRoot.ToLowerInvariant(), "launcher", "output");

        Assert.Throws<InvalidOperationException>(() =>
            ProductStagingPathGuard.RejectOverlappingRoots(launcherRoot, clientRoot, outputRoot)
        );
    }

    [Fact]
    public void RejectOverlappingRootsUsesCanonicalUnicodeIdentity()
    {
        string temporaryRoot = Path.Combine(Path.GetTempPath(), "openconquer-product-identity");

        string launcherRoot = Path.Combine(temporaryRoot, "caf\u00E9");

        string clientRoot = Path.Combine(temporaryRoot, "client");

        string outputRoot = Path.Combine(temporaryRoot, "cafe\u0301", "output");

        Assert.Throws<InvalidOperationException>(() =>
            ProductStagingPathGuard.RejectOverlappingRoots(launcherRoot, clientRoot, outputRoot)
        );
    }

    [Fact]
    public void StageRejectsOverlappingInputAndOutputRoots()
    {
        using TemporaryDirectory temporary = new();

        string launcherRoot = temporary.CreateDirectory("launcher");

        string clientRoot = temporary.CreateDirectory("client");

        string outputRoot = Path.Combine(launcherRoot, "output");

        ProductStageOptions options = CreateStageOptions(
            temporary,
            launcherRoot,
            clientRoot,
            outputRoot,
            createManifestFromClient: false
        );

        Assert.Throws<InvalidOperationException>(() => ManagedProductStager.Stage(options));

        Assert.False(Directory.Exists(outputRoot));
    }

    [Fact]
    public void StageSanitizesUnixModesAndPreservesExecutableIntent()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using TemporaryDirectory temporary = new();

        string launcherRoot = temporary.CreateDirectory("launcher");

        string clientRoot = temporary.CreateDirectory("client");

        string outputRoot = Path.Combine(temporary.RootPath, "output");

        string clientExecutable = Path.Combine(clientRoot, TestClientExecutable);

        File.WriteAllText(clientExecutable, "client");

        UnixFileMode expectedMode =
            UnixFileMode.UserRead
            | UnixFileMode.UserWrite
            | UnixFileMode.UserExecute
            | UnixFileMode.GroupRead
            | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead
            | UnixFileMode.OtherExecute;

        File.SetUnixFileMode(clientExecutable, (UnixFileMode)0xfff);

        ProductStageOptions options = CreateStageOptions(
            temporary, launcherRoot, clientRoot, outputRoot);
        File.SetUnixFileMode(options.ReleaseManifestPath, (UnixFileMode)0xfff);
        File.SetUnixFileMode(options.ReleaseSignaturePath, (UnixFileMode)0xfff);

        ManagedProductStager.Stage(options);

        string activeReleaseRoot = ManagedProductDescriptor.GetActiveReleaseRoot(outputRoot);
        string stagedExecutable = Path.Combine(
            activeReleaseRoot,
            ManagedProductDescriptor.ClientRoot,
            TestClientExecutable
        );

        Assert.Equal(expectedMode, File.GetUnixFileMode(stagedExecutable));
        Assert.Equal(expectedMode, File.GetUnixFileMode(outputRoot));
        Assert.Equal(expectedMode, File.GetUnixFileMode(Path.Combine(outputRoot,
            ManagedProductDescriptor.ReleasesRoot)));
        Assert.Equal(expectedMode, File.GetUnixFileMode(activeReleaseRoot));
        UnixFileMode dataMode = UnixFileMode.UserRead | UnixFileMode.UserWrite |
            UnixFileMode.GroupRead | UnixFileMode.OtherRead;
        Assert.Equal(dataMode, File.GetUnixFileMode(Path.Combine(activeReleaseRoot,
            ProductReleaseManifest.FileName)));
        Assert.Equal(dataMode, File.GetUnixFileMode(Path.Combine(activeReleaseRoot,
            ProductReleaseSignature.FileName)));
        Assert.Equal(dataMode, File.GetUnixFileMode(Path.Combine(outputRoot,
            ManagedProductDescriptor.FileName)));
    }

    [Fact]
    public void StageRejectsClientMutationAfterManifestCreation()
    {
        using TemporaryDirectory temporary = new();

        string launcherRoot = temporary.CreateDirectory("launcher");

        string clientRoot = temporary.CreateDirectory("client");

        string outputRoot = Path.Combine(temporary.RootPath, "output");

        string clientExecutable = Path.Combine(clientRoot, TestClientExecutable);

        File.WriteAllText(clientExecutable, "client");

        ProductStageOptions options = CreateStageOptions(
            temporary,
            launcherRoot,
            clientRoot,
            outputRoot
        );

        File.AppendAllText(clientExecutable, "-mutated");

        Assert.Throws<InvalidDataException>(() => ManagedProductStager.Stage(options));

        Assert.False(Directory.Exists(outputRoot));

        Assert.Empty(Directory.EnumerateDirectories(temporary.RootPath, "output.staging-*"));
    }

    [Fact]
    public void StageRejectsMalformedReleaseSignatureWithoutCreatingOutput()
    {
        using TemporaryDirectory temporary = new();

        string launcherRoot = temporary.CreateDirectory("launcher");

        string clientRoot = temporary.CreateDirectory("client");

        string outputRoot = Path.Combine(temporary.RootPath, "output");

        File.WriteAllText(Path.Combine(clientRoot, TestClientExecutable), "client");

        ProductStageOptions options = CreateStageOptions(
            temporary,
            launcherRoot,
            clientRoot,
            outputRoot
        );

        File.WriteAllText(options.ReleaseSignaturePath, "{}");

        Assert.Throws<InvalidDataException>(() => ManagedProductStager.Stage(options));

        Assert.False(Directory.Exists(outputRoot));

        Assert.Empty(Directory.EnumerateDirectories(temporary.RootPath, "output.staging-*"));
    }

    [Fact]
    public void StageRejectsReleaseMetadataInsideLauncherPublish()
    {
        using TemporaryDirectory temporary = new();

        string launcherRoot = temporary.CreateDirectory("launcher");

        string clientRoot = temporary.CreateDirectory("client");

        string outputRoot = Path.Combine(temporary.RootPath, "output");

        File.WriteAllText(Path.Combine(clientRoot, TestClientExecutable), "client");

        ProductStageOptions externalOptions = CreateStageOptions(
            temporary,
            launcherRoot,
            clientRoot,
            outputRoot
        );

        string embeddedSignature = Path.Combine(launcherRoot, ProductReleaseSignature.FileName);

        File.Copy(externalOptions.ReleaseSignaturePath, embeddedSignature);

        ProductStageOptions options = new(
            launcherRoot,
            clientRoot,
            externalOptions.ReleaseManifestPath,
            embeddedSignature,
            outputRoot
        );

        Assert.Throws<InvalidOperationException>(() => ManagedProductStager.Stage(options));

        Assert.False(Directory.Exists(outputRoot));
    }

    private static ProductStageOptions CreateStageOptions(
        TemporaryDirectory temporary,
        string launcherRoot,
        string clientRoot,
        string outputRoot,
        bool createManifestFromClient = true
    )
    {
        string metadataRoot = temporary.CreateDirectory($"release-{Guid.NewGuid():N}");

        string manifestPath = Path.Combine(metadataRoot, ProductReleaseManifest.FileName);

        string signaturePath = Path.Combine(metadataRoot, ProductReleaseSignature.FileName);

        if (createManifestFromClient)
        {
            ProductReleaseManifest.Create(
                new ReleaseManifestOptions(
                    clientRoot,
                    TestTargetRuntime,
                    "test-release",
                    ReleaseSequence: 1,
                    MinimumLauncherVersion: 1,
                    manifestPath
                )
            );
        }
        else
        {
            WritePlaceholderReleaseManifest(manifestPath);
        }

        WriteSignatureEnvelope(signaturePath);

        return new ProductStageOptions(
            launcherRoot,
            clientRoot,
            manifestPath,
            signaturePath,
            outputRoot
        );
    }

    private static void WritePlaceholderReleaseManifest(string path)
    {
        using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);

        using Utf8JsonWriter writer = new(stream);

        writer.WriteStartObject();

        writer.WriteNumber("schemaVersion", ProductReleaseManifest.CurrentSchemaVersion);

        writer.WriteString("productId", ProductReleaseManifest.ExpectedProductId);

        writer.WriteNumber("releaseSequence", 1);

        writer.WriteString("releaseVersion", "test-release");

        writer.WriteNumber("minimumLauncherVersion", 1);

        writer.WriteString("targetRuntime", TestTargetRuntime);

        writer.WriteString("clientExecutable", TestClientExecutable);

        writer.WriteStartArray("files");

        writer.WriteStartObject();

        writer.WriteString("path", TestClientExecutable);

        writer.WriteNumber("length", 6);

        writer.WriteString("sha256", new string('0', 64));

        writer.WriteEndObject();

        writer.WriteEndArray();

        writer.WriteEndObject();

        writer.Flush();
    }

    private static void WriteSignatureEnvelope(string path)
    {
        using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);

        using Utf8JsonWriter writer = new(stream);

        writer.WriteStartObject();

        writer.WriteNumber("schemaVersion", 1);

        writer.WriteString("keyId", "sha256:" + new string('0', 64));

        writer.WriteString("algorithm", ProductReleaseSignature.Algorithm);

        writer.WriteBase64String("signature", new byte[64]);

        writer.WriteEndObject();

        writer.Flush();
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private readonly string _path = Path.Combine(
            Path.GetTempPath(),
            $"openconquer-product-tool-{Guid.NewGuid():N}"
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
