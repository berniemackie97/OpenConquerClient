using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using OpenConquer.Content.Ani;
using OpenConquer.Content.Map;

namespace OpenConquer.Content.Tests.Map;

public sealed class GameMapSceneryAniBindingsTests
{
    [Fact]
    public void NewbieScene_BindsEighteenAnimatedSceneryRecordsInSourceOrder()
    {
        using TemporaryContentDirectory root = new();
        root.WriteFile("ani/MapScene.ani", ReadSourceSections());
        GameMapSceneSections scene = ReadScene();

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

    [Theory]
    [InlineData("PosCell=[0,0]")]
    [InlineData("c-city18.tga")]
    public void UnconfiguredMapScenery_DoesNotRequestAnAnimationFile(string partName)
    {
        using TemporaryContentDirectory root = new();
        root.WriteFile("ani/MapScene.ani", ReadSourceSections());

        GameMapSceneSections scene = SceneWithAdditionalScenery("AniTitle=", partName, 0, 0, 0);
        Assert.Equal(39, scene.Scenery.Count);
        Assert.Equal("AniTitle=", scene.Scenery[0].ResourcePath);
        Assert.Equal(partName, scene.Scenery[0].PartName);

        IReadOnlyList<GameMapSceneryAniBinding> bindings =
            GameMapSceneryAniBindings.Load(new ClientContentRoot(root.RootPath), scene);

        Assert.Equal(18, bindings.Count);
        Assert.DoesNotContain(bindings, static binding => binding.Scenery.ResourcePath == "AniTitle=");
    }

    [Fact]
    public void IncompleteSceneryConfiguration_DoesNotSuppressOtherMissingReferences()
    {
        using TemporaryContentDirectory root = new();
        root.WriteFile("ani/MapScene.ani", ReadSourceSections());

        GameMapSceneSections[] invalid =
        [
            SceneWithAdditionalScenery("ani/missing.ani", "c-city18.tga", 0, 0, 0),
            SceneWithAdditionalScenery("AniTitle=", "c-city18.tga", 1, 0, 0),
            SceneWithAdditionalScenery("AniTitle=", "c-city18.tga", 0, 1, 0),
            SceneWithAdditionalScenery("AniTitle=", "c-city18.tga", 0, 0, 1),
        ];

        foreach (GameMapSceneSections scene in invalid)
        {
            Assert.Throws<FileNotFoundException>(() =>
                GameMapSceneryAniBindings.Load(new ClientContentRoot(root.RootPath), scene));
        }
    }

    [Fact]
    public void MissingAniOrReferencedSection_FailsInsteadOfDiscardingScenery()
    {
        GameMapSceneSections scene = ReadScene();
        using TemporaryContentDirectory root = new();
        ClientContentRoot source = new(root.RootPath);

        Assert.Throws<FileNotFoundException>(() => GameMapSceneryAniBindings.Load(source, scene));

        string original = Encoding.Latin1.GetString(ReadSourceSections());
        root.WriteFile("ani/MapScene.ani", original.Replace("[newbie17.tga]", "[unrelated.tga]", StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(() => GameMapSceneryAniBindings.Load(source, scene));
    }

    [Fact]
    public void HashCollidingSectionName_CannotSatisfyRetailSceneryReference()
    {
        GameMapSceneSections scene = ReadScene();
        using TemporaryContentDirectory root = new();
        string original = Encoding.Latin1.GetString(ReadSourceSections());
        string collision = original.Replace("[newbie17.tga]", "[newbjD17.tga]", StringComparison.Ordinal);
        Assert.NotEqual(original, collision);
        root.WriteFile("ani/MapScene.ani", collision);

        ClientContentRoot source = new(root.RootPath);
        AniIndexFile index = AniIndexFile.Load(source, "ani/MapScene.ani", ContentLookupMode.LooseOnly);
        Assert.Equal("newbjD17.tga", index.GetRequiredSection("newbie17.tga").Name);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            GameMapSceneryAniBindings.Load(source, scene));
        Assert.Contains("newbie17.tga", error.Message, StringComparison.Ordinal);
        Assert.Contains("newbjD17.tga", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyReferencedSection_FailsRatherThanProducingAnUnrenderableBinding()
    {
        GameMapSceneSections scene = ReadScene();
        using TemporaryContentDirectory root = new();
        string original = Encoding.Latin1.GetString(ReadSourceSections());
        root.WriteFile("ani/MapScene.ani", original.Replace(
            "[newbie17.tga]\nFrameAmount=1\nFrame0=data/map/mapobj/newbie/newbie17.dds",
            "[newbie17.tga]\nFrameAmount=0", StringComparison.Ordinal));

        Assert.Throws<InvalidDataException>(() =>
            GameMapSceneryAniBindings.Load(new ClientContentRoot(root.RootPath), scene));
    }

    private static GameMapSceneSections SceneWithAdditionalScenery(
        string path, string section, int x, int y, int extraValue)
    {
        string fixture = Path.Combine(AppContext.BaseDirectory, "RetailFixture", "map", "newbie.scene.zlib");
        using FileStream source = File.OpenRead(fixture);
        using ZLibStream decoded = new(source, CompressionMode.Decompress);
        using MemoryStream stream = new();
        decoded.CopyTo(stream);
        byte[] original = stream.ToArray();

        const int RecordLength = 4 + 0x1A0;
        byte[] combined = new byte[original.Length + RecordLength];
        original.AsSpan(0, 8).CopyTo(combined);
        BinaryPrimitives.WriteInt32LittleEndian(combined.AsSpan(4), 39);
        BinaryPrimitives.WriteInt32LittleEndian(combined.AsSpan(8), 4);

        Span<byte> record = combined.AsSpan(12, 0x1A0);
        Encoding.Latin1.GetBytes(path, record);
        Encoding.Latin1.GetBytes(section, record[0x104..]);
        BinaryPrimitives.WriteInt32LittleEndian(record[0x184..], x);
        BinaryPrimitives.WriteInt32LittleEndian(record[0x188..], y);
        BinaryPrimitives.WriteInt32LittleEndian(record[0x18C..], extraValue);

        original.AsSpan(8).CopyTo(combined.AsSpan(8 + RecordLength));
        return GameMapSceneSections.Parse(combined, 0, 1004);
    }

    private static byte[] ReadSourceSections() =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "RetailFixture", "ani", "mapscene-newbie-sections.ani"));

    private static GameMapSceneSections ReadScene()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "RetailFixture", "map", "newbie.scene.zlib");
        using FileStream source = File.OpenRead(path);
        using ZLibStream decoded = new(source, CompressionMode.Decompress);
        using MemoryStream result = new();
        decoded.CopyTo(result);
        return GameMapSceneSections.Parse(result.ToArray(), 0, 1004);
    }
}
