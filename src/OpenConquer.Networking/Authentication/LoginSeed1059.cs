using System.Buffers.Binary;

namespace OpenConquer.Networking.Authentication;

/// <summary>Reads the verified seed field of a completely received, already-decoded 5517 type-1059 frame.</summary>
internal static class LoginSeed1059
{
    internal const ushort PacketType = 1059;
    internal const int MinimumFrameLength = 8;
    internal const int MaximumFrameLength = 64;

    /// <summary>Only the seed at offset 4 is confirmed, so any fields after it are ignored.</summary>
    internal static bool TryReadSeed(ReadOnlySpan<byte> frame, out uint seed)
    {
        seed = 0;
        if (frame.Length < MinimumFrameLength || frame.Length > MaximumFrameLength ||
            BinaryPrimitives.ReadUInt16LittleEndian(frame) != frame.Length ||
            BinaryPrimitives.ReadUInt16LittleEndian(frame[2..]) != PacketType)
        {
            return false;
        }

        seed = BinaryPrimitives.ReadUInt32LittleEndian(frame[4..]);
        return true;
    }
}
