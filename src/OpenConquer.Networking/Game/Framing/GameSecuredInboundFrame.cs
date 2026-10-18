using System.Buffers;

namespace OpenConquer.Networking.Game.Framing;

/// <summary>
/// Owns one decrypted GameServer packet buffer. Borrowed packet/payload memory
/// remains valid only until disposal; the owner must dispose after processing.
/// </summary>
internal sealed class GameSecuredInboundFrame : IDisposable
{
    private byte[]? _buffer;

    internal GameSecuredInboundFrame(byte[] buffer, int packetLength)
    {
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        PacketLength = packetLength;
    }

    internal int PacketLength
    {
        get;
    }

    internal ushort PacketType => System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(Packet.Span[2..]);

    internal ReadOnlyMemory<byte> Packet =>
        (_buffer ?? throw new ObjectDisposedException(nameof(GameSecuredInboundFrame))).AsMemory(0, PacketLength);

    internal ReadOnlyMemory<byte> Payload => Packet[GameSecuredFrameProtocol.HeaderLength..];

    public void Dispose()
    {
        byte[]? buffer = Interlocked.Exchange(ref _buffer, null);
        if (buffer is not null)
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }
}
