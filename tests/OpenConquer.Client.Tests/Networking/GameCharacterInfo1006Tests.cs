using System.Buffers.Binary;
using OpenConquer.Networking.Game.Login;

namespace OpenConquer.Client.Tests.Networking;

public sealed class GameCharacterInfo1006Tests
{
    // Independently compose the native 110-byte fixed region and a
    // count-prefixed string list; no production serializer is used.
    [Fact]
    public void CharacterInfoPreservesNativeFieldTypesAndRawNames()
    {
        byte[] packet = Packet(3, [0xD6, 0xD0], [], [0x81, 0x40]);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), 0x12345678);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(32), uint.MaxValue);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(44), 0xAA55AA55);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(48), 450_000);
        BinaryPrimitives.WriteInt16LittleEndian(packet.AsSpan(66), -7);
        packet[72] = 0xFE;
        packet[74] = 0xFF;
        packet[97] = 7;
        BinaryPrimitives.WriteUInt64LittleEndian(packet.AsSpan(98), 7_654_321);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(106), 42);

        GameCharacterInfo1006 result = GameCharacterInfo1006.Read(packet);

        Assert.Equal(0x12345678u, result.EntityId);
        Assert.Equal(uint.MaxValue, result.LegacyDeed);
        Assert.Equal(0xAA55AA55u, result.VirtuePoints);
        Assert.Equal(450_000u, result.EncodedPreRebirthLevel);
        Assert.Equal((short)-7, result.PkPoints);
        Assert.Equal((byte)0xFE, result.LegacyNobility);
        Assert.Equal((byte)0xFF, result.LegacyAutoAllot);
        Assert.Equal((byte)7, result.ActiveSubProfessionId);
        Assert.Equal(7_654_321ul, result.PackedSubProfessionPhases);
        Assert.Equal(42u, result.RacePoints);
        Assert.Equal("D6D0", Convert.ToHexString(result.PlayerNameBytes.Span));
        Assert.Equal("8140", Convert.ToHexString(result.SpouseNameBytes.Span));
    }

    [Fact]
    public void IgnoredMiddleEntryLength255DoesNotInvalidateFollowingSpouse()
    {
        byte[] ignored = new byte[255];
        Array.Fill(ignored, (byte)0x81);
        byte[] packet = Packet(3, "Ada"u8.ToArray(), ignored, "Lin"u8.ToArray());

        GameCharacterInfo1006 result = GameCharacterInfo1006.Read(packet);

        Assert.Equal("416461", Convert.ToHexString(result.PlayerNameBytes.Span));
        Assert.Equal("4C696E", Convert.ToHexString(result.SpouseNameBytes.Span));
    }

    [Fact]
    public void OverlongSelectedNameBecomesEmptyWithoutShiftingSpouse()
    {
        byte[] packet = Packet(3, "SixteenCharsHere"u8.ToArray(), [], "Lin"u8.ToArray());

        GameCharacterInfo1006 result = GameCharacterInfo1006.Read(packet);

        Assert.True(result.PlayerNameBytes.IsEmpty);
        Assert.Equal("4C696E", Convert.ToHexString(result.SpouseNameBytes.Span));
    }

    [Fact]
    public void EmbeddedNulEndsNameWithoutInvalidatingPacket()
    {
        byte[] packet = Packet(3, "Ada\0tail"u8.ToArray(), [0x81], "Z\0tail"u8.ToArray());

        GameCharacterInfo1006 result = GameCharacterInfo1006.Read(packet);

        Assert.Equal("416461", Convert.ToHexString(result.PlayerNameBytes.Span));
        Assert.Equal("5A", Convert.ToHexString(result.SpouseNameBytes.Span));
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)1)]
    [InlineData((byte)2)]
    public void MissingStringEntriesBecomeEmptyWithoutBlockingFixedFields(byte count)
    {
        byte[] packet = Packet(count, "Ada"u8.ToArray());
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), 777);

        GameCharacterInfo1006 result = GameCharacterInfo1006.Read(packet);

        Assert.Equal(777u, result.EntityId);
        if (count == 0)
        {
            Assert.True(result.PlayerNameBytes.IsEmpty);
        }

        Assert.True(result.SpouseNameBytes.IsEmpty);
    }

    [Fact]
    public void ExtraStringEntriesAndTrailingDataAreNotReinterpretedAsFields()
    {
        byte[] packet = Packet(5, "Ada"u8.ToArray(), [], "Lin"u8.ToArray(),
            [0x81, 0x90, 0x9D], [0xFF]);

        GameCharacterInfo1006 result = GameCharacterInfo1006.Read(packet);

        Assert.Equal("416461", Convert.ToHexString(result.PlayerNameBytes.Span));
        Assert.Equal("4C696E", Convert.ToHexString(result.SpouseNameBytes.Span));
    }

    [Fact]
    public void TruncatedFixedBodyReadsConstructorZeroedNativeStorage()
    {
        byte[] packet = Convert.FromHexString("0400EE03");
        GameCharacterInfo1006 result = GameCharacterInfo1006.Read(packet);

        Assert.Equal(0u, result.EntityId);
        Assert.Equal((short)0, result.PkPoints);
        Assert.True(result.PlayerNameBytes.IsEmpty);
        Assert.True(result.SpouseNameBytes.IsEmpty);
    }

    [Fact]
    public void TruncatedSelectedNameUsesZeroFilledSuffixWithinBoundedStorage()
    {
        byte[] packet = new byte[114];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, (ushort)packet.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), 1006);
        packet[110] = 3;
        packet[111] = 9;
        packet[112] = (byte)'A';
        packet[113] = (byte)'B';

        GameCharacterInfo1006 result = GameCharacterInfo1006.Read(packet);

        Assert.Equal("4142", Convert.ToHexString(result.PlayerNameBytes.Span));
        Assert.True(result.SpouseNameBytes.IsEmpty);
    }

    [Fact]
    public void NonzeroEntityIdIsNotRequiredAtTheSnapshotStage()
    {
        byte[] packet = Packet(3, [], [], []);

        GameCharacterInfo1006 result = GameCharacterInfo1006.Read(packet);

        Assert.Equal(0u, result.EntityId);
        Assert.True(result.PlayerNameBytes.IsEmpty);
    }

    [Fact]
    public void InvalidHeaderTypeAndOversizedStorageFailBeforeSnapshot()
    {
        Assert.Throws<InvalidDataException>(() => GameCharacterInfo1006.Read([]));
        byte[] wrongType = Convert.FromHexString("0400EF03");
        Assert.Throws<InvalidDataException>(() => GameCharacterInfo1006.Read(wrongType));
        byte[] wrongLength = Convert.FromHexString("0500EE03");
        Assert.Throws<InvalidDataException>(() => GameCharacterInfo1006.Read(wrongLength));

        byte[] oversized = new byte[1025];
        BinaryPrimitives.WriteUInt16LittleEndian(oversized, 1025);
        BinaryPrimitives.WriteUInt16LittleEndian(oversized.AsSpan(2), 1006);
        Assert.Throws<InvalidDataException>(() => GameCharacterInfo1006.Read(oversized));
    }

    private static byte[] Packet(byte count, params byte[][] entries)
    {
        int length = 111;
        foreach (byte[] entry in entries)
        {
            length += 1 + entry.Length;
        }

        byte[] packet = new byte[length];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, checked((ushort)length));
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), 1006);
        packet[110] = count;
        int offset = 111;
        foreach (byte[] entry in entries)
        {
            packet[offset++] = checked((byte)entry.Length);
            entry.AsSpan().CopyTo(packet.AsSpan(offset));
            offset += entry.Length;
        }

        return packet;
    }
}
