using System.Text;

namespace OpenConquer.Tests.Fixtures;

internal static class TerrainSceneTestData
{
    private const int AnimationPathBytes = 0x100;
    private const int SectionNameBytes = 0x40;
    private const int CellBytes = 3 * sizeof(int);

    public static byte[] Create(string sectionStem, int width, int height)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.Latin1, leaveOpen: true);
        writer.Write(1);
        WriteField(writer, @"ani\MapScene.ani", AnimationPathBytes);
        WriteField(writer, $"{sectionStem}.tga", SectionNameBytes);
        writer.Write(-60);
        writer.Write(-48);
        writer.Write(200);
        writer.Write(width);
        writer.Write(height);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(new byte[checked(width * height * CellBytes)]);
        return stream.ToArray();
    }

    private static void WriteField(BinaryWriter writer, string value, int fieldBytes)
    {
        byte[] bytes = Encoding.Latin1.GetBytes(value);
        writer.Write(bytes);
        writer.Write(new byte[fieldBytes - bytes.Length]);
    }
}
