using OpenConquer.Networking.Game.Framing;

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
    /// Native input gate (0x7606CD): signed kind 1-11 except 6, intensity 0-1022, direction 0-359; rejection is a no-op.
    /// Accepted values are retained as the wire tuple here; native rendering-parameter normalization is separate.
    /// </summary>
    internal bool HasSupportedValues => Kind is >= 1 and <= 11 and not 6 && Intensity <= 1022 && DirectionDegrees <= 359;

    /// <summary>Reads the fields from the native message buffer. Parse has no size rule; fields past a short message read zero.</summary>
    internal static GameWeather1016 Read(GameMessageBuffer message) => new(
        message.ReadUInt32(4),
        message.ReadUInt32(8),
        message.ReadUInt32(12),
        message.ReadUInt32(16));
}
