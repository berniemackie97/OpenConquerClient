namespace OpenConquer.Product.Tool.Tests;

public sealed class LocalProductBuilderTests
{
    [Fact]
    public void BuildComposesAndActivatesLocalProduct()
    {
        using TemporaryDirectory temporary = new();

        TestRepository repository = TestRepository.Create(temporary);
        DevelopmentPublisherIdentityPaths publisherPaths = CreatePublisherPaths(temporary);
        FakeDotNetProcessRunner processRunner = new();
        LocalProductBuilder builder = new(processRunner);

        LocalProductBuildResult result = builder.Build(
            new LocalProductOptions(repository.InvocationPath),
            publisherPaths
        );

        string targetRuntime = RequireCurrentTargetRuntime();
        string expectedProductPath = Path.Combine(
            repository.RootPath,
            "artifacts",
            "local-product",
            "product"
        );

        Assert.Equal(expectedProductPath, result.ProductPath);
        Assert.Equal(1UL, result.ReleaseSequence);
        Assert.Equal("local-1", result.ReleaseVersion);
        Assert.Equal(targetRuntime, result.TargetRuntime);

        ProductReleaseManifest manifest = ProductReleaseManifest.Read(
            Path.Combine(result.ProductPath, ProductReleaseManifest.FileName)
        );

        Assert.Equal(1UL, manifest.ReleaseSequence);
        Assert.Equal("local-1", manifest.ReleaseVersion);
        Assert.Equal(targetRuntime, manifest.TargetRuntime);
        Assert.Equal(1, manifest.MinimumLauncherVersion);

        ProductReleaseManifest.VerifyClient(
            Path.Combine(result.ProductPath, ManagedProductDescriptor.ClientRoot),
            manifest
        );

        ProductReleaseSignature.ValidateEnvelope(
            Path.Combine(result.ProductPath, ProductReleaseSignature.FileName)
        );

        Assert.True(
            File.Exists(Path.Combine(result.ProductPath, ManagedProductDescriptor.FileName))
        );
        Assert.False(
            Directory.Exists(
                Path.Combine(repository.RootPath, "artifacts", "local-product", "product.previous")
            )
        );
        Assert.False(File.Exists(Path.Combine(result.ProductPath, ProductReleaseTrust.FileName)));
        Assert.False(
            File.Exists(Path.Combine(result.ProductPath, LocalProductPaths.PublicKeyFileName))
        );
        Assert.False(
            File.Exists(Path.Combine(result.ProductPath, LocalProductPaths.RawSignatureFileName))
        );
        Assert.False(
            File.Exists(
                Path.Combine(
                    result.ProductPath,
                    DevelopmentPublisherIdentityPaths.PrivateKeyFileName
                )
            )
        );

        Assert.True(File.Exists(publisherPaths.PrivateKeyPath));
        Assert.Equal(
            "1",
            File.ReadAllText(
                Path.Combine(publisherPaths.RootPath, DevelopmentReleaseSequence.FileName)
            )
        );

        AssertWorkspaceIsClean(repository.RootPath);

        Assert.Equal(5, processRunner.Invocations.Count);

        Assert.Equal(
            ["restore", "OpenConquer.Client.slnx", "--locked-mode"],
            processRunner.Invocations[0].Arguments
        );

        Assert.Equal("run", processRunner.Invocations[1].Arguments[0]);
        Assert.Contains("verify-content-set", processRunner.Invocations[1].Arguments);
        Assert.DoesNotContain("--no-build", processRunner.Invocations[1].Arguments);

        Assert.Equal("publish", processRunner.Invocations[2].Arguments[0]);
        Assert.Equal(
            "src/OpenConquer.Client/OpenConquer.Client.csproj",
            processRunner.Invocations[2].Arguments[1]
        );

        Assert.Equal("run", processRunner.Invocations[3].Arguments[0]);
        Assert.Contains("verify-content-set", processRunner.Invocations[3].Arguments);
        Assert.Contains("--no-build", processRunner.Invocations[3].Arguments);

        Assert.Equal("publish", processRunner.Invocations[4].Arguments[0]);
        Assert.Equal(
            "src/OpenConquer.Launcher/OpenConquer.Launcher.csproj",
            processRunner.Invocations[4].Arguments[1]
        );

        Assert.All(
            processRunner.Invocations,
            invocation => Assert.Equal(repository.RootPath, invocation.WorkingDirectory)
        );
    }

    [Fact]
    public void BuildRepeatedlyAdvancesSequenceAndPreservesPreviousProduct()
    {
        using TemporaryDirectory temporary = new();

        TestRepository repository = TestRepository.Create(temporary);
        DevelopmentPublisherIdentityPaths publisherPaths = CreatePublisherPaths(temporary);
        FakeDotNetProcessRunner processRunner = new();
        LocalProductBuilder builder = new(processRunner);

        LocalProductBuildResult first = builder.Build(
            new LocalProductOptions(repository.InvocationPath),
            publisherPaths
        );
        LocalProductBuildResult second = builder.Build(
            new LocalProductOptions(repository.InvocationPath),
            publisherPaths
        );

        string previousProductPath = Path.Combine(
            repository.RootPath,
            "artifacts",
            "local-product",
            "product.previous"
        );

        Assert.Equal(1UL, first.ReleaseSequence);
        Assert.Equal(2UL, second.ReleaseSequence);
        Assert.Equal(2UL, ReadReleaseSequence(second.ProductPath));
        Assert.Equal(1UL, ReadReleaseSequence(previousProductPath));
        Assert.Equal(
            "2",
            File.ReadAllText(
                Path.Combine(publisherPaths.RootPath, DevelopmentReleaseSequence.FileName)
            )
        );

        AssertWorkspaceIsClean(repository.RootPath);
    }

    [Fact]
    public void BuildRecoversSequenceFromActiveProductWhenPersistentCounterIsMissing()
    {
        using TemporaryDirectory temporary = new();

        TestRepository repository = TestRepository.Create(temporary);
        DevelopmentPublisherIdentityPaths publisherPaths = CreatePublisherPaths(temporary);
        FakeDotNetProcessRunner processRunner = new();
        LocalProductBuilder builder = new(processRunner);

        LocalProductBuildResult first = builder.Build(
            new LocalProductOptions(repository.InvocationPath),
            publisherPaths
        );

        File.Delete(Path.Combine(publisherPaths.RootPath, DevelopmentReleaseSequence.FileName));

        LocalProductBuildResult second = builder.Build(
            new LocalProductOptions(repository.InvocationPath),
            publisherPaths
        );

        Assert.Equal(1UL, first.ReleaseSequence);
        Assert.Equal(2UL, second.ReleaseSequence);
        Assert.Equal(2UL, ReadReleaseSequence(second.ProductPath));
        Assert.Equal(
            "2",
            File.ReadAllText(
                Path.Combine(publisherPaths.RootPath, DevelopmentReleaseSequence.FileName)
            )
        );

        AssertWorkspaceIsClean(repository.RootPath);
    }

    [Fact]
    public void BuildRejectsServerDatBeforeReservingAnotherSequence()
    {
        using TemporaryDirectory temporary = new();

        TestRepository repository = TestRepository.Create(temporary);
        DevelopmentPublisherIdentityPaths publisherPaths = CreatePublisherPaths(temporary);
        FakeDotNetProcessRunner processRunner = new();
        LocalProductBuilder builder = new(processRunner);

        LocalProductBuildResult first = builder.Build(
            new LocalProductOptions(repository.InvocationPath),
            publisherPaths
        );

        processRunner.IncludeServerDat = true;

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            builder.Build(new LocalProductOptions(repository.InvocationPath), publisherPaths)
        );

        Assert.Contains("Server.dat", exception.Message, StringComparison.Ordinal);
        Assert.Equal(1UL, ReadReleaseSequence(first.ProductPath));
        Assert.Equal(
            "1",
            File.ReadAllText(
                Path.Combine(publisherPaths.RootPath, DevelopmentReleaseSequence.FileName)
            )
        );
        Assert.False(
            Directory.Exists(
                Path.Combine(repository.RootPath, "artifacts", "local-product", "product.previous")
            )
        );

        AssertWorkspaceIsClean(repository.RootPath);
    }

    [Fact]
    public void BuildPreservesActiveProductWhenLauncherPublishIsInvalid()
    {
        using TemporaryDirectory temporary = new();

        TestRepository repository = TestRepository.Create(temporary);
        DevelopmentPublisherIdentityPaths publisherPaths = CreatePublisherPaths(temporary);
        FakeDotNetProcessRunner processRunner = new();
        LocalProductBuilder builder = new(processRunner);

        LocalProductBuildResult first = builder.Build(
            new LocalProductOptions(repository.InvocationPath),
            publisherPaths
        );

        processRunner.IncludeLooseLauncherTrust = true;

        Assert.Throws<InvalidDataException>(() =>
            builder.Build(new LocalProductOptions(repository.InvocationPath), publisherPaths)
        );

        Assert.Equal(1UL, ReadReleaseSequence(first.ProductPath));
        Assert.False(
            Directory.Exists(
                Path.Combine(repository.RootPath, "artifacts", "local-product", "product.previous")
            )
        );

        // Sequence 2 was already durably reserved before launcher publication failed.
        Assert.Equal(
            "2",
            File.ReadAllText(
                Path.Combine(publisherPaths.RootPath, DevelopmentReleaseSequence.FileName)
            )
        );

        AssertWorkspaceIsClean(repository.RootPath);
    }

    [Fact]
    public void BuildPreservesActiveProductWhenDotNetCommandFails()
    {
        using TemporaryDirectory temporary = new();

        TestRepository repository = TestRepository.Create(temporary);
        DevelopmentPublisherIdentityPaths publisherPaths = CreatePublisherPaths(temporary);
        FakeDotNetProcessRunner processRunner = new();
        LocalProductBuilder builder = new(processRunner);

        LocalProductBuildResult first = builder.Build(
            new LocalProductOptions(repository.InvocationPath),
            publisherPaths
        );

        processRunner.FailInvocationNumber = processRunner.Invocations.Count + 3;

        Assert.Throws<InvalidOperationException>(() =>
            builder.Build(new LocalProductOptions(repository.InvocationPath), publisherPaths)
        );

        Assert.Equal(1UL, ReadReleaseSequence(first.ProductPath));
        Assert.Equal(
            "1",
            File.ReadAllText(
                Path.Combine(publisherPaths.RootPath, DevelopmentReleaseSequence.FileName)
            )
        );
        Assert.False(
            Directory.Exists(
                Path.Combine(repository.RootPath, "artifacts", "local-product", "product.previous")
            )
        );

        AssertWorkspaceIsClean(repository.RootPath);
    }

    [Fact]
    public void BuildRejectsLinkedActiveProductBeforeReservingSequence()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using TemporaryDirectory temporary = new();

        TestRepository repository = TestRepository.Create(temporary);
        DevelopmentPublisherIdentityPaths publisherPaths = CreatePublisherPaths(temporary);
        FakeDotNetProcessRunner processRunner = new();
        LocalProductBuilder builder = new(processRunner);

        string productRoot = Path.Combine(repository.RootPath, "artifacts", "local-product");
        string linkedTarget = Path.Combine(repository.RootPath, "linked-active-target");

        Directory.CreateDirectory(productRoot);
        Directory.CreateDirectory(linkedTarget);
        Directory.CreateSymbolicLink(Path.Combine(productRoot, "product"), linkedTarget);

        Assert.Throws<InvalidDataException>(() =>
            builder.Build(new LocalProductOptions(repository.InvocationPath), publisherPaths)
        );

        Assert.False(
            File.Exists(Path.Combine(publisherPaths.RootPath, DevelopmentReleaseSequence.FileName))
        );
        AssertWorkspaceIsClean(repository.RootPath);
    }

    private static DevelopmentPublisherIdentityPaths CreatePublisherPaths(
        TemporaryDirectory temporary
    )
    {
        string rootPath = Path.Combine(temporary.RootPath, "development-state");

        return new DevelopmentPublisherIdentityPaths(
            rootPath,
            Path.Combine(rootPath, DevelopmentPublisherIdentityPaths.PrivateKeyFileName)
        );
    }

    private static ulong ReadReleaseSequence(string productPath)
    {
        return ProductReleaseManifest
            .Read(Path.Combine(productPath, ProductReleaseManifest.FileName))
            .ReleaseSequence;
    }

    private static void AssertWorkspaceIsClean(string repositoryRoot)
    {
        string workRoot = Path.Combine(repositoryRoot, "artifacts", "local-product", "work");

        Assert.True(Directory.Exists(workRoot));
        Assert.Empty(Directory.EnumerateDirectories(workRoot));
    }

    private static string RequireCurrentTargetRuntime()
    {
        return ProductTargetRuntime.Current
            ?? throw new InvalidOperationException(
                "The test host is not a supported OpenConquer product runtime."
            );
    }

    private sealed record DotNetInvocation(string WorkingDirectory, string[] Arguments);

    private sealed class FakeDotNetProcessRunner : IDotNetProcessRunner
    {
        private const string TrustPropertyPrefix = "-p:OpenConquerReleaseTrustPath=";

        public List<DotNetInvocation> Invocations { get; } = [];

        public bool IncludeServerDat
        {
            get; set;
        }

        public bool IncludeLooseLauncherTrust
        {
            get; set;
        }

        public int? FailInvocationNumber
        {
            get; set;
        }

        public void Run(string workingDirectory, IReadOnlyList<string> arguments)
        {
            Invocations.Add(new DotNetInvocation(workingDirectory, [.. arguments]));

            if (FailInvocationNumber == Invocations.Count)
            {
                throw new InvalidOperationException("Synthetic .NET SDK command failure.");
            }

            if (
                arguments.Count < 2
                || !string.Equals(arguments[0], "publish", StringComparison.Ordinal)
            )
            {
                return;
            }

            string outputPath = RequiredOption(arguments, "--output");

            switch (arguments[1])
            {
                case "src/OpenConquer.Client/OpenConquer.Client.csproj":
                    CreateClientPublish(outputPath);
                    break;

                case "src/OpenConquer.Launcher/OpenConquer.Launcher.csproj":
                    ValidateLauncherTrustProperty(arguments);
                    CreateLauncherPublish(outputPath);
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unexpected test publish project '{arguments[1]}'."
                    );
            }
        }

        private void CreateClientPublish(string outputPath)
        {
            string targetRuntime = RequireCurrentTargetRuntime();

            Directory.CreateDirectory(outputPath);

            File.WriteAllBytes(
                Path.Combine(outputPath, ProductTargetRuntime.ClientExecutable(targetRuntime)),
                [0x01, 0x02, 0x03, 0x04]
            );

            string contentRoot = Path.Combine(outputPath, "content", "retail-5517");
            string payloadRoot = Path.Combine(contentRoot, "payload");

            Directory.CreateDirectory(payloadRoot);
            File.WriteAllText(
                Path.Combine(contentRoot, "manifest.json"),
                "local-product-builder-test"
            );
            File.WriteAllBytes(Path.Combine(payloadRoot, "asset.bin"), [0x10, 0x20, 0x30]);

            if (IncludeServerDat)
            {
                string serverDatRoot = Path.Combine(outputPath, "legacy", "config");

                Directory.CreateDirectory(serverDatRoot);
                File.WriteAllText(Path.Combine(serverDatRoot, "Server.dat"), "legacy");
            }
        }

        private void CreateLauncherPublish(string outputPath)
        {
            string targetRuntime = RequireCurrentTargetRuntime();

            Directory.CreateDirectory(outputPath);

            string executable = targetRuntime.StartsWith("win-", StringComparison.Ordinal)
                ? "OpenConquer.Launcher.exe"
                : "OpenConquer.Launcher";

            File.WriteAllBytes(Path.Combine(outputPath, executable), [0x05, 0x06, 0x07, 0x08]);

            if (IncludeLooseLauncherTrust)
            {
                File.WriteAllText(Path.Combine(outputPath, ProductReleaseTrust.FileName), "{}");
            }
        }

        private static void ValidateLauncherTrustProperty(IReadOnlyList<string> arguments)
        {
            string? trustProperty = arguments.FirstOrDefault(argument =>
                argument.StartsWith(TrustPropertyPrefix, StringComparison.Ordinal)
            );

            if (trustProperty is null)
            {
                throw new InvalidOperationException(
                    "The launcher publish did not receive the release-trust MSBuild property."
                );
            }

            string trustPath = trustProperty[TrustPropertyPrefix.Length..];

            if (!File.Exists(trustPath))
            {
                throw new InvalidOperationException(
                    "The launcher publish received a missing release-trust document."
                );
            }
        }

        private static string RequiredOption(IReadOnlyList<string> arguments, string option)
        {
            for (int index = 0; index < arguments.Count - 1; index++)
            {
                if (string.Equals(arguments[index], option, StringComparison.Ordinal))
                {
                    return arguments[index + 1];
                }
            }

            throw new InvalidOperationException(
                $"Required test option '{option}' was not supplied."
            );
        }
    }

    private sealed class TestRepository
    {
        private TestRepository(string rootPath, string invocationPath)
        {
            RootPath = rootPath;
            InvocationPath = invocationPath;
        }

        public string RootPath
        {
            get;
        }

        public string InvocationPath
        {
            get;
        }

        public static TestRepository Create(TemporaryDirectory temporary)
        {
            string rootPath = temporary.CreateDirectory("repository");

            CreateFile(Path.Combine(rootPath, "OpenConquer.Client.slnx"));
            CreateFile(Path.Combine(rootPath, "global.json"));
            CreateFile(
                Path.Combine(rootPath, "src", "OpenConquer.Client", "OpenConquer.Client.csproj")
            );
            CreateFile(
                Path.Combine(rootPath, "src", "OpenConquer.Launcher", "OpenConquer.Launcher.csproj")
            );
            CreateFile(
                Path.Combine(
                    rootPath,
                    "tools",
                    "OpenConquer.Content.Tool",
                    "OpenConquer.Content.Tool.csproj"
                )
            );
            CreateFile(
                Path.Combine(
                    rootPath,
                    "tools",
                    "OpenConquer.Product.Tool",
                    "OpenConquer.Product.Tool.csproj"
                )
            );

            string sourceContentRoot = Path.Combine(rootPath, "content", "retail-5517");

            Directory.CreateDirectory(sourceContentRoot);
            File.WriteAllText(
                Path.Combine(sourceContentRoot, "manifest.json"),
                "local-product-builder-source-test"
            );

            return new TestRepository(
                rootPath,
                Path.Combine(rootPath, "src", "OpenConquer.Client")
            );
        }

        private static void CreateFile(string path)
        {
            string? parentPath = Path.GetDirectoryName(path);

            if (string.IsNullOrWhiteSpace(parentPath))
            {
                throw new InvalidOperationException(
                    "A test repository file must have a parent directory."
                );
            }

            Directory.CreateDirectory(parentPath);
            File.WriteAllText(path, string.Empty);
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private readonly string _path = Path.Combine(
            Path.GetTempPath(),
            $"openconquer-local-product-builder-{Guid.NewGuid():N}"
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
