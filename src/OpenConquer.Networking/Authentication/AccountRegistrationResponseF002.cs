using System.Buffers.Binary;

namespace OpenConquer.Networking.Authentication;

/// <summary>
/// Reads the OpenConquer account-registration response (type 0xF002): one 32-bit result code. This is untrusted
/// wire data; an undefined code is a protocol error, not a result.
/// </summary>
internal static class AccountRegistrationResponseF002
{
    internal const ushort PacketType = 0xF002;
    internal const int FrameLength = 8;

    internal static bool TryRead(ReadOnlySpan<byte> frame, out AccountRegistrationResult result)
    {
        result = default;
        if (frame.Length != FrameLength ||
            BinaryPrimitives.ReadUInt16LittleEndian(frame) != FrameLength ||
            BinaryPrimitives.ReadUInt16LittleEndian(frame[2..]) != PacketType)
        {
            return false;
        }

        uint code = BinaryPrimitives.ReadUInt32LittleEndian(frame[4..]);
        if (!Enum.IsDefined((AccountRegistrationResult)code))
        {
            return false;
        }

        result = (AccountRegistrationResult)code;
        return true;
    }
}
