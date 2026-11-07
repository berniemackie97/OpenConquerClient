using OpenConquer.Client.Diagnostics;
using OpenConquer.Content.Items;
using OpenConquer.Networking.Game.Login;
using OpenConquer.Networking.Game.World;

namespace OpenConquer.Client.World;

internal sealed class WorldInboundState : IGameWorldPacketHandler
{
    private const int MaximumPacketDiagnostics = 64;
    private readonly ClientDiagnostics _diagnostics;
    private readonly uint _heroId;
    private readonly Func<uint> _clock;
    private readonly HashSet<uint> _unhandled = [];
    private readonly HashSet<uint> _unhandledAttributes = [];

    /// <summary>
    /// Seeds the hero from the admission snapshot, then replays every hydration frame through this handler in arrival
    /// order, as native processes each message when it arrives. Frames that arrived before the EnterMap reply apply
    /// before its position does. A subtype-46 snapshot therefore clamps against the hero life that preceded it.
    /// </summary>
    internal WorldInboundState(GameWorldEntrySnapshot world, ItemStatCatalog itemStats, Func<uint>? clock = null,
        uint clientStartedAt = 0, ClientDiagnostics? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        _heroId = world.Character.EntityId;
        _diagnostics = diagnostics ?? ClientDiagnostics.Silent;
        _clock = clock ?? (static () => 0);
        Heartbeat = new WorldHeartbeat(world.HeartbeatStartedAt, clientStartedAt, world.LocalAddress);
        Hero = new WorldHeroModel(WorldHeroAttributes.FromCharacter(world.Character), itemStats, world.MapFlags,
            world.Character.TransformLookSourceId != 0);
        Weather = (world.WeatherKind, 0, 0, 0);
        int enteredAt = Math.Clamp(world.HydrationEnteredAt, 0, world.HydrationPackets.Count);
        for (int index = 0; index < world.HydrationPackets.Count; index++)
        {
            if (index == enteredAt)
            {
                ApplyEntryPosition(world);
            }

            GameWorldPacketDispatcher.Dispatch(world.HydrationPackets[index], this);
        }

        if (enteredAt == world.HydrationPackets.Count)
        {
            ApplyEntryPosition(world);
        }
    }

    internal WorldHeroModel Hero
    {
        get;
    }

    internal WorldHeartbeat Heartbeat
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

    internal int X
    {
        get;
        private set;
    }

    internal int Y
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
        // Native DWORD ADD at 0x7406DB/0x7406DD wraps; the protected tile setter never narrows to ushort.
        (X, Y) = (unchecked(X + dx), unchecked(Y + dy));
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
        // CMsgItem__Process subtype 27: hero+0x2CF4 = timeGetTime - +16.
        if (item.Subtype == GameItem1009.LatencyTick)
        {
            Heartbeat.ObserveLatencyTick(_clock(), item.ParameterC);
            return false;
        }

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
                    _diagnostics.UnhandledWorldAttribute(diagnosticKey);
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
        if (_unhandled.Count >= MaximumPacketDiagnostics || !_unhandled.Add(((uint)type << 16) | action))
        {
            return;
        }

        _diagnostics.UnhandledWorldPacket(type, action);
        if (_unhandled.Count == MaximumPacketDiagnostics)
        {
            _diagnostics.WorldPacketLimitReached(MaximumPacketDiagnostics);
        }
    }

    private void ApplyEntryPosition(GameWorldEntrySnapshot world)
    {
        X = world.X;
        Y = world.Y;
        DirectionIndex = world.DirectionIndex;
    }
}
