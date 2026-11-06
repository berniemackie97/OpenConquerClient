using System.Buffers.Binary;
using OpenConquer.Networking.Game.World;

namespace OpenConquer.Client.Tests.Networking;

public sealed class GameUserAttrib10017Tests
{
    // typed-world-dispatch-2026-11-06/wire-layout-golden.json "attributes": wide low64 plus a nonzero status-25 high64.
    private const string GoldenFrame =
        "340021277856341202000000050000001032547698badcfe000000000000000019000000efcdab89674523011032547698badcfe";

    [Fact]
    public void Read_PreservesEntryOrderAndBothPayloadHalves()
    {
        GameUserAttrib10017 attributes = GameUserAttrib10017.Read(GameMessages.Load(Convert.FromHexString(GoldenFrame)));
        Assert.Equal(0x12345678u, attributes.TargetId);
        Assert.Equal(2, attributes.Count);

        Assert.Equal(
            [
                new GameUserAttribEntry(5, 0xfedcba9876543210, 0),
                new GameUserAttribEntry(25, 0x0123456789abcdef, 0xfedcba9876543210),
            ],
            Entries(attributes));
    }

    // Native loops the declared count with no length check (0x758B92); entries past the message read zero.
    [Fact]
    public void Read_ProcessesTheDeclaredCountWithZeroEntriesPastTheMessage()
    {
        byte[] packet = Frame(entryCount: 1, declaredCount: 3);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(12), 5);
        BinaryPrimitives.WriteUInt64LittleEndian(packet.AsSpan(16), 0x1234);

        GameUserAttrib10017 attributes = GameUserAttrib10017.Read(GameMessages.Load(packet));

        Assert.Equal(
            [new GameUserAttribEntry(5, 0x1234, 0), new GameUserAttribEntry(0, 0, 0), new GameUserAttribEntry(0, 0, 0)],
            Entries(attributes));
    }

    [Fact]
    public void Read_IgnoresEntriesBeyondTheDeclaredCount()
    {
        byte[] packet = Frame(entryCount: 2, declaredCount: 1);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(32), 7);

        Assert.Equal([new GameUserAttribEntry(0, 0, 0)], Entries(GameUserAttrib10017.Read(GameMessages.Load(packet))));
    }

    // The native count is signed (jl): zero or negative runs no entries. Above 50 native reads past its buffer; the
    // managed reader stops at the last whole entry inside it.
    [Theory]
    [InlineData(0u, 0)]
    [InlineData(0x80000000u, 0)]
    [InlineData(uint.MaxValue, 0)]
    [InlineData(50u, 50)]
    [InlineData(51u, 50)]
    [InlineData(0x7fffffffu, 50)]
    public void Count_FollowsTheSignedNativeCountUpToTheBufferEdge(uint declared, int expected)
    {
        GameUserAttrib10017 attributes = GameUserAttrib10017.Read(GameMessages.Load(Frame(entryCount: 0, declaredCount: declared)));
        Assert.Equal(expected, attributes.Count);
        Assert.Equal(expected, Entries(attributes).Count);
    }

    private static List<GameUserAttribEntry> Entries(GameUserAttrib10017 attributes)
    {
        List<GameUserAttribEntry> entries = [];
        foreach (GameUserAttribEntry entry in attributes)
        {
            entries.Add(entry);
        }

        return entries;
    }

    private static byte[] Frame(int entryCount, uint declaredCount)
    {
        byte[] packet = GameMessages.Header(GameUserAttrib10017.HeaderLength + entryCount * GameUserAttrib10017.EntryLength,
            GameUserAttrib10017.PacketType);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(8), declaredCount);
        return packet;
    }
}
