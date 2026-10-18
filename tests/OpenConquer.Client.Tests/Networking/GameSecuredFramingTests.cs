using System.Buffers.Binary;
using OpenConquer.Networking.Game.Cryptography;
using OpenConquer.Networking.Game.Framing;

namespace OpenConquer.Client.Tests.Networking;

public sealed class GameSecuredFramingTests
{
    // Independently generated with CAST5-CFB64 (Python cryptography),
    // matching the verified server-side 1052 fixture and DH test secret.
    private const string PacketHex =
        "1C001C0444332211EFCDAB897C00456E001122AABBCC00008D150000";
    private const string ClientWireHex =
        "96064FAD594BD0011BE0CDCA35D411CEDB3F8900FCA7AE811A0ECFEC2A8ABDCAF384E99E";
    private const string ServerWireHex =
        "5F6D0A696917876D91A9D89F37B1411E4845CE4144C1A5E9091FAAA561682915AD9377F4";
    private const string TwoServerFramesHex =
        "5F6D0A696917876D91A9D89F37B1411E4845CE4144C1A5E9091FAAA561682915AD9377F46BF3A240EC10ECA3CB64F14229C698EAC735D1F084952976EFB05494E8224596201E2450";
    private const string TwoClientFramesHex =
        "96064FAD594BD0011BE0CDCA35D411CEDB3F8900FCA7AE811A0ECFEC2A8ABDCAF384E99EDC42B7A2246EC66358C07469427E6B2C57B011AA7A24324686CBD1013DFA144FF71C34C5";

    private static byte[] SessionKey()
    {
        byte[] bytes = new byte[64];
        bytes[0] = 2;
        bytes[1] = 0x71;
        return bytes;
    }

    private static byte[] InboundIv() => [1, 2, 3, 4, 5, 6, 7, 8];

    private static byte[] OutboundIv() => [21, 22, 23, 24, 25, 26, 27, 28];

    private static GameSessionCipher ClientSession() => new(SessionKey(), InboundIv(), OutboundIv());

    [Fact]
    public void ClientProofMatchesIndependentEncryptedWireVector()
    {
        byte[] packet = Convert.FromHexString(PacketHex);
        byte[] output = new byte[GameSecuredFrameProtocol.MaximumWireLength];
        using GameSessionCipher session = ClientSession();
        using GameSecuredFrameEncoder encoder = new(session);

        Assert.Equal(36, GameSecuredFrameProtocol.GetWireLength(packet));
        int written = encoder.Write(packet, output);

        Assert.Equal(36, written);
        Assert.Equal(ClientWireHex, Convert.ToHexString(output.AsSpan(0, written)));
        Assert.All(output.AsSpan(written).ToArray(), value => Assert.Equal((byte)0, value));
    }

    [Fact]
    public void ConsecutiveOutboundFramesUseOneContinuousCipherStream()
    {
        byte[] packet = Convert.FromHexString(PacketHex);
        byte[] output = new byte[72];
        using GameSessionCipher session = ClientSession();
        using GameSecuredFrameEncoder encoder = new(session);

        int first = encoder.Write(packet, output.AsSpan(0, 36));
        int second = encoder.Write(packet, output.AsSpan(36));

        Assert.Equal(36, first);
        Assert.Equal(36, second);
        Assert.Equal(TwoClientFramesHex, Convert.ToHexString(output));
    }

    [Fact]
    public void PacketSourceMayBeTheSameMemoryAsOutputFrame()
    {
        byte[] buffer = new byte[36];
        Convert.FromHexString(PacketHex).CopyTo(buffer, 0);
        using GameSessionCipher session = ClientSession();
        using GameSecuredFrameEncoder encoder = new(session);

        Assert.Equal(36, encoder.Write(buffer.AsSpan(0, 28), buffer));
        Assert.Equal(ClientWireHex, Convert.ToHexString(buffer));
    }

    [Fact]
    public void InvalidPacketAndDestinationFailBeforeCipherStateOrOutputChanges()
    {
        byte[] packet = Convert.FromHexString(PacketHex);
        byte[] malformed = packet.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(malformed, 27);
        byte[] oversized = Enumerable.Repeat((byte)0xA5, 36).ToArray();
        using GameSessionCipher session = ClientSession();
        using GameSecuredFrameEncoder encoder = new(session);

        Assert.Throws<ArgumentException>(() => GameSecuredFrameProtocol.GetWireLength(malformed));
        Assert.Throws<ArgumentException>(() => encoder.Write(malformed, oversized));
        Assert.Throws<ArgumentException>(() => encoder.Write(packet, new byte[35]));
        Assert.Throws<ArgumentException>(() => GameSecuredFrameProtocol.GetWireLength(new byte[3]));
        Assert.All(oversized, b => Assert.Equal((byte)0xA5, b));

        byte[] encoded = new byte[36];
        encoder.Write(packet, encoded);
        Assert.Equal(ClientWireHex, Convert.ToHexString(encoded));
    }

    [Fact]
    public void DecoderReadsReferenceServerFrameAndOwnsPacketUntilDisposal()
    {
        byte[] encrypted = Convert.FromHexString(ServerWireHex);
        using GameSessionCipher session = ClientSession();
        using GameSecuredFrameDecoder decoder = new(session);

        GameSecuredFrameDecodeResult result = decoder.Decode(encrypted);
        Assert.Equal(GameSecuredFrameDecodeStatus.Success, result.Status);
        Assert.Equal(36, result.BytesConsumed);
        Assert.NotNull(result.Frame);
        using GameSecuredInboundFrame frame = result.Frame;
        Assert.Equal(28, frame.PacketLength);
        Assert.Equal(1052, frame.PacketType);
        Assert.Equal(PacketHex, Convert.ToHexString(frame.Packet.Span));
        Assert.Equal(PacketHex[8..], Convert.ToHexString(frame.Payload.Span));
        frame.Dispose();
        Assert.Throws<ObjectDisposedException>(() => _ = frame.Packet);
    }

    [Fact]
    public void BytewiseFragmentationDoesNotLeakPartialFrameOrAdvanceTooFar()
    {
        byte[] encrypted = Convert.FromHexString(ServerWireHex);
        using GameSessionCipher session = ClientSession();
        using GameSecuredFrameDecoder decoder = new(session);

        Assert.Equal(GameSecuredFrameDecodeStatus.NeedMoreData, decoder.Decode([]).Status);
        for (int index = 0; index < encrypted.Length; index++)
        {
            GameSecuredFrameDecodeResult result = decoder.Decode(encrypted.AsSpan(index, 1));
            Assert.Equal(1, result.BytesConsumed);
            if (index != encrypted.Length - 1)
            {
                Assert.Equal(GameSecuredFrameDecodeStatus.NeedMoreData, result.Status);
                Assert.Null(result.Frame);
            }
            else
            {
                Assert.Equal(GameSecuredFrameDecodeStatus.Success, result.Status);
                using GameSecuredInboundFrame frame = Assert.IsType<GameSecuredInboundFrame>(result.Frame);
                Assert.Equal(PacketHex, Convert.ToHexString(frame.Packet.Span));
            }
        }
    }

    [Fact]
    public void DecoderStopsAtFirstFrameAndRetainsStateForSecondFrame()
    {
        byte[] encrypted = Convert.FromHexString(TwoServerFramesHex);
        using GameSessionCipher session = ClientSession();
        using GameSecuredFrameDecoder decoder = new(session);

        GameSecuredFrameDecodeResult first = decoder.Decode(encrypted);
        Assert.Equal(GameSecuredFrameDecodeStatus.Success, first.Status);
        Assert.Equal(36, first.BytesConsumed);
        using GameSecuredInboundFrame firstFrame = Assert.IsType<GameSecuredInboundFrame>(first.Frame);

        GameSecuredFrameDecodeResult second = decoder.Decode(encrypted.AsSpan(first.BytesConsumed));
        Assert.Equal(GameSecuredFrameDecodeStatus.Success, second.Status);
        Assert.Equal(36, second.BytesConsumed);
        using GameSecuredInboundFrame secondFrame = Assert.IsType<GameSecuredInboundFrame>(second.Frame);

        Assert.Equal(PacketHex, Convert.ToHexString(firstFrame.Packet.Span));
        Assert.Equal(PacketHex, Convert.ToHexString(secondFrame.Packet.Span));
    }

    [Theory]
    [InlineData("406D0A696917876D3EDCA5369910180AE89522E1093F37C8479AFD61158E73F8A9C224BD")]
    [InlineData("42680A696917876D6D6E6A1C95F412B8BC5E5282125675C8E70DB472D3C81D2BAF286236")]
    public void InvalidEncryptedPacketLengthFailsClosedAfterFourBytes(string encryptedHex)
    {
        using GameSessionCipher session = ClientSession();
        using GameSecuredFrameDecoder decoder = new(session);

        GameSecuredFrameDecodeResult result = decoder.Decode(Convert.FromHexString(encryptedHex));
        Assert.Equal(GameSecuredFrameDecodeStatus.InvalidPacketLength, result.Status);
        Assert.Equal(4, result.BytesConsumed);
        Assert.Null(result.Frame);
        Assert.Throws<InvalidOperationException>(() => decoder.Decode(Convert.FromHexString(ServerWireHex)));
    }

    [Fact]
    public void InvalidEncryptedSignatureRejectsEntireFrameAndTerminatesDecoder()
    {
        byte[] invalid = Convert.FromHexString(
            "5F6D0A696917876D91A9D89F37B1411E4845CE4144C1A5E9091FAAA561682915BE9377F4");
        using GameSessionCipher session = ClientSession();
        using GameSecuredFrameDecoder decoder = new(session);

        GameSecuredFrameDecodeResult result = decoder.Decode(invalid);
        Assert.Equal(GameSecuredFrameDecodeStatus.InvalidSignature, result.Status);
        Assert.Equal(36, result.BytesConsumed);
        Assert.Null(result.Frame);
        Assert.Throws<InvalidOperationException>(() => decoder.Decode([]));
    }

    [Fact]
    public void MinimumAndMaximumPacketLengthsAreExactAndBounded()
    {
        byte[] minimum = [4, 0, 0x1C, 4];
        byte[] maximum = new byte[GameSecuredFrameProtocol.MaximumPacketLength];
        BinaryPrimitives.WriteUInt16LittleEndian(maximum, (ushort)maximum.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(maximum.AsSpan(2), 1052);

        Assert.Equal(12, GameSecuredFrameProtocol.GetWireLength(minimum));
        Assert.Equal(1032, GameSecuredFrameProtocol.GetWireLength(maximum));

        // Independently encrypted synthetic server frames exercise protocol bounds.
        using GameSessionCipher server = new(SessionKey(), OutboundIv(), InboundIv());
        using GameSessionCipher client = ClientSession();
        using GameSecuredFrameDecoder decoder = new(client);

        byte[] first = new byte[12];
        minimum.CopyTo(first, 0);
        "TQServer"u8.CopyTo(first.AsSpan(minimum.Length));
        server.EncryptOutbound(first, first);
        GameSecuredFrameDecodeResult minResult = decoder.Decode(first);
        Assert.Equal(GameSecuredFrameDecodeStatus.Success, minResult.Status);
        using GameSecuredInboundFrame minFrame = Assert.IsType<GameSecuredInboundFrame>(minResult.Frame);
        Assert.Equal(4, minFrame.PacketLength);
        Assert.Empty(minFrame.Payload.ToArray());

        byte[] last = new byte[1032];
        maximum.CopyTo(last, 0);
        "TQServer"u8.CopyTo(last.AsSpan(maximum.Length));
        server.EncryptOutbound(last, last);
        GameSecuredFrameDecodeResult maxResult = decoder.Decode(last);
        Assert.Equal(GameSecuredFrameDecodeStatus.Success, maxResult.Status);
        Assert.Equal(1032, maxResult.BytesConsumed);
        using GameSecuredInboundFrame maxFrame = Assert.IsType<GameSecuredInboundFrame>(maxResult.Frame);
        Assert.Equal(1024, maxFrame.PacketLength);

        byte[] tooLong = new byte[1025];
        BinaryPrimitives.WriteUInt16LittleEndian(tooLong, (ushort)tooLong.Length);
        Assert.Throws<ArgumentException>(() => GameSecuredFrameProtocol.GetWireLength(tooLong));
    }

    [Fact]
    public void DisposalIsIdempotentAndBorrowedCipherRemainsCallerOwned()
    {
        using GameSessionCipher session = ClientSession();
        GameSecuredFrameDecoder decoder = new(session);
        GameSecuredFrameEncoder encoder = new(session);
        decoder.Decode(Convert.FromHexString(ServerWireHex).AsSpan(0, 2));
        decoder.Dispose();
        decoder.Dispose();
        encoder.Dispose();
        encoder.Dispose();

        Assert.Throws<ObjectDisposedException>(() => decoder.Decode([]));
        Assert.Throws<ObjectDisposedException>(() =>
            encoder.Write(Convert.FromHexString(PacketHex), new byte[36]));

        // Framing codecs borrow the session and never implicitly dispose it.
        byte[] packet = Convert.FromHexString(PacketHex);
        byte[] encrypted = new byte[packet.Length];
        session.EncryptOutbound(packet, encrypted);
        Assert.NotEqual(packet, encrypted);
    }

    [Fact]
    public void SessionCipherFailureTerminatesEncoderAndClearsStagedWire()
    {
        GameSessionCipher session = ClientSession();
        using GameSecuredFrameEncoder encoder = new(session);
        session.Dispose();
        byte[] wire = Enumerable.Repeat((byte)0xA5, 36).ToArray();

        Assert.Throws<ObjectDisposedException>(() =>
            encoder.Write(Convert.FromHexString(PacketHex), wire));
        Assert.All(wire, value => Assert.Equal((byte)0, value));
        Assert.Throws<InvalidOperationException>(() =>
            encoder.Write(Convert.FromHexString(PacketHex), new byte[36]));
    }
}
