using System.Text;
using OpenConquer.Content.Map;

namespace OpenConquer.Content.Tests.Map;

public sealed class GameMapTerrainSceneResourcesTests
{
    private static readonly string[] s_sceneNames = ["stand01", "stand02", "stand03", "stand05", "stand06", "stand08"];

    [Fact]
    public void Load_NewbieMap_BindsPlacementsInMapOrderAndSharesEachSceneFile()
    {
        using TemporaryContentDirectory root = CreateNewbieContent();
        ClientContentRoot source = new(root.RootPath);
        GameMapContent map = GameMapContent.Load(source, GameMapCatalog.Load(source), 1010);

        GameMapTerrainSceneResources resources = GameMapTerrainSceneResources.Load(source, map);

        GameMapSceneryRecord[] terrainScenery = [.. map.Scene.Scenery.Where(static record => record.TypeCode == 1)];
        Assert.Equal(20, resources.Placements.Count);
        Assert.Equal(6, resources.SceneFileCount);
        Assert.Equal(terrainScenery, resources.Placements.Select(static placement => placement.Scenery));

        foreach (IGrouping<string?, GameMapTerrainScenePlacement> sameFile in
            resources.Placements.GroupBy(static placement => placement.Scenery.ResourcePath, StringComparer.OrdinalIgnoreCase))
        {
            IReadOnlyList<GameMapTerrainObjectBinding> shared = sameFile.First().Objects;
            Assert.All(sameFile, placement => Assert.Same(shared, placement.Objects));
        }

        GameMapTerrainObjectBinding binding = Assert.Single(resources.Placements[0].Objects);
        Assert.Equal(binding.Terrain.SectionName, binding.Animation.Name);
    }

    [Fact]
    public void Load_MissingSceneFile_ThrowsFileNotFound()
    {
        using TemporaryContentDirectory root = CreateNewbieContent();
        File.Delete(Path.Combine(root.RootPath, "map", "Scene", "stand06.scene"));
        ClientContentRoot source = new(root.RootPath);
        GameMapContent map = GameMapContent.Load(source, GameMapCatalog.Load(source), 1010);

        Assert.Throws<FileNotFoundException>(() => GameMapTerrainSceneResources.Load(source, map));
    }

    [Fact]
    public void Load_SceneReferencingAMissingAnimationSection_ThrowsInvalidData()
    {
        using TemporaryContentDirectory root = CreateNewbieContent();
        root.WriteFile("map/Scene/stand06.scene", CreateScene("unlisted"));
        ClientContentRoot source = new(root.RootPath);
        GameMapContent map = GameMapContent.Load(source, GameMapCatalog.Load(source), 1010);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => GameMapTerrainSceneResources.Load(source, map));

        Assert.Contains("unlisted.tga", exception.Message, StringComparison.Ordinal);
    }

    private static TemporaryContentDirectory CreateNewbieContent()
    {
        TemporaryContentDirectory root = new();
        string fixture = Path.Combine(AppContext.BaseDirectory, "RetailFixture");
        root.WriteFile("ini/GameMap.dat", File.ReadAllBytes(Path.Combine(fixture, "ini", "GameMap.dat")));
        root.WriteFile("map/map/newbie.7z", File.ReadAllBytes(Path.Combine(fixture, "map", "newbie.7z")));

        StringBuilder index = new();
        foreach (string name in s_sceneNames)
        {
            root.WriteFile($"map/Scene/{name}.scene", CreateScene(name));
            index.Append('[').Append(name).Append(".tga]\nFrameAmount=1\nFrame0=data/map/").Append(name).Append(".dds\n");
        }

        root.WriteFile("ani/MapScene.ani", index.ToString());
        return root;
    }

    private static byte[] CreateScene(string sectionStem)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.Latin1, leaveOpen: true);
        writer.Write(1);
        writer.Write(Field(@"ani\MapScene.ani", 0x100));
        writer.Write(Field($"{sectionStem}.tga", 0x40));
        foreach (int value in (ReadOnlySpan<int>)[-60, -48, 200, 1, 1, 0, 0, 0, 0, 0, 0, 0])
        {
            writer.Write(value);
        }

        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] Field(string value, int length)
    {
        byte[] field = new byte[length];
        Encoding.Latin1.GetBytes(value).CopyTo(field, 0);
        return field;
    }
}
