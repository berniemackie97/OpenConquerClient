using OpenConquer.Content.Ani;
using OpenConquer.Content.Tool.World;

namespace OpenConquer.Content.Tool.Tests.World;

public sealed class WorldMapSceneryAniValidatorTests
{
    [Fact]
    public void RetailNewbieScenery_ValidatesEveryDistinctReferencedFrame()
    {
        using TemporarySourceTree source = CreateSource();

        WorldMapSceneryAniValidationResult result = WorldMapSceneryAniValidator.Validate(source.RootPath, 1010);

        Assert.Equal((18, 16, 16), (result.SceneryCount, result.AnimationCount, result.FrameCount));
    }

    [Fact]
    public void MissingAndCorruptFrame_FailValidation()
    {
        using TemporarySourceTree source = CreateSource();
        const string Frame = "data/map/mapobj/newbie/newbie17.dds";

        File.Delete(source.ChildPath(Frame));
        Assert.Throws<FileNotFoundException>(() => WorldMapSceneryAniValidator.Validate(source.RootPath, 1010));

        source.WriteBytes(Frame, [0x44, 0x44, 0x53]);
        Assert.Throws<InvalidDataException>(() => WorldMapSceneryAniValidator.Validate(source.RootPath, 1010));
    }

    private static TemporarySourceTree CreateSource()
    {
        TemporarySourceTree source = new();
        string fixture = Path.Combine(AppContext.BaseDirectory, "RetailFixture");
        source.WriteBytes("ini/GameMap.dat", File.ReadAllBytes(Path.Combine(fixture, "ini", "GameMap.dat")));
        source.WriteBytes("map/map/newbie.7z", File.ReadAllBytes(Path.Combine(fixture, "map", "newbie.7z")));
        source.WriteBytes("ani/MapScene.ani", File.ReadAllBytes(Path.Combine(fixture, "ani", "mapscene-newbie-sections.ani")));

        AniIndexFile index = AniIndexFile.Load(new ClientContentRoot(source.RootPath),
            "ani/MapScene.ani", ContentLookupMode.LooseOnly);
        byte[] image = WorldTestDds.CreateDxt1();
        foreach (AniIndexSection section in index.Sections)
        {
            source.WriteBytes(Assert.Single(section.FramePaths), image);
        }

        return source;
    }
}
