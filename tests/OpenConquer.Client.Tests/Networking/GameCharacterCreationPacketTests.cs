using System.Buffers.Binary;
using System.Text;
using OpenConquer.Networking.Game.Login;

namespace OpenConquer.Client.Tests.Networking;

public sealed class GameCharacterCreationPacketTests
{
    [Fact]
    public void CreateRequestMatchesTheNativeBuilderLayout()
    {
        byte[] packet = Enumerable.Repeat((byte)0xA5, GameCharacterCreation1001.PacketLength).ToArray();

        GameCharacterCreation1001.WriteCreate(packet, "Hero"u8, 2002, 100, 0x11223344);

        // CMsgCreatePlayer1001__Build 0x7200BF: size 124, type 1001, cancel 0, name at +24, body +72,
        // profession +74, session +76, MAC text +80 left empty, everything else zero.
        Assert.Equal(124, BinaryPrimitives.ReadUInt16LittleEndian(packet));
        Assert.Equal(1001, BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(2)));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(4)));
        Assert.All(packet[8..24], value => Assert.Equal(0, value));
        Assert.Equal("Hero"u8.ToArray(), packet[24..28]);
        Assert.All(packet[28..72], value => Assert.Equal(0, value));
        Assert.Equal(2002, BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(72)));
        Assert.Equal(100, BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(74)));
        Assert.Equal(0x11223344u, BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(76)));
        Assert.All(packet[80..], value => Assert.Equal(0, value));
    }

    [Fact]
    public void CancelCarriesOnlyTheFlagAndSessionToken()
    {
        byte[] packet = new byte[GameCharacterCreation1001.PacketLength];

        GameCharacterCreation1001.WriteCancel(packet, 0x11223344);

        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(4)));
        Assert.All(packet[8..76], value => Assert.Equal(0, value));
        Assert.Equal(0x11223344u, BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(76)));
        Assert.All(packet[80..], value => Assert.Equal(0, value));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    public void CreateRejectsNamesOutsideTheNativeLimit(int length)
    {
        Assert.Throws<ArgumentException>(() =>
            GameCharacterCreation1001.WriteCreate(new byte[124], new byte[length], 1003, 10, 1));
    }

    [Fact]
    public void CreateRejectsEmbeddedNulZeroTokenAndWrongBuffer()
    {
        Assert.Throws<ArgumentException>(() => GameCharacterCreation1001.WriteCreate(new byte[124], "He\0ro"u8, 1003, 10, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => GameCharacterCreation1001.WriteCreate(new byte[124], "Hero"u8, 1003, 10, 0));
        Assert.Throws<ArgumentException>(() => GameCharacterCreation1001.WriteCreate(new byte[123], "Hero"u8, 1003, 10, 1));
    }

    [Theory]
    [InlineData("ANSWER_OK")]
    [InlineData("answer_ok")]
    public void CreationChannelAnswerOkMeansCreated(string text)
    {
        GameCharacterCreationOutcome outcome = GameCharacterCreationResult1004.Read(Talk(0x834, text));

        Assert.Equal(GameCharacterCreationKind.Created, outcome.Kind);
        Assert.True(outcome.RefusalBytes.IsEmpty);
    }

    [Fact]
    public void OtherCreationChannelTextIsAnOwnedRefusal()
    {
        byte[] packet = Talk(0x834, "Invalid character name.");

        GameCharacterCreationOutcome outcome = GameCharacterCreationResult1004.Read(packet);
        packet.AsSpan().Clear();

        Assert.Equal(GameCharacterCreationKind.Refused, outcome.Kind);
        Assert.Equal("Invalid character name.", Encoding.ASCII.GetString(outcome.RefusalBytes.Span));
    }

    [Fact]
    public void LoginChannelTalkIsNotACreationResult()
    {
        Assert.Throws<InvalidDataException>(() => GameCharacterCreationResult1004.Read(Talk(0x835, "ANSWER_OK")));
    }

    [Fact]
    public void MissingMessageEntryIsAnEmptyRefusal()
    {
        byte[] packet = Talk(0x834, "ANSWER_OK");
        packet[24] = 3;

        GameCharacterCreationOutcome outcome = GameCharacterCreationResult1004.Read(packet);

        Assert.Equal(GameCharacterCreationKind.Refused, outcome.Kind);
        Assert.True(outcome.RefusalBytes.IsEmpty);
    }

    private static byte[] Talk(ushort channel, string message)
    {
        byte[] text = Encoding.ASCII.GetBytes(message);
        byte[] packet = new byte[25 + 3 + 1 + text.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, (ushort)packet.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), 1004);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(8), channel);
        packet[24] = 4;
        packet[28] = (byte)text.Length;
        text.CopyTo(packet, 29);
        return packet;
    }
}
