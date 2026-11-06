using System.Buffers.Binary;
using OpenConquer.Networking.Game.Login;
using OpenConquer.Networking.Game.World;

namespace OpenConquer.Client.World;

internal sealed class WorldInboundState : IGameWorldPacketHandler
{
    private readonly uint _heroId;
    private readonly HashSet<uint> _unhandled = [];
    private readonly HashSet<uint> _unhandledAttributes = [];

    internal WorldInboundState(GameWorldEntrySnapshot world)
    {
        _heroId = world.Character.EntityId;
        bool needsEquipmentLifeClamp = world.HydrationPackets.Any(static packet =>
            packet.Length == 88 &&
            BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(2)) == 1009 &&
            BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(12)) == 46);
        Hero = new WorldHeroModel(WorldHeroAttributes.FromCharacter(world.Character),
            WorldHeroEquipment.FromHydration(world.HydrationPackets), needsEquipmentLifeClamp);
        X = world.X;
        Y = world.Y;
        DirectionIndex = world.DirectionIndex;
        Weather = (world.WeatherKind, 0, 0, 0);
    }

    internal WorldHeroModel Hero
    {
        get;
    }
    internal WorldHeroEquipment Equipment => Hero.Equipment;
    internal uint EquipmentRevision => Hero.EquipmentRevision;
    internal WorldHeroAttributes HeroAttributes => Hero.Attributes;

    internal byte DirectionIndex
    {
        get;
        private set;
    }

    internal ushort X
    {
        get;
        private set;
    }

    internal ushort Y
    {
        get;
        private set;
    }

    internal (uint Kind, uint Intensity, uint DirectionDegrees, uint Parameter) Weather
    {
        get;
        private set;
    }

    internal bool Apply(ReadOnlyMemory<byte> packet) => GameWorldPacketDispatcher.Dispatch(packet.Span, this);

    bool IGameWorldPacketHandler.HandleWalk(GameWalk10005 walk)
    {
        if (walk.EntityId != _heroId || walk.Selector == GameWalk10005.MountedSelector)
        {
            ReportUnhandled(GameWalk10005.PacketType, 0);
            return false;
        }

        (int dx, int dy) = walk.GetOrdinaryDelta();
        int x = X + dx;
        int y = Y + dy;
        if ((uint)x > ushort.MaxValue || (uint)y > ushort.MaxValue)
        {
            throw new InvalidDataException("World walk acknowledgement has an invalid destination.");
        }

        (X, Y) = ((ushort)x, (ushort)y);
        DirectionIndex = walk.DirectionIndex;
        return true;
    }

    bool IGameWorldPacketHandler.HandleAction(GameWorldAction10010 action)
    {
        if (action.Action != GameWorldAction10010.PositionCorrection)
        {
            ReportUnhandled(GameWorldAction10010.PacketType, action.Action);
            return false;
        }

        // Client policy, not a native rule: a correction must target the hero and leave every other field zero.
        if (action.EntityId != _heroId || action.ParameterPair != 0 || action.ActionParameter != 0 ||
            action.Direction != 0 || action.Data1 != 0 || action.Data2 != 0 || action.Flag != 0)
        {
            throw new InvalidDataException("World position correction is malformed.");
        }

        bool changed = X != action.X || Y != action.Y;
        (X, Y) = (action.X, action.Y);
        return changed;
    }

    bool IGameWorldPacketHandler.HandleItem(ushort packetType, ReadOnlySpan<byte> packet)
    {
        if (Equipment.Apply(packet))
        {
            Hero.RecordEquipmentChange(packetType == GameWorldPacketDispatcher.ItemPacketType &&
                BinaryPrimitives.ReadUInt16LittleEndian(packet[12..]) == 46);
        }

        return false;
    }

    bool IGameWorldPacketHandler.HandleAttributes(GameUserAttrib10017 attributes)
    {
        if (attributes.TargetId != _heroId)
        {
            ReportUnhandled(GameUserAttrib10017.PacketType, 0);
            return false;
        }

        foreach (GameUserAttribEntry entry in attributes)
        {
            if (!Hero.TryApplyAttribute(entry.AttributeId, entry.Low, entry.High))
            {
                uint diagnosticKey = entry.AttributeId is <= 54 or 128 ? entry.AttributeId : uint.MaxValue;
                if (_unhandledAttributes.Add(diagnosticKey))
                {
                    Console.Error.WriteLine($"OpenConquer: world hero attribute {entry.AttributeId} is not implemented.");
                }
            }
        }

        return false;
    }

    bool IGameWorldPacketHandler.HandleWeather(GameWeather1016 weather)
    {
        if (!weather.HasSupportedValues)
        {
            throw new InvalidDataException("World weather parameters are invalid.");
        }

        Weather = (weather.Kind, weather.Intensity, weather.DirectionDegrees, weather.Parameter);
        return false;
    }

    bool IGameWorldPacketHandler.HandleUnknown(ushort packetType)
    {
        ReportUnhandled(packetType, 0);
        return false;
    }

    private void ReportUnhandled(ushort type, ushort action)
    {
        if (_unhandled.Add(((uint)type << 16) | action))
        {
            Console.Error.WriteLine(action == 0
                ? $"OpenConquer: world packet {type} is not implemented."
                : $"OpenConquer: world action {type}/{action} is not implemented.");
        }
    }
}
