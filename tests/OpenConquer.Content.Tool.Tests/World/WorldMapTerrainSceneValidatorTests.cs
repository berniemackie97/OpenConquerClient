using System.Text;
using OpenConquer.Content.Tool.World;

namespace OpenConquer.Content.Tool.Tests.World;

public sealed class WorldMapTerrainSceneValidatorTests
{
    private static readonly string[] s_files =
        ["stand01", "stand02", "stand03", "stand05", "stand06", "stand08"];

    [Fact]
    public void RetailNewbieDMap_BindsTwentyPlacementsToSixSceneFilesAndSourceFrames()
    {
        using TemporarySourceTree source = CreateSource();

        WorldMapTerrainSceneValidationResult result = WorldMapTerrainSceneValidator.Validate(source.RootPath, 1010);

        Assert.Equal((20, 6, 6, 6),
            (result.PlacementCount, result.SceneFileCount, result.ObjectCount, result.FrameCount));
    }

    [Fact]
    public void MissingAndInvalidReferencedFiles_FailRatherThanSilentlyRemovingObjects()
    {
        using TemporarySourceTree source = CreateSource();
        const string Scene = "map/Scene/stand06.scene";
        const string Image = "data/map/mapobj/sky/stand06.dds";

        File.Delete(source.ChildPath(Scene));
        Assert.Throws<FileNotFoundException>(() => WorldMapTerrainSceneValidator.Validate(source.RootPath, 1010));

        source.WriteBytes(Scene, [1, 0, 0, 0]);
        Assert.Throws<InvalidDataException>(() => WorldMapTerrainSceneValidator.Validate(source.RootPath, 1010));

        source.WriteBytes(Scene, CreateScene("stand06"));
        File.Delete(source.ChildPath(Image));
        Assert.Throws<FileNotFoundException>(() => WorldMapTerrainSceneValidator.Validate(source.RootPath, 1010));

        source.WriteBytes(Image, [0x44, 0x44, 0x53]);
        Assert.Throws<InvalidDataException>(() => WorldMapTerrainSceneValidator.Validate(source.RootPath, 1010));
    }

    private static TemporarySourceTree CreateSource()
    {
        TemporarySourceTree source = new();
        string fixture = Path.Combine(AppContext.BaseDirectory, "RetailFixture");
        source.WriteBytes("ini/GameMap.dat", File.ReadAllBytes(Path.Combine(fixture, "ini", "GameMap.dat")));
        source.WriteBytes("map/map/newbie.7z", File.ReadAllBytes(Path.Combine(fixture, "map", "newbie.7z")));

        StringBuilder ani = new();
        foreach (string name in s_files)
        {
            source.WriteBytes($"map/Scene/{name}.scene", CreateScene(name));
            source.WriteBytes($"data/map/mapobj/sky/{name}.dds", WorldTestDds.CreateDxt1());
            ani.Append('[').Append(name).Append(".tga]\nFrameAmount=1\nFrame0=data/map/mapobj/sky/")
                .Append(name).Append(".dds\n");
        }

        source.WriteText("ani/MapScene.ani", ani.ToString());
        return source;
    }

    private static byte[] CreateScene(string name)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.Latin1, leaveOpen: true);
        writer.Write(1);
        WriteField(writer, @"ani\MapScene.ani", 0x100);
        WriteField(writer, $"{name}.tga", 0x40);
        writer.Write(-60);
        writer.Write(-48);
        writer.Write(200);
        writer.Write(2);
        writer.Write(2);
        writer.Write(0u);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        for (int i = 0; i < 4 * 3; i++)
        {
            writer.Write(0);
        }

        writer.Flush();
        return stream.ToArray();
    }

    private static void WriteField(BinaryWriter writer, string value, int length)
    {
        byte[] bytes = Encoding.Latin1.GetBytes(value);
        writer.Write(bytes);
        writer.Write(new byte[length - bytes.Length]);
    }
}
