using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
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

    [Fact]
    public void Write_MatchesThreeObservedAdmissionFrames()
    {
        using JsonDocument fixture = JsonDocument.Parse(File.ReadAllBytes(
            Path.Combine(AppContext.BaseDirectory, "WireGolden", "1134-mission-catalog.observed.json")));
        JsonElement.ArrayEnumerator frames = fixture.RootElement.GetProperty("frames").EnumerateArray();
        int previous = 0;
        int total = 0;
        int sequence = 13;

        foreach (JsonElement frame in frames)
        {
            Assert.Equal(sequence++, frame.GetProperty("sequence").GetInt32());
            byte[] observed = Convert.FromBase64String(frame.GetProperty("base64").GetString()!);
            Assert.Equal(frame.GetProperty("sha256").GetString()!.ToUpperInvariant(),
                Convert.ToHexString(SHA256.HashData(observed)));

            int count = frame.GetProperty("count").GetInt32();
            Assert.Equal(GameMissionCatalog1134.GetPacketLength(count), observed.Length);
            int[] ids = new int[count];
            for (int index = 0; index < count; index++)
            {
                ReadOnlySpan<byte> record = observed.AsSpan(8 + index * 12, 12);
                int id = BinaryPrimitives.ReadInt32LittleEndian(record);
                Assert.True(id > previous);
                Assert.True(record[4..].IndexOfAnyExcept((byte)0) < 0);
                ids[index] = id;
                previous = id;
            }

            byte[] written = new byte[observed.Length];
            GameMissionCatalog1134.Write(written, ids);
            Assert.Equal(observed, written);
            total += count;
        }

        Assert.Equal(16, sequence);
        Assert.Equal(224, total);
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

        ArgumentOutOfRangeException empty = Assert.Throws<ArgumentOutOfRangeException>(() =>
            GameMissionCatalog1134.Write(destination, []));
        ArgumentOutOfRangeException oversized = Assert.Throws<ArgumentOutOfRangeException>(() =>
            GameMissionCatalog1134.Write(destination, Enumerable.Range(1, 81).ToArray()));
        Assert.Equal("missionIds", empty.ParamName);
        Assert.Equal("missionIds", oversized.ParamName);
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

    [Fact]
    public void Write_RejectsOverlappingInputWithoutWriting()
    {
        byte[] destination = Enumerable.Repeat((byte)0xA5, 32).ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(destination, 600);
        BinaryPrimitives.WriteInt32LittleEndian(destination.AsSpan(4), 601);
        byte[] original = destination.ToArray();

        ArgumentException failure = Assert.Throws<ArgumentException>(() =>
            GameMissionCatalog1134.Write(destination, MemoryMarshal.Cast<byte, int>(destination.AsSpan(0, 8))));

        Assert.Equal("missionIds", failure.ParamName);
        Assert.Equal(original, destination);
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
