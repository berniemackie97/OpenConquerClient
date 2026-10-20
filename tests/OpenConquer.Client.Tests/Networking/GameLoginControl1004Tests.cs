using System.Buffers.Binary;
using OpenConquer.Networking.Game.Login;

namespace OpenConquer.Client.Tests.Networking;

public sealed class GameLoginControl1004Tests
{
    // Independently composed from the server GameChatPacket wire layout:
    // fixed 1004 header, channel 0x835, four length-prefixed ANSI strings.
    private const string AcceptedHex =
        "2600EC0300000000350800000000000000000000000000000400000009414E535745525F4F4B";
    private const string NewRoleHex =
        "2500EC03000000003508000000000000000000000000000004000000084E45575F524F4C45";
    private const string RefusalHex =
        "4800EC030000000035080000000000000000000000000000040000002B546865207365727665722069732066756C6C2E20506C656173652074727920616761696E206C617465722E";
    private const string AnsiRefusalHex =
        "2E00EC0300000000350800000000000000000000000000000400000011536F7272792C20802062616C616E63652E";

    [Fact]
    public void ExistingCharacterControlDoesNotReportWorldAdmission()
    {
        GameLoginControlOutcome result = GameLoginControl1004.Read(Convert.FromHexString(AcceptedHex));

        Assert.Equal(GameLoginControlKind.ExistingCharacter, result.Kind);
        Assert.True(result.RefusalBytes.IsEmpty);
    }

    [Fact]
    public void NewRoleControlHasSeparateRoute()
    {
        GameLoginControlOutcome result = GameLoginControl1004.Read(Convert.FromHexString(NewRoleHex));

        Assert.Equal(GameLoginControlKind.CharacterCreation, result.Kind);
        Assert.True(result.RefusalBytes.IsEmpty);
    }

    [Fact]
    public void OtherLoginChannelTextIsARefusal()
    {
        GameLoginControlOutcome result = GameLoginControl1004.Read(Convert.FromHexString(RefusalHex));

        Assert.Equal(GameLoginControlKind.Refused, result.Kind);
        Assert.Equal("The server is full. Please try again later.",
            System.Text.Encoding.ASCII.GetString(result.RefusalBytes.Span));
    }

    [Fact]
    public void RefusalPreservesOriginalWireBytesWithoutSelectingCodePage()
    {
        GameLoginControlOutcome result = GameLoginControl1004.Read(Convert.FromHexString(AnsiRefusalHex));

        Assert.Equal(GameLoginControlKind.Refused, result.Kind);
        Assert.Equal("536F7272792C20802062616C616E63652E",
            Convert.ToHexString(result.RefusalBytes.Span));
    }

    [Fact]
    public void ControlWordsMatchTheNativeCaseInsensitiveComparison()
    {
        byte[] packet = Convert.FromHexString(AcceptedHex);
        packet[29] = (byte)'a';

        GameLoginControlOutcome result = GameLoginControl1004.Read(packet);

        Assert.Equal(GameLoginControlKind.ExistingCharacter, result.Kind);
        Assert.True(result.RefusalBytes.IsEmpty);
    }

    [Theory]
    [InlineData(0, (byte)0x25)] // Declared length disagrees with available bytes.
    [InlineData(2, (byte)0xED)] // Wrong packet type.
    [InlineData(8, (byte)0x34)] // Creation channel is not a first login outcome.
    [InlineData(25, (byte)16)] // Declared string exceeds the available packet bytes.
    [InlineData(28, (byte)10)] // Message declares more bytes than exist.
    public void InvalidFieldsFailClosed(int index, byte value)
    {
        byte[] packet = Convert.FromHexString(AcceptedHex);
        packet[index] = value;

        Assert.Throws<InvalidDataException>(() => GameLoginControl1004.Read(packet));
    }

    [Theory]
    [InlineData(0, (byte)0x81)]
    [InlineData(1, (byte)0x81)]
    [InlineData(2, (byte)0x81)]
    [InlineData(0, (byte)0x8D)]
    [InlineData(1, (byte)0x8D)]
    [InlineData(2, (byte)0x8D)]
    [InlineData(0, (byte)0x8F)]
    [InlineData(1, (byte)0x8F)]
    [InlineData(2, (byte)0x8F)]
    [InlineData(0, (byte)0x90)]
    [InlineData(1, (byte)0x90)]
    [InlineData(2, (byte)0x90)]
    [InlineData(0, (byte)0x9D)]
    [InlineData(1, (byte)0x9D)]
    [InlineData(2, (byte)0x9D)]
    public void UnusedStringBytesDoNotBlockLoginControl(int fieldIndex, byte value)
    {
        foreach (string fixture in new[] { AcceptedHex, NewRoleHex })
        {
            byte[] packet = WithOneByteStringField(Convert.FromHexString(fixture), fieldIndex, value);

            Assert.Equal(fixture == AcceptedHex
                ? GameLoginControlKind.ExistingCharacter
                : GameLoginControlKind.CharacterCreation,
                GameLoginControl1004.Read(packet).Kind);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void DefinedWindows1252ByteInIgnoredStringPreservesNativeControl(int fieldIndex)
    {
        byte[] packet = WithOneByteStringField(Convert.FromHexString(AcceptedHex), fieldIndex, 0x80);

        GameLoginControlOutcome outcome = GameLoginControl1004.Read(packet);

        Assert.Equal(GameLoginControlKind.ExistingCharacter, outcome.Kind);
        Assert.True(outcome.RefusalBytes.IsEmpty);
    }

    // Compose a fixture with a one-byte sender, recipient or suffix. The
    // fixed server packet layout is independent of the client parser.
    private static byte[] WithOneByteStringField(byte[] packet, int fieldIndex, byte value)
    {
        int lengthOffset = 25 + fieldIndex;
        byte[] result = new byte[packet.Length + 1];

        packet.AsSpan(0, lengthOffset + 1).CopyTo(result);
        result[lengthOffset] = 1;
        result[lengthOffset + 1] = value;
        packet.AsSpan(lengthOffset + 1).CopyTo(result.AsSpan(lengthOffset + 2));
        BinaryPrimitives.WriteUInt16LittleEndian(result, checked((ushort)result.Length));

        return result;
    }

    [Fact]
    public void TruncatedSelectedStringIsRejectedButUnreferencedTrailingDataIsIgnored()
    {
        byte[] original = Convert.FromHexString(AcceptedHex);
        byte[] truncated = original[..^1];
        byte[] extended = [.. original, 0xFF];
        BinaryPrimitives.WriteUInt16LittleEndian(truncated, checked((ushort)truncated.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(extended, checked((ushort)extended.Length));

        Assert.Throws<InvalidDataException>(() => GameLoginControl1004.Read(truncated));
        Assert.Equal(GameLoginControlKind.ExistingCharacter, GameLoginControl1004.Read(extended).Kind);
    }

    [Fact]
    public void NativeReaderSkipsOversizedUnusedEntryWithoutRejectingValidControl()
    {
        byte[] oversizedUnused = new byte[255];
        Array.Fill(oversizedUnused, (byte)0x81);
        byte[] packet = BuildPacket(4, oversizedUnused, [], [], "ANSWER_OK"u8.ToArray());

        Assert.Equal(GameLoginControlKind.ExistingCharacter, GameLoginControl1004.Read(packet).Kind);
    }

    [Fact]
    public void NativeControlWordUsesCStringPrefixEvenWithEmbeddedNul()
    {
        byte[] packet = BuildPacket(4, [], [], [], "ANSWER_OK\0junk"u8.ToArray());

        Assert.Equal(GameLoginControlKind.ExistingCharacter, GameLoginControl1004.Read(packet).Kind);
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)1)]
    [InlineData((byte)2)]
    [InlineData((byte)3)]
    public void MissingControlEntryIsOrdinaryEmptyRefusal(byte count)
    {
        byte[] packet = BuildPacket(count);

        GameLoginControlOutcome result = GameLoginControl1004.Read(packet);

        Assert.Equal(GameLoginControlKind.Refused, result.Kind);
        Assert.True(result.RefusalBytes.IsEmpty);
    }

    [Fact]
    public void AdditionalDeclaredEntriesAndTrailingBytesDoNotChangeControl()
    {
        byte[] packet = BuildPacket(5, [], [], [], "nEw_rOlE"u8.ToArray(), [0xFF]);

        Assert.Equal(GameLoginControlKind.CharacterCreation, GameLoginControl1004.Read(packet).Kind);
    }

    [Fact]
    public void MalformedSelectedLengthCannotReadBeyondReceivedFrame()
    {
        byte[] packet = BuildPacket(4, [], [], [], "ANSWER_OK"u8.ToArray());
        packet[28] = 255;

        Assert.Throws<InvalidDataException>(() => GameLoginControl1004.Read(packet));
    }

    private static byte[] BuildPacket(byte count, params byte[][] strings)
    {
        List<byte> data = new(Convert.FromHexString(AcceptedHex).AsSpan(0, 25).ToArray());
        data[24] = count;
        foreach (byte[] value in strings)
        {
            data.Add(checked((byte)value.Length));
            data.AddRange(value);
        }

        byte[] packet = [.. data];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, checked((ushort)packet.Length));
        return packet;
    }

    [Fact]
    public void BoundsAreEnforcedBeforeAnyFieldRead()
    {
        Assert.Throws<InvalidDataException>(() => GameLoginControl1004.Read([]));
        Assert.Throws<InvalidDataException>(() => GameLoginControl1004.Read(new byte[28]));

        byte[] oversized = new byte[1025];
        BinaryPrimitives.WriteUInt16LittleEndian(oversized, 1025);
        Assert.Throws<InvalidDataException>(() => GameLoginControl1004.Read(oversized));
    }
}
