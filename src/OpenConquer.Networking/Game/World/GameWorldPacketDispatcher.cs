using OpenConquer.Networking.Game.Framing;
using OpenConquer.Networking.Game.Login;

namespace OpenConquer.Networking.Game.World;

/// <summary>Receives decoded world packets from <see cref="GameWorldPacketDispatcher"/>.</summary>
internal interface IGameWorldPacketHandler
{
    bool HandleWalk(GameWalk10005 walk);

    bool HandlePlayerSpawn(GamePlayerSpawn10014 player);

    bool HandleAction(GameWorldAction10010 action);

    bool HandleItemRecord(GameItemRecord1008 record);

    bool HandleItem(GameItem1009 item);

    bool HandleAttributes(GameUserAttrib10017 attributes);

    bool HandleWeather(GameWeather1016 weather);

    bool HandleUnknown(ushort packetType);
}

/// <summary>
/// Routes one world message by its type word to a typed handler, as native <c>CNetMsg__ProcessMsg_Dispatch</c> 0x6DF3B5
/// routes to a registered class's parser and process. Every message is first loaded into a zeroed 0x400 buffer as native
/// does (<see cref="GameMessageBuffer"/>), so codecs have no length rules: short messages read zero past their end.
/// </summary>
internal static class GameWorldPacketDispatcher
{
    /// <returns>
    /// The handler's result, or false for a message native dispatch drops before Parse (size above 0x400, size word not
    /// matching the length, type 0). Native skips those and continues (0x6DEC3A), so no handler is called.
    /// </returns>
    internal static bool Dispatch(ReadOnlySpan<byte> packet, IGameWorldPacketHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        Span<byte> storage = stackalloc byte[GameMessageBuffer.Capacity];
        if (!GameMessageBuffer.TryLoad(packet, storage, out GameMessageBuffer message))
        {
            return false;
        }

        return message.Type switch
        {
            GameWalk10005.PacketType => handler.HandleWalk(GameWalk10005.Read(message)),
            GamePlayerSpawn10014.PacketType => GamePlayerSpawn10014.TryRead(message, out GamePlayerSpawn10014? player)
                && handler.HandlePlayerSpawn(player),
            GameWorldAction10010.PacketType => handler.HandleAction(GameWorldAction10010.Read(message)),
            GameItemRecord1008.PacketType => handler.HandleItemRecord(GameItemRecord1008.Read(message)),
            GameItem1009.PacketType => handler.HandleItem(GameItem1009.Read(message)),
            GameUserAttrib10017.PacketType => handler.HandleAttributes(GameUserAttrib10017.Read(message)),
            GameWeather1016.PacketType => handler.HandleWeather(GameWeather1016.Read(message)),
            ushort type => handler.HandleUnknown(type),
        };
    }
}
