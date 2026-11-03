using System.Buffers.Binary;

namespace OpenConquer.Client.World;

internal readonly record struct WorldHeroItem(uint Id, uint TypeId, ushort Durability,
    byte AdditionLevel, byte EnchantmentLifeBonus);

internal sealed class WorldHeroEquipment
{
    private static ReadOnlySpan<byte> SnapshotSlots => [1, 2, 3, 4, 5, 6, 7, 8, 9, 15, 16, 17, 18];

    private readonly WorldHeroItem?[] _tracked = new WorldHeroItem?[30];
    private readonly WorldHeroItem?[] _active = new WorldHeroItem?[19];

    internal WorldHeroItem? GetActiveItem(int slot) => slot is >= 1 and <= 18
        ? _active[slot] : throw new ArgumentOutOfRangeException(nameof(slot));

    internal static WorldHeroEquipment FromHydration(IReadOnlyList<byte[]> packets)
    {
        WorldHeroEquipment equipment = new();

        foreach (byte[] packet in packets)
        {
            if (packet.Length < 4 || BinaryPrimitives.ReadUInt16LittleEndian(packet) != packet.Length)
            {
                throw new InvalidDataException("World-entry equipment hydration contains an invalid packet header.");
            }

            ushort type = BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(2));
            if (type == 1008)
            {
                if (packet.Length != 68)
                {
                    throw new InvalidDataException("World-entry item snapshot has an invalid length.");
                }

                if (packet[16] != 1)
                {
                    continue;
                }

                uint id = BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(4));
                uint typeId = BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(8));
                if (id == 0 || typeId == 0)
                {
                    throw new InvalidDataException("World-entry item snapshot has an invalid identity.");
                }

                byte position = packet[18];
                if (position is not ((>= 1 and <= 18) or (>= 21 and <= 29)))
                {
                    continue;
                }

                WorldHeroItem item = new(id, typeId,
                    BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(12)), packet[33], packet[36]);
                equipment._tracked[position] = item;
                if (position is >= 10 and <= 18)
                {
                    equipment._active[position] = item;
                }
            }
            else if (type == 1009)
            {
                if (packet.Length < 14)
                {
                    throw new InvalidDataException("World-entry equipment packet is truncated.");
                }

                if (BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(12)) != 46)
                {
                    continue;
                }

                if (packet.Length != 88)
                {
                    throw new InvalidDataException("World-entry active-equipment snapshot has an invalid length.");
                }

                ReadOnlySpan<byte> slots = SnapshotSlots;
                for (int i = 0; i < slots.Length; i++)
                {
                    uint id = BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(32 + i * 4));
                    equipment._active[slots[i]] = id == 0 ? null : equipment.FindTracked(id);
                }
            }
        }

        return equipment;
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
