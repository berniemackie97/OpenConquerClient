using System.Buffers.Binary;
using OpenConquer.Networking.Game.Login;
using OpenConquer.Networking.Game.World;

namespace OpenConquer.Client.World;

internal sealed class WorldInboundState
{
    private readonly uint _heroId;
    private readonly HashSet<uint> _unhandled = [];

    internal WorldInboundState(GameWorldEntrySnapshot world)
    {
        _heroId = world.Character.EntityId;
        X = world.X;
        Y = world.Y;
        Weather = (world.WeatherKind, 0, 0, 0);
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
