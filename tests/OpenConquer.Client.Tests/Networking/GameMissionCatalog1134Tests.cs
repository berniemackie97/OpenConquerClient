using System.Buffers.Binary;
using OpenConquer.Networking.Game.Login;

namespace OpenConquer.Client.Tests.Networking;

public sealed class GameMissionCatalog1134Tests
{
    [Fact]
    public void Write_MatchesTheDocumentedTwoRecordLayout()
    {
        byte[] destination = new byte[32];
        GameMissionCatalog1134.Write(destination, [600, 601]);

        Assert.Equal("20006E0403000200580200000000000000000000590200000000000000000000",
            Convert.ToHexString(destination));
    }

    [Theory]
    [InlineData(1, 20)]
    [InlineData(64, 776)]
    [InlineData(80, 968)]
    public void Write_UsesBoundedFramesAndClearsUnusedFields(int count, int length)
    {
        int[] missionIds = Enumerable.Range(600, count).ToArray();
        byte[] destination = Enumerable.Repeat((byte)0xA5, length).ToArray();

        GameMissionCatalog1134.Write(destination, missionIds);

        Assert.Equal(length, GameMissionCatalog1134.GetPacketLength(count));
        Assert.Equal(length, BinaryPrimitives.ReadUInt16LittleEndian(destination));
        Assert.Equal(GameMissionCatalog1134.PacketType, BinaryPrimitives.ReadUInt16LittleEndian(destination.AsSpan(2)));
        Assert.Equal(GameMissionCatalog1134.CatalogSubtype, BinaryPrimitives.ReadUInt16LittleEndian(destination.AsSpan(4)));
        Assert.Equal(count, BinaryPrimitives.ReadUInt16LittleEndian(destination.AsSpan(6)));

        for (int index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> record = destination.AsSpan(8 + index * 12, 12);
            Assert.Equal(missionIds[index], BinaryPrimitives.ReadInt32LittleEndian(record));
            Assert.True(record[4..].IndexOfAnyExcept((byte)0) < 0);
        }
    }

    [Fact]
    public void Write_RejectsEmptyOrOversizedFramesWithoutWriting()
    {
        byte[] destination = Enumerable.Repeat((byte)0xA5, 20).ToArray();

        Assert.Throws<ArgumentOutOfRangeException>(() => GameMissionCatalog1134.Write(destination, []));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            GameMissionCatalog1134.Write(destination, Enumerable.Range(1, 81).ToArray()));
        Assert.All(destination, value => Assert.Equal((byte)0xA5, value));
    }

    [Theory]
    [InlineData(new[] { 0, 2 })]
    [InlineData(new[] { -1, 2 })]
    [InlineData(new[] { 600, 600 })]
    [InlineData(new[] { 601, 600 })]
    public void Write_RejectsInvalidMissionIdsWithoutWriting(int[] missionIds)
    {
        byte[] destination = Enumerable.Repeat((byte)0xA5, 32).ToArray();

        Assert.Throws<ArgumentException>(() => GameMissionCatalog1134.Write(destination, missionIds));
        Assert.All(destination, value => Assert.Equal((byte)0xA5, value));
    }

    [Theory]
    [InlineData(31)]
    [InlineData(33)]
    public void Write_RejectsIncorrectFrameLengthWithoutWriting(int length)
    {
        byte[] destination = Enumerable.Repeat((byte)0xA5, length).ToArray();

        Assert.Throws<ArgumentException>(() => GameMissionCatalog1134.Write(destination, [600, 601]));
        Assert.All(destination, value => Assert.Equal((byte)0xA5, value));
    }
}
