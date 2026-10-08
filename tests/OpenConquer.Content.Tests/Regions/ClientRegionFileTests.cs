using System.Buffers.Binary;
using OpenConquer.Content.Regions;

namespace OpenConquer.Content.Tests.Regions;

public sealed class ClientRegionFileTests
{
    [Fact]
    public void Parse_UsesNativeRectangleUnionAndHalfOpenEdges()
    {
        byte[] encoded = CreateFile(
            new ClientRegionRectangle(10, 20, 20, 30),
            new ClientRegionRectangle(30, 40, 50, 60));

        ClientRegionFile region = ClientRegionFile.Parse(encoded);

        Assert.Equal(2, region.RectangleCount);
        Assert.Equal(new ClientRegionRectangle(10, 20, 50, 60), region.Bounds);
        Assert.True(region.Contains(10, 20));
        Assert.True(region.Contains(19, 29));
        Assert.True(region.Contains(49, 59));
        Assert.False(region.Contains(20, 30));
        Assert.False(region.Contains(29, 40));
        Assert.False(region.Contains(50, 60));
        Assert.False(region.Contains(25, 35));
    }

    [Theory]
    [InlineData(0, 0u)]
    [InlineData(4, 2u)]
    [InlineData(8, 0u)]
    [InlineData(12, 32u)]
    public void Parse_RejectsTamperedRegionHeader(int offset, uint replacement)
    {
        byte[] encoded = CreateFile(new ClientRegionRectangle(1, 2, 5, 9));
        BinaryPrimitives.WriteUInt32LittleEndian(encoded.AsSpan(sizeof(uint) + offset), replacement);

        Assert.Throws<InvalidDataException>(() => ClientRegionFile.Parse(encoded));
    }

    [Fact]
    public void Parse_RejectsTruncatedAndExtendedFiles()
    {
        byte[] encoded = CreateFile(new ClientRegionRectangle(1, 2, 5, 9));

        Assert.Throws<InvalidDataException>(() => ClientRegionFile.Parse([.. encoded, 0]));
        Assert.Throws<InvalidDataException>(() => ClientRegionFile.Parse(encoded.AsSpan()[..^1]));
    }

    [Fact]
    public void Parse_RejectsIncorrectBounds()
    {
        byte[] encoded = CreateFile(new ClientRegionRectangle(1, 2, 5, 9));
        BinaryPrimitives.WriteInt32LittleEndian(encoded.AsSpan(sizeof(uint) + 16), 0);

        Assert.Throws<InvalidDataException>(() => ClientRegionFile.Parse(encoded));
    }

    [Fact]
    public void Parse_RejectsNonpositiveRectangles()
    {
        byte[] encoded = CreateFile(new ClientRegionRectangle(1, 2, 5, 9));
        BinaryPrimitives.WriteInt32LittleEndian(encoded.AsSpan(sizeof(uint) + 32 + 8), 1);

        Assert.Throws<InvalidDataException>(() => ClientRegionFile.Parse(encoded));
    }

    [Fact]
    public void Load_UsesLooseOnlyContent()
    {
        using TemporaryContentDirectory directory = new();
        directory.WriteFile("ini/ProgressHp.rgn", CreateFile(new ClientRegionRectangle(1, 2, 5, 9)));

        ClientRegionFile region = ClientRegionFile.Load(new ClientContentRoot(directory.RootPath), "ini/ProgressHp.rgn");

        Assert.True(region.Contains(2, 3));
    }

    private static byte[] CreateFile(params ClientRegionRectangle[] rectangles)
    {
        int payloadLength = 32 + rectangles.Length * 16;
        byte[] encoded = new byte[payloadLength + sizeof(uint)];

        BinaryPrimitives.WriteUInt32LittleEndian(encoded, (uint)payloadLength);
        Span<byte> payload = encoded.AsSpan(sizeof(uint));

        BinaryPrimitives.WriteUInt32LittleEndian(payload, 32);
        BinaryPrimitives.WriteUInt32LittleEndian(payload[4..], 1);
        BinaryPrimitives.WriteUInt32LittleEndian(payload[8..], (uint)rectangles.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(payload[12..], (uint)(rectangles.Length * 16));

        ClientRegionRectangle bounds = new(
            rectangles.Min(static rectangle => rectangle.Left),
            rectangles.Min(static rectangle => rectangle.Top),
            rectangles.Max(static rectangle => rectangle.Right),
            rectangles.Max(static rectangle => rectangle.Bottom));

        WriteRectangle(payload[16..], bounds);

        for (int index = 0; index < rectangles.Length; index++)
        {
            WriteRectangle(payload[(32 + index * 16)..], rectangles[index]);
        }

        return encoded;
    }

    private static void WriteRectangle(Span<byte> destination, ClientRegionRectangle rectangle)
    {
        BinaryPrimitives.WriteInt32LittleEndian(destination, rectangle.Left);
        BinaryPrimitives.WriteInt32LittleEndian(destination[4..], rectangle.Top);
        BinaryPrimitives.WriteInt32LittleEndian(destination[8..], rectangle.Right);
        BinaryPrimitives.WriteInt32LittleEndian(destination[12..], rectangle.Bottom);
    }
}
