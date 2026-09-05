namespace OpenConquer.Product.Tool.Tests;

public sealed class ProductToolCommandLineTests
{
    [Fact]
    public void TryParse_ResolvesWorkingDirectoryRelativePaths()
    {
        string workingDirectory = Path.Combine(Path.GetTempPath(), "openconquer-product-tool-working");

        bool parsed = ProductToolCommandLine.TryParse(
            [
                "stage-managed-product",
                "--launcher-publish", "launcher",
                "--client-publish", "client",
                "--output", "managed",
            ],
            workingDirectory,
            out ProductStageOptions? options,
            out string? errorMessage);

        Assert.True(parsed, errorMessage);
        Assert.NotNull(options);
        Assert.Equal(Path.GetFullPath(Path.Combine(workingDirectory, "launcher")), options.LauncherPublishPath);
        Assert.Equal(Path.GetFullPath(Path.Combine(workingDirectory, "client")), options.ClientPublishPath);
        Assert.Equal(Path.GetFullPath(Path.Combine(workingDirectory, "managed")), options.OutputRootPath);
    }

    [Fact]
    public void TryParse_RejectsDuplicateAndOutOfOrderOptions()
    {
        bool duplicate = ProductToolCommandLine.TryParse(
            [
                "stage-managed-product",
                "--launcher-publish", "launcher",
                "--launcher-publish", "again",
                "--client-publish", "client",
                "--output", "managed",
            ],
            Path.GetTempPath(),
            out _,
            out _);

        bool outOfOrder = ProductToolCommandLine.TryParse(
            [
                "--launcher-publish", "launcher",
                "stage-managed-product",
                "--client-publish", "client",
                "--output", "managed",
            ],
            Path.GetTempPath(),
            out _,
            out _);

        Assert.False(duplicate);
        Assert.False(outOfOrder);
    }
}
