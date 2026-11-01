using System.Buffers.Binary;

namespace OpenConquer.Content.Tool.Tests.World;

/// <summary>Generated DXT1 test pixels, not original retail artwork.</summary>
internal static class WorldTestDds
{
    public static byte[] CreateDxt1()
    {
        byte[] bytes = new byte[136];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x20534444);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 124);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 0x00081007);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), 4);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), 4);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(20), 8);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(76), 32);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(80), 4);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(84), 0x31545844);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(108), 0x1000);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(128), 0xF800);
        return bytes;
    }
}
