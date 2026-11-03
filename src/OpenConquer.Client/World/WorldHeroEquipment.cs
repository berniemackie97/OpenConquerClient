using System.Buffers.Binary;

namespace OpenConquer.Client.World;

internal readonly record struct WorldHeroItem(uint Id, uint TypeId, ushort Durability,
    byte AdditionLevel, byte EnchantmentLifeBonus);

internal sealed class WorldHeroEquipment
{
    private static ReadOnlySpan<byte> SnapshotSlots => [1, 2, 3, 5, 4, 6, 7, 8, 9, 16, 15, 17, 18];

    private readonly WorldHeroItem?[] _tracked = new WorldHeroItem?[30];
    private readonly WorldHeroItem?[] _active = new WorldHeroItem?[19];

    internal WorldHeroItem? GetActiveItem(int slot) => slot is >= 1 and <= 18
        ? _active[slot] : throw new ArgumentOutOfRangeException(nameof(slot));

    internal static WorldHeroEquipment FromHydration(IReadOnlyList<byte[]> packets)
    {
        WorldHeroEquipment equipment = new();
        foreach (byte[] packet in packets)
        {
            if (packet.Length < 4)
            {
                throw new InvalidDataException("World-entry equipment hydration contains an invalid packet header.");
            }

            switch (BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(2)))
            {
                case 1008:
                    equipment.ApplyItemSnapshot(packet);
                    break;
                case 1009:
                    equipment.ApplyActiveSnapshot(packet);
                    break;
            }
        }

        return equipment;
    }

    private void ApplyItemSnapshot(ReadOnlySpan<byte> packet)
    {
        if (packet.Length != 68)
        {
            throw new InvalidDataException("World-entry item snapshot has an invalid length.");
        }

        if (packet[16] != 1)
        {
            return;
        }

        uint id = BinaryPrimitives.ReadUInt32LittleEndian(packet[4..]);
        uint typeId = BinaryPrimitives.ReadUInt32LittleEndian(packet[8..]);
        if (id == 0 || typeId == 0)
        {
            throw new InvalidDataException("World-entry item snapshot has an invalid identity.");
        }

        byte position = packet[18];
        if (position is 0 or 19 or 20 or >= 30)
        {
            return;
        }

        WorldHeroItem item = new(id, typeId,
            BinaryPrimitives.ReadUInt16LittleEndian(packet[12..]), packet[33], packet[36]);
        _tracked[position] = item;
        if (position is (>= 10 and <= 12) or (>= 15 and <= 18))
        {
            _active[position] = item;
        }
    }

    private void ApplyActiveSnapshot(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < 14)
        {
            throw new InvalidDataException("World-entry equipment packet is truncated.");
        }

        if (BinaryPrimitives.ReadUInt16LittleEndian(packet[12..]) != 46)
        {
            return;
        }

        if (packet.Length != 88)
        {
            throw new InvalidDataException("World-entry active-equipment snapshot has an invalid length.");
        }

        ReadOnlySpan<byte> slots = SnapshotSlots;
        for (int i = 0; i < slots.Length; i++)
        {
            uint id = BinaryPrimitives.ReadUInt32LittleEndian(packet[(32 + i * 4)..]);
            _active[slots[i]] = id == 0 ? null : FindTracked(id);
        }
    }

    private WorldHeroItem? FindTracked(uint id)
    {
        for (int position = 1; position < _tracked.Length; position++)
        {
            if (_tracked[position] is { } item && item.Id == id)
            {
                return item;
            }
        }

        return null;
    }
}
