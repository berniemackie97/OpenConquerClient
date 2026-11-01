using System.IO.Compression;
using System.Text;
using OpenConquer.Content.Map;

namespace OpenConquer.Content.Tests.Map;

public sealed class GameMapSceneryAniBindingsTests
{
    [Fact]
    public void RetailNewbieScene_BindsEighteenAnimatedSceneryRecordsInSourceOrder()
    {
        using TemporaryContentDirectory root = new();
        root.WriteFile("ani/MapScene.ani", ReadSourceSections());
        GameMapSceneSections scene = ReadRetailScene();

        IReadOnlyList<GameMapSceneryAniBinding> bindings =
            GameMapSceneryAniBindings.Load(new ClientContentRoot(root.RootPath), scene);

        Assert.Equal(18, bindings.Count);
        Assert.Equal(
            ["newbie17.tga", "newbie16.tga", "newbie15.tga", "newbie10.tga", "newbie09.tga",
             "newbie07.tga", "newbie01.tga", "newbie06.tga", "newbie05.tga", "newbie06.tga",
             "newbie11.tga", "newbie12.tga", "newbie13.tga", "newbie18.tga", "newbie14.tga",
             "newbie04.tga", "newbie08.tga", "newbie16.tga"],
            bindings.Select(static binding => binding.Animation.Name));
        Assert.Same(scene.Scenery[0], bindings[0].Scenery);
        Assert.Equal("data/map/mapobj/newbie/newbie17.dds",
            Assert.Single(bindings[0].Animation.FramePaths));
        Assert.Same(bindings[1].Animation, bindings[17].Animation);
        Assert.Same(bindings[7].Animation, bindings[9].Animation);
        Assert.All(bindings, static binding => Assert.Equal(1, binding.Animation.FrameCount));
    }

    [Fact]
    public void MissingAniOrReferencedSection_FailsInsteadOfDiscardingScenery()
    {
        GameMapSceneSections scene = ReadRetailScene();
        using TemporaryContentDirectory root = new();
        ClientContentRoot source = new(root.RootPath);

        Assert.Throws<FileNotFoundException>(() => GameMapSceneryAniBindings.Load(source, scene));

        string original = Encoding.Latin1.GetString(ReadSourceSections());
        root.WriteFile("ani/MapScene.ani", original.Replace("[newbie17.tga]", "[unrelated.tga]", StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(() => GameMapSceneryAniBindings.Load(source, scene));
    }

    [Fact]
    public void EmptyReferencedSection_FailsRatherThanProducingAnUnrenderableBinding()
    {
        GameMapSceneSections scene = ReadRetailScene();
        using TemporaryContentDirectory root = new();
        string original = Encoding.Latin1.GetString(ReadSourceSections());
        root.WriteFile("ani/MapScene.ani", original.Replace(
            "[newbie17.tga]\nFrameAmount=1\nFrame0=data/map/mapobj/newbie/newbie17.dds",
            "[newbie17.tga]\nFrameAmount=0", StringComparison.Ordinal));

        Assert.Throws<InvalidDataException>(() =>
            GameMapSceneryAniBindings.Load(new ClientContentRoot(root.RootPath), scene));
    }

    private static byte[] ReadSourceSections() =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "RetailFixture", "ani", "mapscene-newbie-sections.ani"));

    private static GameMapSceneSections ReadRetailScene()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "RetailFixture", "map", "newbie.scene.zlib");
        using FileStream source = File.OpenRead(path);
        using ZLibStream decoded = new(source, CompressionMode.Decompress);
        using MemoryStream result = new();
        decoded.CopyTo(result);
        return GameMapSceneSections.Parse(result.ToArray(), 0);
    }
}
