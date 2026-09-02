using OpenConquer.Rendering;

namespace OpenConquer.Client.Tests;

public sealed class ClientStartupOptionsTests
{
    [Fact]
    public void TryParse_UsesDefaultContentRootWhenNoArgumentsAreSupplied()
    {
        string defaultContentRootPath = CreateAbsolutePath("default-content");
        string workingDirectoryPath = CreateAbsolutePath("working-directory");

        bool parsed = ClientStartupOptions.TryParse(
            [],
            defaultContentRootPath,
            workingDirectoryPath,
            out ClientStartupOptions? options,
            out string? errorMessage
        );

        Assert.True(parsed);
        Assert.NotNull(options);
        Assert.Null(errorMessage);
        Assert.Equal(
            Path.TrimEndingDirectorySeparator(defaultContentRootPath),
            options.ContentRootPath
        );
    }

    [Fact]
    public void TryParse_UsesConfiguredPathWhenContentRootIsAbsolute()
    {
        string defaultContentRootPath = CreateAbsolutePath("default-content");
        string workingDirectoryPath = CreateAbsolutePath("working-directory");
        string configuredContentRootPath = CreateAbsolutePath("configured-content");

        bool parsed = ClientStartupOptions.TryParse(
            ["--content-root", configuredContentRootPath],
            defaultContentRootPath,
            workingDirectoryPath,
            out ClientStartupOptions? options,
            out string? errorMessage
        );

        Assert.True(parsed);
        Assert.NotNull(options);
        Assert.Null(errorMessage);
        Assert.Equal(
            Path.TrimEndingDirectorySeparator(configuredContentRootPath),
            options.ContentRootPath
        );
    }

    [Fact]
    public void TryParse_ResolvesRelativeContentRootAgainstWorkingDirectory()
    {
        string defaultContentRootPath = CreateAbsolutePath("default-content");
        string workingDirectoryPath = CreateAbsolutePath("working-directory");

        bool parsed = ClientStartupOptions.TryParse(
            ["--content-root", "legacy-client"],
            defaultContentRootPath,
            workingDirectoryPath,
            out ClientStartupOptions? options,
            out string? errorMessage
        );

        Assert.True(parsed);
        Assert.NotNull(options);
        Assert.Null(errorMessage);
        Assert.Equal(Path.Combine(workingDirectoryPath, "legacy-client"), options.ContentRootPath);
    }

    [Fact]
    public void TryParse_ReturnsFailureWhenContentRootIsDuplicated()
    {
        string defaultContentRootPath = CreateAbsolutePath("default-content");
        string workingDirectoryPath = CreateAbsolutePath("working-directory");

        bool parsed = ClientStartupOptions.TryParse(
            ["--content-root", "first", "--content-root", "second"],
            defaultContentRootPath,
            workingDirectoryPath,
            out ClientStartupOptions? options,
            out string? errorMessage
        );

        Assert.False(parsed);
        Assert.Null(options);
        Assert.Equal("Startup option '--content-root' may only be specified once.", errorMessage);
    }

    [Fact]
    public void TryParse_ReturnsFailureWhenContentRootValueIsMissing()
    {
        string defaultContentRootPath = CreateAbsolutePath("default-content");
        string workingDirectoryPath = CreateAbsolutePath("working-directory");

        bool parsed = ClientStartupOptions.TryParse(
            ["--content-root"],
            defaultContentRootPath,
            workingDirectoryPath,
            out ClientStartupOptions? options,
            out string? errorMessage
        );

        Assert.False(parsed);
        Assert.Null(options);
        Assert.Equal("Startup option '--content-root' requires a path value.", errorMessage);
    }

    [Fact]
    public void TryParse_ReturnsFailureWhenAnOptionReplacesTheContentRootValue()
    {
        string defaultContentRootPath = CreateAbsolutePath("default-content");
        string workingDirectoryPath = CreateAbsolutePath("working-directory");

        bool parsed = ClientStartupOptions.TryParse(
            ["--content-root", "--unknown"],
            defaultContentRootPath,
            workingDirectoryPath,
            out ClientStartupOptions? options,
            out string? errorMessage
        );

        Assert.False(parsed);
        Assert.Null(options);
        Assert.Equal("Startup option '--content-root' requires a path value.", errorMessage);
    }

    [Fact]
    public void TryParse_ReturnsFailureWhenContentRootValueIsWhitespace()
    {
        string defaultContentRootPath = CreateAbsolutePath("default-content");
        string workingDirectoryPath = CreateAbsolutePath("working-directory");

        bool parsed = ClientStartupOptions.TryParse(
            ["--content-root", "   "],
            defaultContentRootPath,
            workingDirectoryPath,
            out ClientStartupOptions? options,
            out string? errorMessage
        );

        Assert.False(parsed);
        Assert.Null(options);
        Assert.Equal("Startup option '--content-root' requires a path value.", errorMessage);
    }

    [Fact]
    public void TryParse_ReturnsFailureForUnknownOption()
    {
        string defaultContentRootPath = CreateAbsolutePath("default-content");
        string workingDirectoryPath = CreateAbsolutePath("working-directory");

        bool parsed = ClientStartupOptions.TryParse(
            ["--unknown"],
            defaultContentRootPath,
            workingDirectoryPath,
            out ClientStartupOptions? options,
            out string? errorMessage
        );

        Assert.False(parsed);
        Assert.Null(options);
        Assert.Equal("Unknown startup argument '--unknown'.", errorMessage);
    }

    [Fact]
    public void TryParse_ReturnsFailureForPositionalArgument()
    {
        string defaultContentRootPath = CreateAbsolutePath("default-content");
        string workingDirectoryPath = CreateAbsolutePath("working-directory");

        bool parsed = ClientStartupOptions.TryParse(
            ["legacy-client"],
            defaultContentRootPath,
            workingDirectoryPath,
            out ClientStartupOptions? options,
            out string? errorMessage
        );

        Assert.False(parsed);
        Assert.Null(options);
        Assert.Equal("Unknown startup argument 'legacy-client'.", errorMessage);
    }

    [Fact]
    public void TryParse_ThrowsArgumentExceptionWhenDefaultContentRootIsRelative()
    {
        string workingDirectoryPath = CreateAbsolutePath("working-directory");

        Assert.Throws<ArgumentException>(() =>
            ClientStartupOptions.TryParse(
                [],
                "relative-content",
                workingDirectoryPath,
                out _,
                out _
            )
        );
    }

    [Fact]
    public void TryParse_ThrowsArgumentExceptionWhenWorkingDirectoryIsRelative()
    {
        string defaultContentRootPath = CreateAbsolutePath("default-content");

        Assert.Throws<ArgumentException>(() =>
            ClientStartupOptions.TryParse(
                [],
                defaultContentRootPath,
                "relative-working-directory",
                out _,
                out _
            )
        );
    }

    [Fact]
    public void TryParse_DefaultsPresentationToFitSoTheFrameIsNeverDistorted()
    {
        bool parsed = ClientStartupOptions.TryParse(
            [],
            CreateAbsolutePath("default-content"),
            CreateAbsolutePath("working-directory"),
            out ClientStartupOptions? options,
            out string? errorMessage
        );

        Assert.True(parsed);
        Assert.NotNull(options);
        Assert.Null(errorMessage);
        Assert.Equal(PresentationPolicy.Fit, options.PresentationPolicy);
    }

    [Theory]
    [InlineData("fit", PresentationPolicy.Fit)]
    [InlineData("integer", PresentationPolicy.IntegerScale)]
    [InlineData("stretch", PresentationPolicy.Stretch)]
    [InlineData("Integer", PresentationPolicy.IntegerScale)]
    [InlineData("STRETCH", PresentationPolicy.Stretch)]
    public void TryParse_AcceptsEveryPresentationPolicyNameCaseInsensitively(string value, PresentationPolicy expected)
    {
        bool parsed = ClientStartupOptions.TryParse(
            ["--presentation", value],
            CreateAbsolutePath("default-content"),
            CreateAbsolutePath("working-directory"),
            out ClientStartupOptions? options,
            out string? errorMessage
        );

        Assert.True(parsed);
        Assert.NotNull(options);
        Assert.Null(errorMessage);
        Assert.Equal(expected, options.PresentationPolicy);
    }

    [Fact]
    public void TryParse_ReturnsFailureForUnknownPresentationValue()
    {
        bool parsed = ClientStartupOptions.TryParse(
            ["--presentation", "letterbox"],
            CreateAbsolutePath("default-content"),
            CreateAbsolutePath("working-directory"),
            out ClientStartupOptions? options,
            out string? errorMessage
        );

        Assert.False(parsed);
        Assert.Null(options);
        Assert.NotNull(errorMessage);

        // The message must name the accepted values, or the only way to discover them is the source.
        Assert.Contains("fit", errorMessage, StringComparison.Ordinal);
        Assert.Contains("integer", errorMessage, StringComparison.Ordinal);
        Assert.Contains("stretch", errorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void TryParse_ReturnsFailureWhenPresentationValueIsMissing()
    {
        bool parsed = ClientStartupOptions.TryParse(
            ["--presentation"],
            CreateAbsolutePath("default-content"),
            CreateAbsolutePath("working-directory"),
            out ClientStartupOptions? options,
            out string? errorMessage
        );

        Assert.False(parsed);
        Assert.Null(options);
        Assert.NotNull(errorMessage);
    }

    [Fact]
    public void TryParse_ReturnsFailureWhenAnOptionReplacesThePresentationValue()
    {
        // A forgotten value must not swallow the next option and leave it silently unapplied.
        bool parsed = ClientStartupOptions.TryParse(
            ["--presentation", "--content-root", CreateAbsolutePath("content")],
            CreateAbsolutePath("default-content"),
            CreateAbsolutePath("working-directory"),
            out ClientStartupOptions? options,
            out string? errorMessage
        );

        Assert.False(parsed);
        Assert.Null(options);
        Assert.NotNull(errorMessage);
    }

    [Fact]
    public void TryParse_ReturnsFailureWhenPresentationIsDuplicated()
    {
        bool parsed = ClientStartupOptions.TryParse(
            ["--presentation", "fit", "--presentation", "integer"],
            CreateAbsolutePath("default-content"),
            CreateAbsolutePath("working-directory"),
            out ClientStartupOptions? options,
            out string? errorMessage
        );

        Assert.False(parsed);
        Assert.Null(options);
        Assert.NotNull(errorMessage);
    }

    [Fact]
    public void TryParse_AcceptsContentRootAndPresentationTogetherInEitherOrder()
    {
        string contentRoot = CreateAbsolutePath("content");

        foreach (string[] args in new[]
        {
            new[] { "--content-root", contentRoot, "--presentation", "integer" },
            new[] { "--presentation", "integer", "--content-root", contentRoot },
        })
        {
            bool parsed = ClientStartupOptions.TryParse(
                args,
                CreateAbsolutePath("default-content"),
                CreateAbsolutePath("working-directory"),
                out ClientStartupOptions? options,
                out string? errorMessage
            );

            Assert.True(parsed);
            Assert.NotNull(options);
            Assert.Null(errorMessage);
            Assert.Equal(Path.TrimEndingDirectorySeparator(contentRoot), options.ContentRootPath);
            Assert.Equal(PresentationPolicy.IntegerScale, options.PresentationPolicy);
        }
    }

    [Fact]
    public void PresentationPolicyNames_ListsEveryAcceptedValue()
    {
        Assert.Equal("fit|integer|stretch", ClientStartupOptions.PresentationPolicyNames);
    }

    private static string CreateAbsolutePath(string leafName)
    {
        return Path.Combine(
            Path.GetTempPath(),
            "OpenConquer.Client.Tests",
            Guid.NewGuid().ToString("N"),
            leafName
        );
    }
}
