using OpenConquer.Content.Items;
using OpenConquer.Networking.Game.Login;
using OpenConquer.Networking.Game.World;

namespace OpenConquer.Client.World;

internal sealed class WorldInboundState : IGameWorldPacketHandler
{
    private readonly uint _heroId;
    private readonly HashSet<uint> _unhandled = [];
    private readonly HashSet<uint> _unhandledAttributes = [];

    /// <summary>
    /// Seeds the hero from the admission snapshot, then replays the hydration item frames in arrival order, so a
    /// subtype-46 snapshot clamps against the hero life that preceded it. Other hydration frames are not consumed.
    /// </summary>
    internal WorldInboundState(GameWorldEntrySnapshot world, ItemStatCatalog itemStats)
    {
        ArgumentNullException.ThrowIfNull(world);
        _heroId = world.Character.EntityId;
        Hero = new WorldHeroModel(WorldHeroAttributes.FromCharacter(world.Character), itemStats, world.MapFlags,
            world.Character.TransformLookSourceId != 0);
        HydrationItems hydration = new(Hero);
        foreach (byte[] packet in world.HydrationPackets)
        {
            GameWorldPacketDispatcher.Dispatch(packet, hydration);
        }

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

        // Native 108 (0x6FFCBF..0x6FFCF3): a role that is not the hero is a silent no-op, and only +24/+26 are read.
        if (action.EntityId != _heroId)
        {
            return false;
        }

        bool changed = X != action.X || Y != action.Y;
        (X, Y) = (action.X, action.Y);
        return changed;
    }

    bool IGameWorldPacketHandler.HandleItemRecord(GameItemRecord1008 record)
    {
        Hero.ApplyItemRecord(record);
        return false;
    }

    bool IGameWorldPacketHandler.HandleItem(GameItem1009 item)
    {
        Hero.ApplyItem(item);
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
        // CMsgWeatherUpdate__ValidateAndNormalize 0x7606CD rejects these and Process then does nothing.
        if (!weather.HasSupportedValues)
        {
            return false;
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

    /// <summary>Consumes only hydration item frames, as world entry delivers them before ingress starts.</summary>
    private sealed class HydrationItems(WorldHeroModel hero) : IGameWorldPacketHandler
    {
        public bool HandleItemRecord(GameItemRecord1008 record)
        {
            hero.ApplyItemRecord(record);
            return false;
        }

        public bool HandleItem(GameItem1009 item)
        {
            hero.ApplyItem(item);
            return false;
        }

        public bool HandleWalk(GameWalk10005 walk) => false;

        public bool HandleAction(GameWorldAction10010 action) => false;

        public bool HandleAttributes(GameUserAttrib10017 attributes) => false;

        public bool HandleWeather(GameWeather1016 weather) => false;

        public bool HandleUnknown(ushort packetType) => false;
    }
}
