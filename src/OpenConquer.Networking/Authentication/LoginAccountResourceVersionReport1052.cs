using System.Buffers.Binary;

namespace OpenConquer.Networking.Authentication;

/// <summary>
/// The 28-byte AccountServer resource report. Not the GameServer 1052 login proof sent later.
/// </summary>
internal static class LoginAccountResourceVersionReport1052
{
    internal const ushort PacketType = 1052;
    internal const int FrameLength = 28;

    internal static void Write(Span<byte> destination, uint sessionToken, int resourceVersion)
    {
        if (destination.Length != FrameLength)
        {
            throw new ArgumentException("The native AccountServer 1052 frame must be exactly 28 bytes.", nameof(destination));
        }

        ArgumentOutOfRangeException.ThrowIfZero(sessionToken);

        destination.Clear();
        BinaryPrimitives.WriteUInt16LittleEndian(destination, FrameLength);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[2..], PacketType);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[4..], sessionToken);
        BinaryPrimitives.WriteInt32LittleEndian(destination[8..], resourceVersion);
        "res.dat"u8.CopyTo(destination[12..]);
    }
}
