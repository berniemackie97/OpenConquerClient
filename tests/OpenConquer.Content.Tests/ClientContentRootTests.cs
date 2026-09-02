namespace OpenConquer.Content.Tests;

public sealed class ClientContentRootTests
{
    [Fact]
    public void ConstructorWhenRootDoesNotExistThrowsDirectoryNotFoundException()
    {
        string missingPath = Path.Combine(
            Path.GetTempPath(),
            Guid.NewGuid().ToString("N"));

        Assert.Throws<DirectoryNotFoundException>(
            () => new ClientContentRoot(missingPath));
    }

    [Fact]
    public void TryResolveFileUsesWindowsStyleCaseInsensitivePathResolution()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        string expectedPath = temporaryDirectory.WriteFile(
            "INI/GameSetup.InI",
            "[ScreenModeRecord]\nScreenMode=2\n");

        ClientContentRoot contentRoot = new(temporaryDirectory.RootPath);

        bool resolved = contentRoot.TryResolveFile(
            @"ini\gamesetup.ini",
            out string? actualPath);

        Assert.True(resolved);
        Assert.Equal(expectedPath, actualPath);
    }

    [Fact]
    public void TryResolveFileWhenFileDoesNotExistReturnsFalse()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        ClientContentRoot contentRoot = new(temporaryDirectory.RootPath);

        bool resolved = contentRoot.TryResolveFile(
            "ini/GameSetup.Ini",
            out string? actualPath);

        Assert.False(resolved);
        Assert.Null(actualPath);
    }

    [Theory]
    [InlineData("../GameSetup.Ini")]
    [InlineData("ini/../GameSetup.Ini")]
    [InlineData("/ini/GameSetup.Ini")]
    [InlineData(@"\ini\GameSetup.Ini")]
    [InlineData(@"C:\ini\GameSetup.Ini")]
    public void TryResolveFileWhenPathEscapesOrRootsOutsideContentThrowsArgumentException(
        string relativePath)
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        ClientContentRoot contentRoot = new(temporaryDirectory.RootPath);

        Assert.Throws<ArgumentException>(
            () => contentRoot.TryResolveFile(relativePath, out _));
    }
}
