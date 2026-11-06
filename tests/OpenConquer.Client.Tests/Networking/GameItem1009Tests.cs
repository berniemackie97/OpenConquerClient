using System.Buffers.Binary;
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
        Assert.True(GameItem1009.TryRead(Convert.FromHexString(SnapshotFrame), out GameItem1009 item));
        Assert.Equal((0x0a0b0c0du, 0x01020304u, GameItem1009.EquipmentSnapshot), (item.ParameterA, item.ParameterB, item.Subtype));
        Assert.True(item.HasListLayout);
        for (int i = 0; i < GameItem1009.SnapshotItemCount; i++)
        {
            Assert.Equal(0x1000u + (uint)i, item.GetSnapshotItemId(i));
        }
    }

    [Fact]
    public void List_ReadsTheDeclaredCountAndEntriesAfterTheFixedBody()
    {
        Assert.True(GameItem1009.TryRead(Convert.FromHexString(ListFrame), out GameItem1009 item));
        Assert.Equal((GameItem1009.EquipItem, 3u, 3), (item.Subtype, item.DeclaredListCount, item.ListCapacity));
        Assert.Equal([0x2000u, 0x2001u, 0x2002u], [item.GetListedId(0), item.GetListedId(1), item.GetListedId(2)]);
    }

    [Fact]
    public void Accessors_StayInsideTheFrame()
    {
        Assert.True(GameItem1009.TryRead(Convert.FromHexString(ListFrame), out GameItem1009 item));
        try
        {
            item.GetListedId(3);
            Assert.Fail("A list index past the frame was read.");
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

    [Theory]
    [InlineData(14, false, 0)]
    [InlineData(87, false, 0)]
    [InlineData(88, true, 1)]
    [InlineData(90, false, 0)]
    [InlineData(92, true, 2)]
    public void ListLayout_RequiresTheFixedBodyAndWholeEntries(int length, bool hasList, int capacity)
    {
        Assert.True(GameItem1009.TryRead(Frame(length), out GameItem1009 item));
        Assert.Equal((hasList, capacity), (item.HasListLayout, item.ListCapacity));
    }

    [Fact]
    public void TryRead_RejectsFramesTooShortForTheSubtypeOrWithAMismatchedHeader()
    {
        Assert.False(GameItem1009.TryRead(Frame(13), out _));
        byte[] packet = Frame(88);
        BinaryPrimitives.WriteUInt16LittleEndian(packet, 89);
        Assert.False(GameItem1009.TryRead(packet, out _));
    }

    private static byte[] Frame(int length)
    {
        byte[] packet = new byte[length];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, (ushort)length);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), GameItem1009.PacketType);
        return packet;
    }
}
