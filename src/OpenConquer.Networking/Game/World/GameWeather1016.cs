using System.Buffers.Binary;

namespace OpenConquer.Networking.Game.World;

/// <summary>
/// 5517 weather update (1016): four 32-bit fields at +4 kind, +8 intensity, +12 direction and +16 parameter.
/// Parser 0x7607DE, process 0x760801.
/// </summary>
internal readonly record struct GameWeather1016(uint Kind, uint Intensity, uint DirectionDegrees, uint Parameter)
{
    internal const ushort PacketType = 1016;
    internal const int PacketLength = 20;

    /// <summary>
    /// Client ingress policy: kind 1-11 except 6, intensity 0-1022, direction 0-359. Native validation uses signed
    /// comparisons and normalizes instead of rejecting, so a refusal here is not a native rejection.
    /// </summary>
    internal bool HasSupportedValues => Kind is >= 1 and <= 11 and not 6 && Intensity <= 1022 && DirectionDegrees <= 359;

    internal static bool TryRead(ReadOnlySpan<byte> packet, out GameWeather1016 weather)
    {
        weather = default;
        if (packet.Length != PacketLength ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet) != PacketLength ||
            BinaryPrimitives.ReadUInt16LittleEndian(packet[2..]) != PacketType)
        {
            return false;
        }

        weather = new GameWeather1016(
            BinaryPrimitives.ReadUInt32LittleEndian(packet[4..]),
            BinaryPrimitives.ReadUInt32LittleEndian(packet[8..]),
            BinaryPrimitives.ReadUInt32LittleEndian(packet[12..]),
            BinaryPrimitives.ReadUInt32LittleEndian(packet[16..]));
        return true;
    }
}
