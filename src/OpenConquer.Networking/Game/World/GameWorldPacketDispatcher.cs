using System.Buffers.Binary;
using OpenConquer.Networking.Game.Login;

namespace OpenConquer.Networking.Game.World;

/// <summary>Receives decoded world packets from <see cref="GameWorldPacketDispatcher"/>.</summary>
internal interface IGameWorldPacketHandler
{
    bool HandleWalk(GameWalk10005 walk);

    bool HandleAction(GameWorldAction10010 action);

    bool HandleItemRecord(GameItemRecord1008 record);

    bool HandleItem(GameItem1009 item);

    bool HandleAttributes(GameUserAttrib10017 attributes);

    bool HandleWeather(GameWeather1016 weather);

    bool HandleUnknown(ushort packetType);
}

/// <summary>
/// Routes one world frame by its type word to a typed handler, as native <c>CNetMsg__ProcessMsg_Dispatch</c> 0x6DF3B5
/// routes to a registered class's parser and process. Frames that do not match their codec's layout throw
/// <see cref="InvalidDataException"/>; the layout rules are this client's ingress policy.
/// </summary>
internal static class GameWorldPacketDispatcher
{
    /// <returns>The handler's result.</returns>
    internal static bool Dispatch(ReadOnlySpan<byte> packet, IGameWorldPacketHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (packet.Length < 4 || BinaryPrimitives.ReadUInt16LittleEndian(packet) != packet.Length)
        {
            throw new InvalidDataException("World packet has an invalid length.");
        }

        ushort type = BinaryPrimitives.ReadUInt16LittleEndian(packet[2..]);
        switch (type)
        {
            case GameWalk10005.PacketType:
                return GameWalk10005.TryRead(packet, out GameWalk10005 walk)
                    ? handler.HandleWalk(walk)
                    : throw new InvalidDataException("World walk packet is malformed.");
            case GameWorldAction10010.PacketType:
                return GameWorldAction10010.TryRead(packet, out GameWorldAction10010 action)
                    ? handler.HandleAction(action)
                    : throw new InvalidDataException("World action packet is malformed.");
            case GameItemRecord1008.PacketType:
                return GameItemRecord1008.TryRead(packet, out GameItemRecord1008 record)
                    ? handler.HandleItemRecord(record)
                    : throw new InvalidDataException("World item record has an invalid length.");
            case GameItem1009.PacketType:
                return GameItem1009.TryRead(packet, out GameItem1009 item)
                    ? handler.HandleItem(item)
                    : throw new InvalidDataException("World item packet is truncated.");
            case GameUserAttrib10017.PacketType:
                return GameUserAttrib10017.TryRead(packet, out GameUserAttrib10017 attributes)
                    ? handler.HandleAttributes(attributes)
                    : throw new InvalidDataException("World attribute packet has an invalid record layout or count.");
            case GameWeather1016.PacketType:
                return GameWeather1016.TryRead(packet, out GameWeather1016 weather)
                    ? handler.HandleWeather(weather)
                    : throw new InvalidDataException("World weather packet has an invalid length.");
            default:
                return handler.HandleUnknown(type);
        }
    }
}
