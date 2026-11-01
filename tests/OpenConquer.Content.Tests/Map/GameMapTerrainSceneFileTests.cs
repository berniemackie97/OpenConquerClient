using System.Buffers.Binary;
using System.Text;
using OpenConquer.Content.Map;

namespace OpenConquer.Content.Tests.Map;

public sealed class GameMapTerrainSceneFileTests
{
    private const int CountBytes = sizeof(int);
    private const int PathBytes = 0x100;
    private const int SectionBytes = 0x40;
    private const int GridWidthOffset = CountBytes + PathBytes + SectionBytes + 3 * sizeof(int);

    public static TheoryData<string> MalformedCases =>
    [
        "truncated", "trailing", "negative-count", "count-beyond-file", "negative-grid", "oversized-grid",
        "empty-path", "unterminated-path", "parent-path", "rooted-path", "empty-section",
    ];

    [Fact]
    public void Parse_ReadsEveryObjectFieldAndTheLowWordOfEachCellValue()
    {
        byte[] data = CreateScene();

        GameMapTerrainObject terrainObject = Assert.Single(GameMapTerrainSceneFile.Parse(data).Objects);

        Assert.Equal(CountBytes + 0x164 + 2 * 12, data.Length);
        Assert.Equal(("ani/MapScene.ani", "stand06.tga"), (terrainObject.AnimationPath, terrainObject.SectionName));
        Assert.Equal((-60, -48, 200), (terrainObject.RenderOffsetX, terrainObject.RenderOffsetY, terrainObject.FrameIntervalMilliseconds));
        Assert.Equal((2, 1), (terrainObject.GridWidth, terrainObject.GridHeight));
        Assert.Equal(0xAABBCCDDu, terrainObject.AuthoredColor);
        Assert.Equal((3, -4, 7), (terrainObject.AnchorOffsetX, terrainObject.AnchorOffsetY, terrainObject.DrawParameter));
        Assert.Equal([new GameMapTerrainCell(1, 0x8002, -1), new GameMapTerrainCell(3, 4, 5)], terrainObject.Cells);
    }

    [Fact]
    public void Parse_AcceptsAnEmptyScene()
    {
        Assert.Empty(GameMapTerrainSceneFile.Parse([0, 0, 0, 0]).Objects);
    }

    [Theory]
    [MemberData(nameof(MalformedCases))]
    public void Parse_MalformedScene_ThrowsInvalidData(string malformation)
    {
        byte[] data = CreateMalformedScene(malformation);

        Assert.Throws<InvalidDataException>(() => GameMapTerrainSceneFile.Parse(data));
    }

    private static byte[] CreateMalformedScene(string malformation)
    {
        byte[] scene = CreateScene();
        switch (malformation)
        {
            case "truncated":
                return scene[..^1];
            case "trailing":
                return [.. scene, 0];
            case "negative-count":
                BinaryPrimitives.WriteInt32LittleEndian(scene, -1);
                return scene;
            case "count-beyond-file":
                BinaryPrimitives.WriteInt32LittleEndian(scene, 2);
                return scene;
            case "negative-grid":
                BinaryPrimitives.WriteInt32LittleEndian(scene.AsSpan(GridWidthOffset), -1);
                return scene;
            case "oversized-grid":
                BinaryPrimitives.WriteInt32LittleEndian(scene.AsSpan(GridWidthOffset), int.MaxValue);
                return scene;
            case "empty-path":
                scene.AsSpan(CountBytes, PathBytes).Clear();
                return scene;
            case "unterminated-path":
                scene.AsSpan(CountBytes, PathBytes).Fill((byte)'a');
                return scene;
            case "parent-path":
                WriteField(scene.AsSpan(CountBytes, PathBytes), "../outside.ani");
                return scene;
            case "rooted-path":
                WriteField(scene.AsSpan(CountBytes, PathBytes), "C:/outside.ani");
                return scene;
            case "empty-section":
                scene.AsSpan(CountBytes + PathBytes, SectionBytes).Clear();
                return scene;
            default:
                throw new ArgumentOutOfRangeException(nameof(malformation), malformation, null);
        }
    }

    private static byte[] CreateScene()
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.Latin1, leaveOpen: true);
        writer.Write(1);
        writer.Write(Field(@"ani\MapScene.ani", PathBytes));
        writer.Write(Field("stand06.tga", SectionBytes));
        writer.Write(-60);
        writer.Write(-48);
        writer.Write(200);
        writer.Write(2);
        writer.Write(1);
        writer.Write(0xAABBCCDDu);
        writer.Write(3);
        writer.Write(-4);
        writer.Write(7);
        writer.Write(0x12340001);
        writer.Write(0xFEDC8002u);
        writer.Write(0xABCDFFFFu);
        writer.Write(3);
        writer.Write(4);
        writer.Write(5);
        writer.Flush();
        return stream.ToArray();
    }

    private static byte[] Field(string value, int length)
    {
        byte[] field = new byte[length];
        WriteField(field, value);
        return field;
    }

    private static void WriteField(Span<byte> field, string value)
    {
        field.Clear();
        Encoding.Latin1.GetBytes(value).CopyTo(field);
    }
}
