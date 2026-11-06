using System.Buffers.Binary;
using OpenConquer.Networking.Game.World;

namespace OpenConquer.Client.Tests.Networking;

public sealed class GameUserAttrib10017Tests
{
    // typed-world-dispatch-2026-11-06/wire-layout-golden.json "attributes": wide low64 plus a nonzero status-25 high64.
    private const string GoldenFrame =
        "340021277856341202000000050000001032547698badcfe000000000000000019000000efcdab89674523011032547698badcfe";

    [Fact]
    public void TryRead_PreservesEntryOrderAndBothPayloadHalves()
    {
        Assert.True(GameUserAttrib10017.TryRead(Convert.FromHexString(GoldenFrame), out GameUserAttrib10017 attributes));
        Assert.Equal(0x12345678u, attributes.TargetId);
        Assert.Equal(2, attributes.Count);

        List<GameUserAttribEntry> entries = [];
        foreach (GameUserAttribEntry entry in attributes)
        {
            entries.Add(entry);
        }

        Assert.Equal(
            [
                new GameUserAttribEntry(5, 0xfedcba9876543210, 0),
                new GameUserAttribEntry(25, 0x0123456789abcdef, 0xfedcba9876543210),
            ],
            entries);
    }

    [Fact]
    public void TryRead_AcceptsAnEmptyEntryList()
    {
        byte[] packet = Frame(entryCount: 0, declaredCount: 0);
        Assert.True(GameUserAttrib10017.TryRead(packet, out GameUserAttrib10017 attributes));
        Assert.Equal(0, attributes.Count);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, 2)]
    [InlineData(2, uint.MaxValue)]
    public void TryRead_RejectsACountThatDisagreesWithTheLength(int entryCount, uint declaredCount) =>
        Assert.False(GameUserAttrib10017.TryRead(Frame(entryCount, declaredCount), out _));

    [Theory]
    [InlineData(11)]
    [InlineData(13)]
    [InlineData(31)]
    public void TryRead_RejectsLengthsThatAreNotWholeEntries(int length)
    {
        byte[] packet = new byte[length];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, (ushort)length);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), GameUserAttrib10017.PacketType);
        Assert.False(GameUserAttrib10017.TryRead(packet, out _));
    }

    [Fact]
    public void TryRead_RejectsAHeaderSizeThatDisagreesWithTheBuffer()
    {
        byte[] packet = Frame(entryCount: 1, declaredCount: 1);
        BinaryPrimitives.WriteUInt16LittleEndian(packet, (ushort)(packet.Length + 20));
        Assert.False(GameUserAttrib10017.TryRead(packet, out _));
    }

    private static byte[] Frame(int entryCount, uint declaredCount)
    {
        byte[] packet = new byte[GameUserAttrib10017.HeaderLength + entryCount * GameUserAttrib10017.EntryLength];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, (ushort)packet.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), GameUserAttrib10017.PacketType);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(8), declaredCount);
        return packet;
    }
}
