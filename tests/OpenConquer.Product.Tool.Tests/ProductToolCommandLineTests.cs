namespace OpenConquer.Product.Tool.Tests;

public sealed class ProductToolCommandLineTests
{
    [Fact]
    public void TryParse_ResolvesWorkingDirectoryRelativePaths()
    {
        string workingDirectory = Path.Combine(
            Path.GetTempPath(),
            "openconquer-product-tool-working"
        );

        bool parsed = ProductToolCommandLine.TryParse(
            [
                "stage-managed-product",
                "--launcher-publish",
                "launcher",
                "--client-publish",
                "client",
                "--release-manifest",
                "release/openconquer.release.json",
                "--release-signature",
                "release/openconquer.release.sig",
                "--output",
                "managed",
            ],
            workingDirectory,
            out ProductToolOptions? options,
            out string? errorMessage
        );

        Assert.True(parsed, errorMessage);

        ProductStageOptions stageOptions = Assert.IsType<ProductStageOptions>(options);

        Assert.Equal(
            Path.GetFullPath(Path.Combine(workingDirectory, "launcher")),
            stageOptions.LauncherPublishPath
        );

        Assert.Equal(
            Path.GetFullPath(Path.Combine(workingDirectory, "client")),
            stageOptions.ClientPublishPath
        );

        Assert.Equal(
            Path.GetFullPath(Path.Combine(workingDirectory, "release/openconquer.release.json")),
            stageOptions.ReleaseManifestPath
        );

        Assert.Equal(
            Path.GetFullPath(Path.Combine(workingDirectory, "release/openconquer.release.sig")),
            stageOptions.ReleaseSignaturePath
        );

        Assert.Equal(
            Path.GetFullPath(Path.Combine(workingDirectory, "managed")),
            stageOptions.OutputRootPath
        );
    }

    [Fact]
    public void TryParse_RejectsDuplicateAndOutOfOrderOptions()
    {
        bool duplicate = ProductToolCommandLine.TryParse(
            [
                "stage-managed-product",
                "--launcher-publish",
                "launcher",
                "--launcher-publish",
                "again",
                "--client-publish",
                "client",
                "--release-manifest",
                "openconquer.release.json",
                "--release-signature",
                "openconquer.release.sig",
                "--output",
                "managed",
            ],
            Path.GetTempPath(),
            out _,
            out _
        );

        bool outOfOrder = ProductToolCommandLine.TryParse(
            [
                "--launcher-publish",
                "launcher",
                "stage-managed-product",
                "--client-publish",
                "client",
                "--release-manifest",
                "openconquer.release.json",
                "--release-signature",
                "openconquer.release.sig",
                "--output",
                "managed",
            ],
            Path.GetTempPath(),
            out _,
            out _
        );

        Assert.False(duplicate);
        Assert.False(outOfOrder);
    }

    [Fact]
    public void TryParseReleaseTrustResolvesRepeatedPublicKeyPaths()
    {
        string workingDirectory = Path.Combine(Path.GetTempPath(), "openconquer-product-tool-trust-working");

        bool parsed = ProductToolCommandLine.TryParse(
            [
                "create-release-trust",
                "--public-key",
                "keys/first.der",
                "--public-key",
                "keys/second.der",
                "--output",
                "release/release-trust.json",
            ],
            workingDirectory,
            out ProductToolOptions? options,
            out string? errorMessage);

        Assert.True(parsed, errorMessage);

        ReleaseTrustOptions trustOptions = Assert.IsType<ReleaseTrustOptions>(options);

        Assert.Equal(
            [
                Path.GetFullPath(Path.Combine(workingDirectory, "keys/first.der")),
                Path.GetFullPath(Path.Combine(workingDirectory, "keys/second.der")),
            ],
            trustOptions.PublicKeyPaths);

        Assert.Equal(
            Path.GetFullPath(Path.Combine(workingDirectory, "release/release-trust.json")),
            trustOptions.OutputPath);
    }

    [Fact]
    public void TryParseReleaseTrustRejectsRepeatedOutput()
    {
        bool parsed = ProductToolCommandLine.TryParse(
            [
                "create-release-trust",
                "--public-key",
                "publisher.der",
                "--output",
                "first.json",
                "--output",
                "second.json",
            ],
            Path.GetTempPath(),
            out ProductToolOptions? options,
            out string? errorMessage);

        Assert.False(parsed);
        Assert.Null(options);
        Assert.Equal("Option '--output' must not be repeated.", errorMessage);
    }

    [Fact]
    public void TryParseReleaseTrustRejectsMissingPublicKey()
    {
        bool parsed = ProductToolCommandLine.TryParse(
            [
                "create-release-trust",
                "--output",
                "release-trust.json",
            ],
            Path.GetTempPath(),
            out ProductToolOptions? options,
            out string? errorMessage);

        Assert.False(parsed);
        Assert.Null(options);
        Assert.Equal("Option '--public-key' is required.", errorMessage);
    }

    [Fact]
    public void TryParseReleaseTrustRejectsMissingOutput()
    {
        bool parsed = ProductToolCommandLine.TryParse(
            [
                "create-release-trust",
                "--public-key",
                "publisher.der",
            ],
            Path.GetTempPath(),
            out ProductToolOptions? options,
            out string? errorMessage);

        Assert.False(parsed);
        Assert.Null(options);
        Assert.Equal("Option '--output' is required.", errorMessage);
    }

    [Fact]
    public void TryParseReleaseTrustRejectsUnknownOption()
    {
        bool parsed = ProductToolCommandLine.TryParse(
            [
                "create-release-trust",
                "--public-key",
                "publisher.der",
                "--trust-store",
                "release-trust.json",
            ],
            Path.GetTempPath(),
            out ProductToolOptions? options,
            out string? errorMessage);

        Assert.False(parsed);
        Assert.Null(options);
        Assert.Equal("Option '--trust-store' is not valid for this command.", errorMessage);
    }

    [Fact]
    public void TryParseReleaseTrustRejectsMoreThanMaximumKeyCount()
    {
        List<string> args = ["create-release-trust"];

        for (int index = 0; index <= ProductReleaseTrust.MaximumKeyCount; index++)
        {
            args.Add("--public-key");
            args.Add($"publisher-{index}.der");
        }

        args.Add("--output");
        args.Add("release-trust.json");

        bool parsed = ProductToolCommandLine.TryParse(args, Path.GetTempPath(), out ProductToolOptions? options, out string? errorMessage);

        Assert.False(parsed);
        Assert.Null(options);
        Assert.Equal($"Release trust supports at most {ProductReleaseTrust.MaximumKeyCount} public keys.", errorMessage);
    }

    [Fact]
    public void TryParseReleaseTrustRejectsMissingOptionValue()
    {
        bool parsed = ProductToolCommandLine.TryParse(
            [
                "create-release-trust",
                "--public-key",
                "--output",
                "release-trust.json",
            ],
            Path.GetTempPath(),
            out ProductToolOptions? options,
            out string? errorMessage);

        Assert.False(parsed);
        Assert.Null(options);
        Assert.Equal("Option '--public-key' requires a value.", errorMessage);
    }
}
