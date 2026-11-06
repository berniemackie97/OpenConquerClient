using System.Buffers.Binary;
using OpenConquer.Networking.Game.Framing;
using OpenConquer.Networking.Game.World;

namespace OpenConquer.Client.Tests.Networking;

public sealed class GameItem1009Tests
{
    // Synthetic sentinel frames built from the 20_notes 1009 layout and the CP2.3b snapshot audit, not captures.
    private const string SnapshotFrame =
        "5800f1030d0c0b0a040302012e0000000000000000000000000000000000000000100000011000000210000003100000" +
        "0410000005100000061000000710000008100000091000000a1000000b1000000c10000000000000";

    private const string ListFrame =
        "6000f1035500000004000000050000000000000003000000000000000000000000000000000000000000000000000000" +
        "000000000000000000000000000000000000000000000000000000000000000000000000002000000120000002200000";

    [Fact]
    public void Snapshot_ReadsThirteenUidsFromOffset0x20InWireOrder()
    {
        GameItem1009 item = GameItem1009.Read(GameMessages.Load(Convert.FromHexString(SnapshotFrame)));
        Assert.Equal((0x0a0b0c0du, 0x01020304u, GameItem1009.EquipmentSnapshot), (item.ParameterA, item.ParameterB, item.Subtype));
        Assert.Equal(88, item.Length);
        for (int i = 0; i < GameItem1009.SnapshotItemCount; i++)
        {
            Assert.Equal(0x1000u + (uint)i, item.GetSnapshotItemId(i));
        }
    }

    [Fact]
    public void List_ReadsTheDeclaredCountAndEntriesAfterTheFixedBody()
    {
        GameItem1009 item = GameItem1009.Read(GameMessages.Load(Convert.FromHexString(ListFrame)));
        Assert.Equal((GameItem1009.EquipItem, 3u, 3), (item.Subtype, item.DeclaredListCount, item.ListCount));
        Assert.Equal([0x2000u, 0x2001u, 0x2002u], [item.GetListedId(0), item.GetListedId(1), item.GetListedId(2)]);
    }

    // Parse 0x6EAE56 has no size rule; the buffer is zero past the message.
    [Fact]
    public void Read_ReadsZeroPastAShortMessage()
    {
        GameItem1009 snapshot = GameItem1009.Read(GameMessages.Load(GameMessages.Truncate(Convert.FromHexString(SnapshotFrame), 0x28)));
        Assert.Equal(GameItem1009.EquipmentSnapshot, snapshot.Subtype);
        Assert.Equal([0x1000u, 0x1001u, 0u], [snapshot.GetSnapshotItemId(0), snapshot.GetSnapshotItemId(1), snapshot.GetSnapshotItemId(2)]);

        GameItem1009 list = GameItem1009.Read(GameMessages.Load(GameMessages.Truncate(Convert.FromHexString(ListFrame), 88)));
        Assert.Equal((3u, 3), (list.DeclaredListCount, list.ListCount));
        Assert.Equal([0x2000u, 0u, 0u], [list.GetListedId(0), list.GetListedId(1), list.GetListedId(2)]);

        GameItem1009 bare = GameItem1009.Read(GameMessages.Load(GameMessages.Header(4, GameItem1009.PacketType)));
        Assert.Equal((0u, 0u, (ushort)0, 0), (bare.ParameterA, bare.ParameterB, bare.Subtype, bare.ListCount));
    }

    // Native subtype 5 loops the unsigned count past its buffer (0x6EB436); the managed list stops at the buffer edge.
    [Theory]
    [InlineData(234u, 234)]
    [InlineData(235u, 235)]
    [InlineData(236u, 235)]
    [InlineData(uint.MaxValue, 235)]
    public void ListCount_StopsAtTheNativeBufferEdge(uint declared, int expected)
    {
        byte[] packet = GameMessages.Header(GameMessageBuffer.Capacity, GameItem1009.PacketType);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(20), declared);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(GameMessageBuffer.Capacity - 4), 0x77);

        GameItem1009 item = GameItem1009.Read(GameMessages.Load(packet));

        Assert.Equal((declared, expected), (item.DeclaredListCount, item.ListCount));
        Assert.Equal(0x77u, item.GetListedId(GameItem1009.ListCapacity - 1));
    }

    [Fact]
    public void Accessors_StayInsideTheNativeBuffer()
    {
        GameItem1009 item = GameItem1009.Read(GameMessages.Load(Convert.FromHexString(ListFrame)));
        try
        {
            item.GetListedId(GameItem1009.ListCapacity);
            Assert.Fail("A list index past the native buffer was read.");
        }
        catch (ArgumentOutOfRangeException)
        {
        }

        try
        {
            item.GetSnapshotItemId(GameItem1009.SnapshotItemCount);
            Assert.Fail("A snapshot index past the 13 UIDs was read.");
        }
        catch (ArgumentOutOfRangeException)
        {
        }
    }
}
