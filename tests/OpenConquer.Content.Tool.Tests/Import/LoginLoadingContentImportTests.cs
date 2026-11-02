using OpenConquer.Content.Login;
using OpenConquer.Content.Tool.Import;
using OpenConquer.Content.Tool.Verify;

namespace OpenConquer.Content.Tool.Tests.Import;

public sealed class LoginLoadingContentImportTests
{
    [Fact]
    public void PreparedLoadingMediaIsIncludedInTheVerifiedContentSet()
    {
        using TemporarySourceTree source = new();
        using TemporarySourceTree media = new();
        using TemporarySourceTree destinationParent = new();
        source.WriteStartupSnapshot();

        foreach (string path in LoginLoadingContentPaths.Paths)
        {
            media.WriteBytes(path, [1, 2, 3, 4]);
        }

        string destination = destinationParent.ChildPath("set");
        var imported = ContentSetImporter.Import(source.RootPath, destination, loadingMediaRootPath: media.RootPath);
        var verified = ContentSetVerifier.Verify(destination);

        Assert.Equal(imported.FileCount, verified.FileCount);
        Assert.All(LoginLoadingContentPaths.Paths, path =>
            Assert.Contains(imported.Entries, entry => entry.SourcePath == path));
        Assert.Equal(111 + LoginLoadingContentPaths.Paths.Count, imported.FileCount);
    }

    [Fact]
    public void IncompleteLoadingMediaDoesNotPublishAContentSet()
    {
        using TemporarySourceTree source = new();
        using TemporarySourceTree media = new();
        using TemporarySourceTree destinationParent = new();
        source.WriteStartupSnapshot();
        media.WriteBytes(LoginLoadingContentPaths.Timeline, [1, 2, 3, 4]);

        string destination = destinationParent.ChildPath("set");
        Assert.Throws<FileNotFoundException>(() =>
            ContentSetImporter.Import(source.RootPath, destination, loadingMediaRootPath: media.RootPath));
        Assert.False(Directory.Exists(destination));
    }
}
