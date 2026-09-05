using OpenConquer.Platform;
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
        Assert.Equal(DesktopWindowMode.Resizable, options.WindowMode);
        Assert.Equal(1280, options.WindowSize.Width);
        Assert.Equal(720, options.WindowSize.Height);
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
            ["--content-root", "custom-content"],
            defaultContentRootPath,
            workingDirectoryPath,
            out ClientStartupOptions? options,
            out string? errorMessage
        );

        Assert.True(parsed);
        Assert.NotNull(options);
        Assert.Null(errorMessage);
        Assert.Equal(Path.Combine(workingDirectoryPath, "custom-content"), options.ContentRootPath);
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
            ["custom-content"],
            defaultContentRootPath,
            workingDirectoryPath,
            out ClientStartupOptions? options,
            out string? errorMessage
        );

        Assert.False(parsed);
        Assert.Null(options);
        Assert.Equal("Unknown startup argument 'custom-content'.", errorMessage);
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
    [InlineData("resizable", DesktopWindowMode.Resizable)]
    [InlineData("fixed", DesktopWindowMode.Fixed)]
    [InlineData("fullscreen", DesktopWindowMode.Fullscreen)]
    [InlineData("Fixed", DesktopWindowMode.Fixed)]
    [InlineData("FULLSCREEN", DesktopWindowMode.Fullscreen)]
    public void TryParse_AcceptsEveryWindowModeNameCaseInsensitively(string value, DesktopWindowMode expected)
    {
        bool parsed = ClientStartupOptions.TryParse(
            ["--window-mode", value],
            CreateAbsolutePath("default-content"),
            CreateAbsolutePath("working-directory"),
            out ClientStartupOptions? options,
            out string? errorMessage
        );

        Assert.True(parsed);
        Assert.NotNull(options);
        Assert.Null(errorMessage);
        Assert.Equal(expected, options.WindowMode);
    }

    [Fact]
    public void TryParse_ReturnsFailureForUnknownWindowModeValue()
    {
        bool parsed = ClientStartupOptions.TryParse(
            ["--window-mode", "borderless"],
            CreateAbsolutePath("default-content"),
            CreateAbsolutePath("working-directory"),
            out ClientStartupOptions? options,
            out string? errorMessage
        );

        Assert.False(parsed);
        Assert.Null(options);
        Assert.NotNull(errorMessage);
        Assert.Contains("resizable", errorMessage, StringComparison.Ordinal);
        Assert.Contains("fixed", errorMessage, StringComparison.Ordinal);
        Assert.Contains("fullscreen", errorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void TryParse_ReturnsFailureWhenWindowModeValueIsMissing()
    {
        bool parsed = ClientStartupOptions.TryParse(
            ["--window-mode"],
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
    public void TryParse_ReturnsFailureWhenAnOptionReplacesTheWindowModeValue()
    {
        bool parsed = ClientStartupOptions.TryParse(
            ["--window-mode", "--presentation", "fit"],
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
    public void TryParse_ReturnsFailureWhenWindowModeIsDuplicated()
    {
        bool parsed = ClientStartupOptions.TryParse(
            ["--window-mode", "fixed", "--window-mode", "fullscreen"],
            CreateAbsolutePath("default-content"),
            CreateAbsolutePath("working-directory"),
            out ClientStartupOptions? options,
            out string? errorMessage
        );

        Assert.False(parsed);
        Assert.Null(options);
        Assert.NotNull(errorMessage);
    }

    [Theory]
    [InlineData("1280x720", 1280, 720)]
    [InlineData("1024X768", 1024, 768)]
    public void TryParse_AcceptsSupportedWindowSize(string value, int expectedWidth, int expectedHeight)
    {
        bool parsed = ClientStartupOptions.TryParse(
            ["--window-size", value],
            CreateAbsolutePath("default-content"),
            CreateAbsolutePath("working-directory"),
            out ClientStartupOptions? options,
            out string? errorMessage
        );

        Assert.True(parsed);
        Assert.NotNull(options);
        Assert.Null(errorMessage);
        Assert.Equal(expectedWidth, options.WindowSize.Width);
        Assert.Equal(expectedHeight, options.WindowSize.Height);
    }

    [Theory]
    [InlineData("1280")]
    [InlineData("1280x")]
    [InlineData("x720")]
    [InlineData("0x720")]
    [InlineData("1280x0")]
    [InlineData("16385x720")]
    [InlineData("7680x4321")]
    [InlineData("1280x720x60")]
    public void TryParse_ReturnsFailureForUnsupportedWindowSize(string value)
    {
        bool parsed = ClientStartupOptions.TryParse(
            ["--window-size", value],
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
    public void TryParse_ReturnsFailureWhenWindowSizeIsMissing()
    {
        bool parsed = ClientStartupOptions.TryParse(
            ["--window-size"],
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
    public void TryParse_ReturnsFailureWhenWindowSizeIsDuplicated()
    {
        bool parsed = ClientStartupOptions.TryParse(
            ["--window-size", "1280x720", "--window-size", "1024x768"],
            CreateAbsolutePath("default-content"),
            CreateAbsolutePath("working-directory"),
            out ClientStartupOptions? options,
            out string? errorMessage
        );

        Assert.False(parsed);
        Assert.Null(options);
        Assert.NotNull(errorMessage);
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
    public void TryParse_AcceptsContentRootPresentationAndWindowModeTogetherInEitherOrder()
    {
        string contentRoot = CreateAbsolutePath("content");

        foreach (string[] args in new[]
        {
            new[] { "--content-root", contentRoot, "--window-size", "1280x720", "--presentation", "integer", "--window-mode", "fixed" },
            new[] { "--window-mode", "fixed", "--window-size", "1280x720", "--presentation", "integer", "--content-root", contentRoot },
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
            Assert.Equal(DesktopWindowMode.Fixed, options.WindowMode);
            Assert.Equal(new PixelSize(1280, 720), options.WindowSize);
        }
    }

    [Fact]
    public void PresentationPolicyNames_ListsEveryAcceptedValue()
    {
        Assert.Equal("fit|integer|stretch", ClientStartupOptions.PresentationPolicyNames);
    }

    [Fact]
    public void WindowModeNames_ListsEveryAcceptedValue()
    {
        Assert.Equal("resizable|fixed|fullscreen", ClientStartupOptions.WindowModeNames);
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
