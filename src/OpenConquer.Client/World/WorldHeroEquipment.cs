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
            equipment.Apply(packet);
        }

        return equipment;
    }

    internal bool Apply(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < 4 || BinaryPrimitives.ReadUInt16LittleEndian(packet) != packet.Length)
        {
            throw new InvalidDataException("Equipment packet has an invalid header.");
        }

        return BinaryPrimitives.ReadUInt16LittleEndian(packet[2..]) switch
        {
            1008 => ApplyItem(packet),
            1009 => ApplyTick(packet),
            _ => false,
        };
    }

    private bool ApplyItem(ReadOnlySpan<byte> packet)
    {
        if (packet.Length != 68)
        {
            throw new InvalidDataException("Item snapshot has an invalid length.");
        }

        byte kind = packet[16];
        if (kind is not 1 and not 3)
        {
            return false;
        }

        uint id = BinaryPrimitives.ReadUInt32LittleEndian(packet[4..]);
        uint typeId = BinaryPrimitives.ReadUInt32LittleEndian(packet[8..]);
        if (id == 0 || typeId == 0)
        {
            throw new InvalidDataException("Item snapshot has an invalid identity.");
        }

        int previous = FindTrackedPosition(id);
        if (kind == 3 && previous == 0)
        {
            return false;
        }

        byte position = packet[18];
        if (kind == 3 && position == 0)
        {
            return RemoveItem(id);
        }

        if (position is 0 or 19 or 20 or >= 30)
        {
            return false;
        }

        WorldHeroItem item = new(id, typeId,
            BinaryPrimitives.ReadUInt16LittleEndian(packet[12..]), packet[33], packet[36]);

        if (previous != 0 && previous != position)
        {
            _tracked[previous] = null;
            ClearActive(id);
        }

        _tracked[position] = item;
        for (int slot = 1; slot < _active.Length; slot++)
        {
            if (_active[slot] is { } active && active.Id == id)
            {
                _active[slot] = item;
            }
        }

        if (position is (>= 10 and <= 12) or (>= 15 and <= 18))
        {
            _active[position] = item;
        }

        return true;
    }

    private bool ApplyTick(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < 14)
        {
            throw new InvalidDataException("Equipment packet is truncated.");
        }

        ushort subtype = BinaryPrimitives.ReadUInt16LittleEndian(packet[12..]);
        if (subtype is not 3 and not 46)
        {
            return false;
        }

        if (packet.Length != 88)
        {
            throw new InvalidDataException("Equipment mutation has an invalid length.");
        }

        if (subtype == 3)
        {
            return RemoveItem(BinaryPrimitives.ReadUInt32LittleEndian(packet[4..]));
        }

        ReadOnlySpan<byte> slots = SnapshotSlots;
        for (int i = 0; i < slots.Length; i++)
        {
            uint id = BinaryPrimitives.ReadUInt32LittleEndian(packet[(32 + i * 4)..]);
            _active[slots[i]] = id == 0 ? null : FindTracked(id);
        }

        return true;
    }

    private bool RemoveItem(uint id)
    {
        int position = FindTrackedPosition(id);
        if (position == 0)
        {
            return false;
        }

        _tracked[position] = null;
        ClearActive(id);
        return true;
    }

    private void ClearActive(uint id)
    {
        for (int slot = 1; slot < _active.Length; slot++)
        {
            if (_active[slot] is { } item && item.Id == id)
            {
                _active[slot] = null;
            }
        }
    }

    private int FindTrackedPosition(uint id)
    {
        for (int position = 1; position < _tracked.Length; position++)
        {
            if (_tracked[position] is { } item && item.Id == id)
            {
                return position;
            }
        }

        return 0;
    }

    private WorldHeroItem? FindTracked(uint id)
    {
        int position = FindTrackedPosition(id);
        return position == 0 ? null : _tracked[position];
    }
}
