namespace OpenConquer.Content.Tests;

public sealed class ClientContentPathContractTests
{
    [Fact]
    public void PackagedSource_RejectsNullVirtualPathWithArgumentNullException()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        PackagedClientContentSource source = PackagedClientContentSource.Open(
            temporaryDirectory.RootPath
        );

        Assert.Throws<ArgumentNullException>(() =>
            source.TryOpenRead(null!, ContentLookupMode.PackageOnly, out _)
        );
    }

    [Fact]
    public void LooseSource_RejectsNullRelativePathWithArgumentNullException()
    {
        using TemporaryContentDirectory temporaryDirectory = new();

        ClientContentRoot source = new(temporaryDirectory.RootPath);

        Assert.Throws<ArgumentNullException>(() => source.TryResolveFile(null!, out _));
    }
}
