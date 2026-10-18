using System.Buffers.Binary;

namespace OpenConquer.Networking.Authentication;

/// <summary>
/// The AccountServer 1100 report. The caller supplies the hardware text; it isn't trusted, isn't used as an
/// authentication secret and is never read from the machine automatically.
/// </summary>
internal static class LoginAccountMacAddressReport1100
{
    internal const ushort PacketType = 1100;
    internal const int FrameLength = 52;
    internal const int MacFieldLength = 40;

    internal static void Write(Span<byte> destination, uint sessionToken, ReadOnlySpan<byte> macAddress)
    {
        if (destination.Length != FrameLength)
        {
            throw new ArgumentException("The native 1100 frame must be exactly 52 bytes.", nameof(destination));
        }

        ArgumentOutOfRangeException.ThrowIfZero(sessionToken);

        if (macAddress.Length is not (0 or 12))
        {
            throw new ArgumentException("The MAC report must be empty or 12 uppercase ASCII hexadecimal bytes.", nameof(macAddress));
        }

        foreach (byte value in macAddress)
        {
            if (value is >= (byte)'0' and <= (byte)'9' or >= (byte)'A' and <= (byte)'F')
            {
                continue;
            }

            throw new ArgumentException("The MAC report must contain uppercase ASCII hexadecimal bytes.", nameof(macAddress));
        }

        destination.Clear();
        BinaryPrimitives.WriteUInt16LittleEndian(destination, FrameLength);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[2..], PacketType);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[4..], sessionToken);
        macAddress.CopyTo(destination[8..]);
        // Bytes 48..51 are in the packet but what they mean isn't known, so they stay zero instead of a guess.
    }
}
