using OpenConquer.Content.Ani;

namespace OpenConquer.Content.Tests.Ani;

public sealed class AniSectionResolverTests
{
    private const string Index = "[stand01.tga]\nFrameAmount=1\nFrame0=data/map/stand01.dds\n[empty.tga]\nFrameAmount=0\n";

    [Fact]
    public void Resolve_ReturnsTheNamedSectionAndLoadsEachIndexOnce()
    {
        using TemporaryContentDirectory root = new();
        root.WriteFile("ani/MapScene.ani", Index);
        AniSectionResolver resolver = new(new ClientContentRoot(root.RootPath), ContentLookupMode.LooseOnly);

        AniIndexSection first = resolver.Resolve(@"ani\MapScene.ani", "stand01.tga");
        File.Delete(Path.Combine(root.RootPath, "ani", "MapScene.ani"));
        AniIndexSection second = resolver.Resolve("ANI/mapscene.ani", "stand01.tga");

        Assert.Same(first, second);
        Assert.Equal("data/map/stand01.dds", Assert.Single(first.FramePaths));
    }

    [Theory]
    [InlineData("missing.tga")]
    [InlineData("empty.tga")]
    public void Resolve_MissingOrEmptySection_ThrowsInvalidData(string sectionName)
    {
        using TemporaryContentDirectory root = new();
        root.WriteFile("ani/MapScene.ani", Index);
        AniSectionResolver resolver = new(new ClientContentRoot(root.RootPath), ContentLookupMode.LooseOnly);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => resolver.Resolve("ani/MapScene.ani", sectionName));

        Assert.Contains(sectionName, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_SectionWithACollidingNameHash_ThrowsInvalidData()
    {
        using TemporaryContentDirectory root = new();
        root.WriteFile("ani/MapScene.ani", "[newbjD17.tga]\nFrameAmount=1\nFrame0=data/map/a.dds\n");
        AniSectionResolver resolver = new(new ClientContentRoot(root.RootPath), ContentLookupMode.LooseOnly);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => resolver.Resolve("ani/MapScene.ani", "newbie17.tga"));

        Assert.Contains("newbjD17.tga", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_MissingIndex_ThrowsFileNotFound()
    {
        using TemporaryContentDirectory root = new();
        AniSectionResolver resolver = new(new ClientContentRoot(root.RootPath), ContentLookupMode.LooseOnly);

        Assert.Throws<FileNotFoundException>(() => resolver.Resolve("ani/MapScene.ani", "stand01.tga"));
    }
}
