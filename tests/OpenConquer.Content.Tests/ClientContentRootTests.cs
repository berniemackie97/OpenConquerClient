namespace OpenConquer.Content.Tests;

public sealed class ClientContentRootTests
{
    [Fact]
    public void Constructor_ThrowsDirectoryNotFoundExceptionWhenRootDoesNotExist()
    {
        string missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        Assert.Throws<DirectoryNotFoundException>(() => new ClientContentRoot(missingPath));
    }

    [Fact]
    public void Constructor_ThrowsIOExceptionWhenRootIsAFile()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        string filePath = temporaryDirectory.WriteFile("not-a-directory", string.Empty);

        Assert.Throws<IOException>(() => new ClientContentRoot(filePath));
    }

    [Fact]
    public void Constructor_ThrowsIOExceptionWhenRootIsASymbolicLink()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        string targetPath = Path.Combine(temporaryDirectory.RootPath, "actual-root");

        Directory.CreateDirectory(targetPath);

        string linkPath = Path.Combine(temporaryDirectory.RootPath, "linked-root");

        _ = Directory.CreateSymbolicLink(linkPath, targetPath);

        Assert.Throws<IOException>(() => new ClientContentRoot(linkPath));
    }

    [Fact]
    public void TryResolveFile_UsesWindowsStyleCaseInsensitivePathResolution()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        string expectedPath = temporaryDirectory.WriteFile(
            "INI/GameSetup.InI",
            "[ScreenModeRecord]\nScreenMode=2\n"
        );

        ClientContentRoot contentRoot = new(temporaryDirectory.RootPath);

        bool resolved = contentRoot.TryResolveFile(@"ini\gamesetup.ini", out string? actualPath);

        Assert.True(resolved);
        Assert.Equal(expectedPath, actualPath);
    }

    [Fact]
    public void TryResolveFile_ReturnsFalseWhenFileDoesNotExist()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        ClientContentRoot contentRoot = new(temporaryDirectory.RootPath);

        bool resolved = contentRoot.TryResolveFile("ini/GameSetup.Ini", out string? actualPath);

        Assert.False(resolved);
        Assert.Null(actualPath);
    }

    [Fact]
    public void TryResolveFile_ThrowsIOExceptionWhenIntermediateDirectoryIsASymbolicLink()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        string contentRootPath = Path.Combine(temporaryDirectory.RootPath, "content");

        string outsideDirectoryPath = Path.Combine(temporaryDirectory.RootPath, "outside");

        Directory.CreateDirectory(contentRootPath);
        Directory.CreateDirectory(outsideDirectoryPath);

        temporaryDirectory.WriteFile("outside/GameSetup.Ini", "[ScreenModeRecord]\nScreenMode=2\n");

        string linkedDirectoryPath = Path.Combine(contentRootPath, "ini");

        _ = Directory.CreateSymbolicLink(linkedDirectoryPath, outsideDirectoryPath);

        ClientContentRoot contentRoot = new(contentRootPath);

        Assert.Throws<IOException>(() => contentRoot.TryResolveFile("ini/GameSetup.Ini", out _));
    }

    [Fact]
    public void TryResolveFile_ThrowsIOExceptionWhenTargetFileIsASymbolicLink()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        string contentRootPath = Path.Combine(temporaryDirectory.RootPath, "content");

        string contentIniPath = Path.Combine(contentRootPath, "ini");

        Directory.CreateDirectory(contentIniPath);

        string outsideFilePath = temporaryDirectory.WriteFile(
            "outside/GameSetup.Ini",
            "[ScreenModeRecord]\nScreenMode=2\n"
        );

        string linkedFilePath = Path.Combine(contentIniPath, "GameSetup.Ini");

        _ = File.CreateSymbolicLink(linkedFilePath, outsideFilePath);

        ClientContentRoot contentRoot = new(contentRootPath);

        Assert.Throws<IOException>(() => contentRoot.TryResolveFile("ini/GameSetup.Ini", out _));
    }

    [Theory]
    [InlineData("../GameSetup.Ini")]
    [InlineData("ini/../GameSetup.Ini")]
    [InlineData("/ini/GameSetup.Ini")]
    [InlineData(@"\ini\GameSetup.Ini")]
    [InlineData(@"C:\ini\GameSetup.Ini")]
    public void TryResolveFile_ThrowsArgumentExceptionWhenPathEscapesTheContentRoot(
        string relativePath
    )
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        ClientContentRoot contentRoot = new(temporaryDirectory.RootPath);

        Assert.Throws<ArgumentException>(() => contentRoot.TryResolveFile(relativePath, out _));
    }
}
