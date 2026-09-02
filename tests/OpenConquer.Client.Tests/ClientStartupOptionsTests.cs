namespace OpenConquer.Client.Tests;

public sealed class ClientStartupOptionsTests
{
    [Fact]
    public void TryParseWithNoArgumentsUsesDefaultContentRoot()
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
    public void TryParseWithAbsoluteContentRootUsesConfiguredPath()
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
    public void TryParseWithRelativeContentRootResolvesAgainstWorkingDirectory()
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
    public void TryParseWithDuplicateContentRootReturnsFailure()
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
    public void TryParseWithMissingContentRootValueReturnsFailure()
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
    public void TryParseWithOptionInPlaceOfContentRootValueReturnsFailure()
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
    public void TryParseWithWhitespaceContentRootValueReturnsFailure()
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
    public void TryParseWithUnknownOptionReturnsFailure()
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
    public void TryParseWithPositionalArgumentReturnsFailure()
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
    public void TryParseWithRelativeDefaultContentRootThrowsArgumentException()
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
    public void TryParseWithRelativeWorkingDirectoryThrowsArgumentException()
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
