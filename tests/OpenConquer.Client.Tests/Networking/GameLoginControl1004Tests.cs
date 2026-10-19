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
        Assert.Null(result.RefusalText);
    }

    [Fact]
    public void NewRoleControlHasSeparateRoute()
    {
        GameLoginControlOutcome result = GameLoginControl1004.Read(Convert.FromHexString(NewRoleHex));

        Assert.Equal(GameLoginControlKind.CharacterCreation, result.Kind);
        Assert.Null(result.RefusalText);
    }

    [Fact]
    public void OtherLoginChannelTextIsARefusal()
    {
        GameLoginControlOutcome result = GameLoginControl1004.Read(Convert.FromHexString(RefusalHex));

        Assert.Equal(GameLoginControlKind.Refused, result.Kind);
        Assert.Equal("The server is full. Please try again later.", result.RefusalText);
    }

    [Fact]
    public void RefusalTextUsesServerWindows1252Encoding()
    {
        GameLoginControlOutcome result = GameLoginControl1004.Read(Convert.FromHexString(AnsiRefusalHex));

        Assert.Equal(GameLoginControlKind.Refused, result.Kind);
        Assert.Equal("Sorry, € balance.", result.RefusalText);
    }

    [Fact]
    public void ControlWordsAreExactAndCaseSensitive()
    {
        byte[] packet = Convert.FromHexString(AcceptedHex);
        packet[29] = (byte)'a';

        GameLoginControlOutcome result = GameLoginControl1004.Read(packet);

        Assert.Equal(GameLoginControlKind.Refused, result.Kind);
        Assert.Equal("aNSWER_OK", result.RefusalText);
    }

    [Theory]
    [InlineData(0, (byte)0x25)] // Declared length disagrees with available bytes.
    [InlineData(2, (byte)0xED)] // Wrong packet type.
    [InlineData(8, (byte)0x34)] // Creation channel is not a first login outcome.
    [InlineData(24, (byte)3)]  // Wrong string count.
    [InlineData(25, (byte)16)] // Native name buffer cannot hold this entry.
    [InlineData(28, (byte)10)] // Message declares more bytes than exist.
    [InlineData(30, (byte)0)] // Embedded NUL is not a valid control string.
    [InlineData(30, (byte)0x81)] // Undefined Windows-1252 byte.
    public void InvalidFieldsFailClosed(int index, byte value)
    {
        byte[] packet = Convert.FromHexString(AcceptedHex);
        packet[index] = value;

        Assert.Throws<InvalidDataException>(() => GameLoginControl1004.Read(packet));
    }

    [Fact]
    public void TruncatedOrExtendedStringListIsRejectedEvenWithConsistentHeader()
    {
        byte[] original = Convert.FromHexString(AcceptedHex);
        byte[] truncated = original[..^1];
        byte[] extended = [.. original, 0xFF];
        BinaryPrimitives.WriteUInt16LittleEndian(truncated, checked((ushort)truncated.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(extended, checked((ushort)extended.Length));

        Assert.Throws<InvalidDataException>(() => GameLoginControl1004.Read(truncated));
        Assert.Throws<InvalidDataException>(() => GameLoginControl1004.Read(extended));
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
