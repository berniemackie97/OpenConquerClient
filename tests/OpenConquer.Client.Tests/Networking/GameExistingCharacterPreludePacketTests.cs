using System.Buffers.Binary;
using OpenConquer.Networking.Game.Login;

namespace OpenConquer.Client.Tests.Networking;

public sealed class GameExistingCharacterPreludePacketTests
{
    // Independently constructed from verified native field offsets and the
    // companion server's current zero-history/state-0 policy.
    private const string EmptyHistoryHex = "0A001E08000000000000";
    private const string ZeroStateHex = "08001F0800000000";

    [Fact]
    public void NativeCompactHistoryAndCurrentServerStateAreAccepted()
    {
        GameLoginHistory2078 history = GameLoginHistory2078.Read(
            Convert.FromHexString(EmptyHistoryHex));
        GameServerState2079 state = GameServerState2079.Read(
            Convert.FromHexString(ZeroStateHex));

        Assert.Equal(0u, history.LastLoginTimestamp);
        Assert.Equal((byte)0, history.LocationWarningFlag);
        Assert.Equal(string.Empty, history.LastLoginLocation);
        Assert.Equal(0u, state.Value);
        Assert.Equal((ushort)2000, state.SelectedUiState);
    }

    [Fact]
    public void AccountHistoryPreservesNonzeroValuesAndDecodesWindows1252()
    {
        byte[] history = Convert.FromHexString("0E001E08BA1D594E02" + "436166E900");
        GameLoginHistory2078 result = GameLoginHistory2078.Read(history);

        Assert.Equal(0x4E591DBAu, result.LastLoginTimestamp);
        Assert.Equal((byte)2, result.LocationWarningFlag);
        Assert.Equal("Café", result.LastLoginLocation);
    }

    [Fact]
    public void NativeCompatiblePaddedHistoryIsNotMisreadAsSeedData()
    {
        byte[] history = new byte[264];
        BinaryPrimitives.WriteUInt16LittleEndian(history, (ushort)history.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(history.AsSpan(2), 2078);
        BinaryPrimitives.WriteUInt32LittleEndian(history.AsSpan(4), uint.MaxValue);
        history[8] = byte.MaxValue;
        history[9] = (byte)'X';
        history[10] = 0;
        history[11] = 0xFF; // Opaque trailing native-compatible padding.

        GameLoginHistory2078 result = GameLoginHistory2078.Read(history);

        Assert.Equal(uint.MaxValue, result.LastLoginTimestamp);
        Assert.Equal(byte.MaxValue, result.LocationWarningFlag);
        Assert.Equal("X", result.LastLoginLocation);
    }

    [Theory]
    [InlineData((byte)0x81)]
    [InlineData((byte)0x8D)]
    [InlineData((byte)0x8F)]
    [InlineData((byte)0x90)]
    [InlineData((byte)0x9D)]
    public void UndefinedAnsiByteInHistoryLocationIsRejected(byte invalid)
    {
        byte[] history = Convert.FromHexString(EmptyHistoryHex);
        history[9] = invalid; // Removes the only NUL; add a terminator.
        Array.Resize(ref history, history.Length + 1);
        BinaryPrimitives.WriteUInt16LittleEndian(history, (ushort)history.Length);

        Assert.Throws<InvalidDataException>(() => GameLoginHistory2078.Read(history));
    }

    [Fact]
    public void MissingHistoryTerminatorIsRejected()
    {
        byte[] packet = Convert.FromHexString(EmptyHistoryHex);
        packet[9] = (byte)'X';
        Assert.Throws<InvalidDataException>(() => GameLoginHistory2078.Read(packet));
    }

    [Fact]
    public void HistoryRejectsWrongTypeSizeAndOversizedFrames()
    {
        byte[] wrongType = Convert.FromHexString(EmptyHistoryHex);
        wrongType[2]++;
        Assert.Throws<InvalidDataException>(() => GameLoginHistory2078.Read(wrongType));

        byte[] wrongSize = Convert.FromHexString(EmptyHistoryHex);
        wrongSize[0]--;
        Assert.Throws<InvalidDataException>(() => GameLoginHistory2078.Read(wrongSize));

        Assert.Throws<InvalidDataException>(() =>
            GameLoginHistory2078.Read(Convert.FromHexString(EmptyHistoryHex)[..^1]));

        byte[] oversized = new byte[1025];
        BinaryPrimitives.WriteUInt16LittleEndian(oversized, (ushort)oversized.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(oversized.AsSpan(2), 2078);
        Assert.Throws<InvalidDataException>(() => GameLoginHistory2078.Read(oversized));
    }

    [Theory]
    [InlineData(0u, (ushort)2000)]
    [InlineData(1u, (ushort)2001)]
    public void KnownServerStateValuesSelectVerifiedNativeUiState(uint wire, ushort expected)
    {
        byte[] packet = Convert.FromHexString(ZeroStateHex);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), wire);
        GameServerState2079 state = GameServerState2079.Read(packet);

        Assert.Equal(wire, state.Value);
        Assert.Equal(expected, state.SelectedUiState);
    }

    [Theory]
    [InlineData(2u)]
    [InlineData(2000u)]
    [InlineData(uint.MaxValue)]
    public void UnknownServerStateValuesRemainNoOpObservations(uint wire)
    {
        byte[] packet = Convert.FromHexString(ZeroStateHex);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), wire);
        GameServerState2079 state = GameServerState2079.Read(packet);

        Assert.Equal(wire, state.Value);
        Assert.Null(state.SelectedUiState);
    }

    [Fact]
    public void Native2079IgnoresBoundedExtraBytes()
    {
        byte[] padded = Convert.FromHexString("0C001F080100000078563412");
        GameServerState2079 state = GameServerState2079.Read(padded);

        Assert.Equal(1u, state.Value);
        Assert.Equal((ushort)2001, state.SelectedUiState);
    }

    [Fact]
    public void ServerStateRejectsTruncatedWrongTypeLengthAndOversizedFrames()
    {
        Assert.Throws<InvalidDataException>(() =>
            GameServerState2079.Read(Convert.FromHexString("07001F08000000")));

        byte[] wrongType = Convert.FromHexString(ZeroStateHex);
        wrongType[2]++;
        Assert.Throws<InvalidDataException>(() => GameServerState2079.Read(wrongType));

        byte[] wrongLength = Convert.FromHexString(ZeroStateHex);
        wrongLength[0]++;
        Assert.Throws<InvalidDataException>(() => GameServerState2079.Read(wrongLength));

        byte[] oversized = new byte[1025];
        BinaryPrimitives.WriteUInt16LittleEndian(oversized, (ushort)oversized.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(oversized.AsSpan(2), 2079);
        Assert.Throws<InvalidDataException>(() => GameServerState2079.Read(oversized));
    }
}
