using System.Buffers.Binary;
using OpenConquer.Networking.Game.Framing;

namespace OpenConquer.Networking.Game.Login;

/// <summary>
/// The native 2079 payload is a single unsigned value. Unknown values are
/// accepted as no-ops by the retail UI handler; they are not admission failures.
/// </summary>
internal readonly record struct GameServerState2079(uint Value)
{
    private const ushort PacketType = 2079;
    private const int MinimumPacketLength = 8;

    // The existing native UI global starts at 2000; unknown wire values
    // preserve its previous value rather than selecting a new one.
    internal ushort? SelectedUiState => Value switch
    {
        0 => 2000,
        1 => 2001,
        _ => null
    };

    internal static GameServerState2079 Read(ReadOnlySpan<byte> packet)
    {
        if (packet.Length is < MinimumPacketLength or > GameSecuredFrameProtocol.MaximumPacketLength ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet) != packet.Length ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet[2..]) != PacketType)
        {
            throw new InvalidDataException("GameServer state is not a valid 2079 packet.");
        }

        // Native 5517 ignores any bounded bytes after the first state DWORD.
        return new GameServerState2079(BinaryPrimitives.ReadUInt32LittleEndian(packet[4..]));
    }
}
