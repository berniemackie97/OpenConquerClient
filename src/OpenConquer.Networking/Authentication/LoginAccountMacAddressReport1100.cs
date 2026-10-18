using System.Buffers.Binary;

namespace OpenConquer.Networking.Authentication;

/// <summary>
/// Native AccountServer 1100 report. Hardware text is untrusted, caller-provided telemetry
/// and is never used as an authentication secret or discovered automatically.
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
        // Byte offsets 48..51 are structurally present but semantically unknown in native evidence.
        // Zero initialization is deliberate; no guessed telemetry is injected.
    }
}
