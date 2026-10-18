using System.Buffers.Binary;

namespace OpenConquer.Networking.Authentication;

/// <summary>
/// The standard native 5517 GameServer login proof, sent only after the GameServer
/// Diffie-Hellman handshake. Packet 1052 also has a DIFFERENT AccountServer form.
/// Pure serialization; never sends packets or authorizes a game session.
/// </summary>
internal static class GameLoginProof1052
{
    internal const int PacketLength = 28;
    internal const ushort PacketType = 1052;
    internal const ushort Mode = 0x7C;
    internal const int HardwareAddressLength = 6;

    /// <summary>
    /// Writes native little-endian fields and their two zeroed reserved bytes.
    /// An empty hardware address represents the verified native unavailable fallback.
    /// </summary>
    internal static void Write(Span<byte> destination, uint sessionToken, uint authenticationKey,
        ushort localeTag, ReadOnlySpan<byte> hardwareAddress, int resourceVersion)
    {
        if (destination.Length != PacketLength)
        {
            throw new ArgumentException("The GameServer 1052 proof must be exactly 28 bytes.", nameof(destination));
        }

        if (sessionToken == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sessionToken),
                "A GameServer login proof requires a nonzero AccountServer session token.");
        }

        if (hardwareAddress.Length is not (0 or HardwareAddressLength))
        {
            throw new ArgumentException("The hardware address must contain exactly six bytes or be empty.",
                nameof(hardwareAddress));
        }

        destination.Clear();
        BinaryPrimitives.WriteUInt16LittleEndian(destination, PacketLength);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[2..], PacketType);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[4..], sessionToken);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[8..], authenticationKey);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[12..], Mode);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[14..], localeTag);
        hardwareAddress.CopyTo(destination[16..]);
        BinaryPrimitives.WriteInt32LittleEndian(destination[24..], resourceVersion);
    }
}
