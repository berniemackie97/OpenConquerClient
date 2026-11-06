using System.Buffers.Binary;
using OpenConquer.Networking.Game.Login;
using OpenConquer.Networking.Game.World;

namespace OpenConquer.Client.World;

internal sealed class WorldInboundState
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

    internal bool Apply(ReadOnlyMemory<byte> packet)
    {
        ReadOnlySpan<byte> data = packet.Span;
        if (data.Length < 4 || BinaryPrimitives.ReadUInt16LittleEndian(data) != data.Length)
        {
            throw new InvalidDataException("World packet has an invalid length.");
        }

        ushort type = BinaryPrimitives.ReadUInt16LittleEndian(data[2..]);
        if (type == GameWalk10005.PacketType)
        {
            if (!GameWalk10005.TryRead(data, out GameWalk10005 walk))
            {
                throw new InvalidDataException("World walk packet is malformed.");
            }

            if (walk.EntityId != _heroId || walk.Selector == GameWalk10005.MountedSelector)
            {
                ReportUnhandled(type, 0);
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

        if (type == GameWorldAction10010.PacketType)
        {
            if (!GameWorldAction10010.TryRead(data, out ushort action, out uint entityId,
                    out uint parameter, out ushort x, out ushort y))
            {
                throw new InvalidDataException("World action packet is malformed.");
            }

            if (action == 108)
            {
                if (entityId != _heroId || parameter != 0 ||
                    BinaryPrimitives.ReadUInt32LittleEndian(data[12..]) != 0 ||
                    BinaryPrimitives.ReadUInt16LittleEndian(data[22..]) != 0 ||
                    BinaryPrimitives.ReadUInt64LittleEndian(data[28..]) != 0 ||
                    data[36] != 0)
                {
                    throw new InvalidDataException("World position correction is malformed.");
                }

                bool changed = X != x || Y != y;
                (X, Y) = (x, y);
                return changed;
            }

            ReportUnhandled(type, action);
            return false;
        }

        if (type is 1008 or 1009)
        {
            if (Equipment.Apply(data))
            {
                Hero.RecordEquipmentChange(type == 1009 && BinaryPrimitives.ReadUInt16LittleEndian(data[12..]) == 46);
            }

            return false;
        }

        if (type == 10017)
        {
            ApplyHeroAttributes(data);
            return false;
        }

        if (type == 1016)
        {
            if (data.Length != 20)
            {
                throw new InvalidDataException("World weather packet has an invalid length.");
            }

            uint kind = BinaryPrimitives.ReadUInt32LittleEndian(data[4..]);
            uint intensity = BinaryPrimitives.ReadUInt32LittleEndian(data[8..]);
            uint direction = BinaryPrimitives.ReadUInt32LittleEndian(data[12..]);
            uint parameter = BinaryPrimitives.ReadUInt32LittleEndian(data[16..]);
            if (kind is < 1 or > 11 or 6 || intensity > 1022 || direction > 359)
            {
                throw new InvalidDataException("World weather parameters are invalid.");
            }

            Weather = (kind, intensity, direction, parameter);
            return false;
        }

        ReportUnhandled(type, 0);
        return false;
    }

    private void ApplyHeroAttributes(ReadOnlySpan<byte> data)
    {
        const int headerLength = 12;
        const int entryLength = 20;
        if (data.Length < headerLength || (data.Length - headerLength) % entryLength != 0)
        {
            throw new InvalidDataException("World attribute packet has an invalid record layout.");
        }

        uint entityId = BinaryPrimitives.ReadUInt32LittleEndian(data[4..]);
        uint count = BinaryPrimitives.ReadUInt32LittleEndian(data[8..]);
        if (count != (uint)((data.Length - headerLength) / entryLength))
        {
            throw new InvalidDataException("World attribute record count does not match the packet length.");
        }

        if (entityId != _heroId)
        {
            ReportUnhandled(10017, 0);
            return;
        }

        for (int offset = headerLength; offset < data.Length; offset += entryLength)
        {
            ReadOnlySpan<byte> entry = data.Slice(offset, entryLength);
            uint attributeId = BinaryPrimitives.ReadUInt32LittleEndian(entry);
            ulong low = BinaryPrimitives.ReadUInt64LittleEndian(entry[4..]);
            ulong high = BinaryPrimitives.ReadUInt64LittleEndian(entry[12..]);
            if (!Hero.TryApplyAttribute(attributeId, low, high))
            {
                uint diagnosticKey = attributeId is <= 54 or 128 ? attributeId : uint.MaxValue;
                if (_unhandledAttributes.Add(diagnosticKey))
                {
                    Console.Error.WriteLine($"OpenConquer: world hero attribute {attributeId} is not implemented.");
                }
            }
        }
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
