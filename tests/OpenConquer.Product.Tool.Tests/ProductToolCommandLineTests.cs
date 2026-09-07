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
}
