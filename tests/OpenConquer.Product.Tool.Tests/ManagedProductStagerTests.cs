using System.Text.Json;

namespace OpenConquer.Product.Tool.Tests;

public sealed class ManagedProductStagerTests
{
    [Fact]
    public void StageComposesManagedProductAndWritesDescriptor()
    {
        using TemporaryDirectory temporary = new();

        string launcherRoot = temporary.CreateDirectory("launcher");
        string clientRoot = temporary.CreateDirectory("client");
        string outputRoot = Path.Combine(temporary.RootPath, "output");

        File.WriteAllText(Path.Combine(launcherRoot, "OpenConquer.Launcher"), "launcher");

        File.WriteAllText(Path.Combine(clientRoot, "OpenConquer.Client"), "client");

        ManagedProductStager.Stage(new ProductStageOptions(launcherRoot, clientRoot, outputRoot));

        Assert.Equal(
            "launcher",
            File.ReadAllText(Path.Combine(outputRoot, "OpenConquer.Launcher"))
        );

        Assert.Equal(
            "client",
            File.ReadAllText(
                Path.Combine(outputRoot, ManagedProductDescriptor.ClientRoot, "OpenConquer.Client")
            )
        );

        string descriptorPath = Path.Combine(outputRoot, ManagedProductDescriptor.FileName);

        Assert.True(File.Exists(descriptorPath));

        using JsonDocument descriptor = JsonDocument.Parse(File.ReadAllBytes(descriptorPath));

        JsonElement root = descriptor.RootElement;

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());

        Assert.Equal("OpenConquer", root.GetProperty("productId").GetString());

        Assert.Equal(
            ManagedProductDescriptor.ClientRoot,
            root.GetProperty("clientRoot").GetString()
        );

        Assert.Equal(3, root.EnumerateObject().Count());
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

        File.WriteAllText(Path.Combine(clientNestedRoot, "client.txt"), "client");

        ManagedProductStager.Stage(new ProductStageOptions(launcherRoot, clientRoot, outputRoot));

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

        File.WriteAllText(Path.Combine(clientRoot, "OpenConquer.Client"), "client");

        Assert.Throws<InvalidDataException>(() =>
            ManagedProductStager.Stage(
                new ProductStageOptions(launcherRoot, clientRoot, outputRoot)
            )
        );

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

        File.WriteAllText(Path.Combine(clientRoot, "OpenConquer.Client"), "client");

        Assert.Throws<InvalidDataException>(() =>
            ManagedProductStager.Stage(
                new ProductStageOptions(launcherRoot, clientRoot, outputRoot)
            )
        );

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

        File.WriteAllText(Path.Combine(clientRoot, "OpenConquer.Client"), "client");

        File.WriteAllText(existingFile, "keep");

        Assert.Throws<InvalidOperationException>(() =>
            ManagedProductStager.Stage(
                new ProductStageOptions(launcherRoot, clientRoot, outputRoot)
            )
        );

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

        Assert.Throws<InvalidDataException>(() =>
            ManagedProductStager.Stage(
                new ProductStageOptions(launcherRoot, clientRoot, outputRoot)
            )
        );

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

        string clientExecutable = Path.Combine(clientRoot, "OpenConquer.Client");

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

        Assert.Throws<InvalidDataException>(() =>
            ManagedProductStager.Stage(
                new ProductStageOptions(launcherRoot, clientRoot, outputRoot)
            )
        );

        Assert.False(Directory.Exists(outputRoot));
    }

    [Fact]
    public void StageRemovesTemporaryOutputWhenClientCopyFails()
    {
        using TemporaryDirectory temporary = new();

        string launcherRoot = temporary.CreateDirectory("launcher");

        string clientRoot = temporary.CreateDirectory("client");

        string outputRoot = Path.Combine(temporary.RootPath, "output");

        string clientFile = Path.Combine(clientRoot, "OpenConquer.Client");

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

        Assert.Throws<InvalidDataException>(() =>
            ManagedProductStager.Stage(
                new ProductStageOptions(launcherRoot, clientRoot, outputRoot)
            )
        );

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

        File.WriteAllText(Path.Combine(clientRoot, "OpenConquer.Client"), "client");

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

        Assert.Throws<InvalidDataException>(() =>
            ManagedProductStager.Stage(
                new ProductStageOptions(launcherRoot, linkedClientRoot, outputRoot)
            )
        );

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

        File.WriteAllText(Path.Combine(clientRoot, "OpenConquer.Client"), "client");

        ManagedProductStager.Stage(
            new ProductStageOptions(linkedLauncherRoot, clientRoot, outputRoot)
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

        File.WriteAllText(Path.Combine(clientRoot, "OpenConquer.Client"), "client");

        Assert.Throws<InvalidOperationException>(() =>
            ManagedProductStager.Stage(
                new ProductStageOptions(launcherRoot, clientRoot, outputRoot)
            )
        );

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

        File.WriteAllText(Path.Combine(clientRoot, "OpenConquer.Client"), "client");

        Assert.Throws<InvalidDataException>(() =>
            ManagedProductStager.Stage(
                new ProductStageOptions(launcherRoot, clientRoot, outputRoot)
            )
        );

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

        Assert.Throws<InvalidOperationException>(() =>
            ManagedProductStager.Stage(
                new ProductStageOptions(launcherRoot, clientRoot, outputRoot)
            )
        );

        Assert.False(Directory.Exists(outputRoot));
    }

    [Fact]
    public void StagePreservesUnixExecutableMode()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using TemporaryDirectory temporary = new();

        string launcherRoot = temporary.CreateDirectory("launcher");

        string clientRoot = temporary.CreateDirectory("client");

        string outputRoot = Path.Combine(temporary.RootPath, "output");

        string clientExecutable = Path.Combine(clientRoot, "OpenConquer.Client");

        File.WriteAllText(clientExecutable, "client");

        UnixFileMode expectedMode =
            UnixFileMode.UserRead
            | UnixFileMode.UserWrite
            | UnixFileMode.UserExecute
            | UnixFileMode.GroupRead
            | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead
            | UnixFileMode.OtherExecute;

        File.SetUnixFileMode(clientExecutable, expectedMode);

        ManagedProductStager.Stage(new ProductStageOptions(launcherRoot, clientRoot, outputRoot));

        string stagedExecutable = Path.Combine(
            outputRoot,
            ManagedProductDescriptor.ClientRoot,
            "OpenConquer.Client"
        );

        Assert.Equal(expectedMode, File.GetUnixFileMode(stagedExecutable));
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
