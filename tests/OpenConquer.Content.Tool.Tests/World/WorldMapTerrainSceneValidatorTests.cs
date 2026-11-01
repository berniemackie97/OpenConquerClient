using System.Text;
using OpenConquer.Tests.Fixtures;
using OpenConquer.Content.Tool.World;

namespace OpenConquer.Content.Tool.Tests.World;

public sealed class WorldMapTerrainSceneValidatorTests
{
    private const string Scene = "map/Scene/stand06.scene";
    private const string Image = "data/map/mapobj/sky/stand06.dds";

    private static readonly string[] s_sceneNames = ["stand01", "stand02", "stand03", "stand05", "stand06", "stand08"];

    [Fact]
    public void Validate_NewbieMap_CountsPlacementsSceneFilesObjectsAndFrames()
    {
        using TemporarySourceTree source = CreateSource();

        WorldMapTerrainSceneValidationResult result = WorldMapTerrainSceneValidator.Validate(source.RootPath, 1010);

        Assert.Equal((20, 6, 6, 6), (result.PlacementCount, result.SceneFileCount, result.ObjectCount, result.FrameCount));
    }

    [Theory]
    [InlineData("missing-scene", typeof(FileNotFoundException))]
    [InlineData("corrupt-scene", typeof(InvalidDataException))]
    [InlineData("missing-image", typeof(FileNotFoundException))]
    [InlineData("corrupt-image", typeof(InvalidDataException))]
    public void Validate_BrokenDependency_Throws(string breakage, Type expected)
    {
        using TemporarySourceTree source = CreateSource();
        switch (breakage)
        {
            case "missing-scene":
                File.Delete(source.ChildPath(Scene));
                break;
            case "corrupt-scene":
                source.WriteBytes(Scene, [1, 0, 0, 0]);
                break;
            case "missing-image":
                File.Delete(source.ChildPath(Image));
                break;
            case "corrupt-image":
                source.WriteBytes(Image, [0x44, 0x44, 0x53]);
                break;
        }

        Assert.Throws(expected, () => WorldMapTerrainSceneValidator.Validate(source.RootPath, 1010));
    }

    private static TemporarySourceTree CreateSource()
    {
        TemporarySourceTree source = new();
        string fixture = Path.Combine(AppContext.BaseDirectory, "RetailFixture");
        source.WriteBytes("ini/GameMap.dat", File.ReadAllBytes(Path.Combine(fixture, "ini", "GameMap.dat")));
        source.WriteBytes("map/map/newbie.7z", File.ReadAllBytes(Path.Combine(fixture, "map", "newbie.7z")));

        StringBuilder index = new();
        foreach (string name in s_sceneNames)
        {
            source.WriteBytes($"map/Scene/{name}.scene", TerrainSceneTestData.Create(name, 2, 2));
            source.WriteBytes($"data/map/mapobj/sky/{name}.dds", WorldTestDds.CreateDxt1());
            index.Append('[').Append(name).Append(".tga]\nFrameAmount=1\nFrame0=data/map/mapobj/sky/")
                .Append(name).Append(".dds\n");
        }

        source.WriteText("ani/MapScene.ani", index.ToString());
        return source;
    }

}
