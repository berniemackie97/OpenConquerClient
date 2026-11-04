using System.Buffers.Binary;

namespace OpenConquer.Client.World;

internal readonly record struct WorldHeroItem(uint Id, uint TypeId, ushort Durability,
    byte AdditionLevel, byte EnchantmentLifeBonus);

internal sealed class WorldHeroEquipment
{
    private static ReadOnlySpan<byte> SnapshotSlots => [1, 2, 3, 4, 5, 6, 7, 8, 9, 15, 16, 17, 18];

    private readonly Dictionary<uint, WorldHeroItem> _inventory = [];
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
        if (kind == 3 && previous == 0 && !_inventory.ContainsKey(id))
        {
            return false;
        }

        byte position = packet[18];
        if (position is 19 or 20 or >= 30)
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

        if (position == 0)
        {
            _inventory[id] = item;
        }
        else
        {
            _inventory.Remove(id);
            _tracked[position] = item;
        }

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
        if (subtype is not (3 or 5 or 6 or 17 or 46))
        {
            return false;
        }

        if (packet.Length < 88 || (packet.Length - 84) % 4 != 0)
        {
            throw new InvalidDataException("Equipment mutation has an invalid length.");
        }

        uint itemId = BinaryPrimitives.ReadUInt32LittleEndian(packet[4..]);
        uint position = BinaryPrimitives.ReadUInt32LittleEndian(packet[8..]);
        switch (subtype)
        {
            case 3:
                return RemoveItem(itemId);
            case 5:
                return EquipItem(packet, itemId, position);
            case 6:
                return UnequipItem(position);
            case 17:
                return UpdateDurability(itemId, position);
            default:
                if (packet.Length != 88)
                {
                    throw new InvalidDataException("Active-equipment snapshot has an invalid length.");
                }

                ReadOnlySpan<byte> slots = SnapshotSlots;
                for (int i = 0; i < slots.Length; i++)
                {
                    uint id = BinaryPrimitives.ReadUInt32LittleEndian(packet[(32 + i * 4)..]);
                    _active[slots[i]] = id == 0 ? null : FindTracked(id);
                }

                return true;
        }
    }

    private bool RemoveItem(uint id)
    {
        bool removed = _inventory.Remove(id);
        int position = FindTrackedPosition(id);
        if (position != 0)
        {
            _tracked[position] = null;
            ClearActive(id);
            removed = true;
        }

        return removed;
    }

    private bool EquipItem(ReadOnlySpan<byte> packet, uint itemId, uint position)
    {
        uint count = BinaryPrimitives.ReadUInt32LittleEndian(packet[20..]);
        if (count > (uint)((packet.Length - 84) / 4))
        {
            throw new InvalidDataException("Equipment displacement list is truncated.");
        }

        if (position is < 1 or >= 30 or 19 or 20 ||
            !_inventory.TryGetValue(itemId, out WorldHeroItem item))
        {
            return false;
        }

        for (int index = 0; index < count; index++)
        {
            uint displacedId = BinaryPrimitives.ReadUInt32LittleEndian(packet[(84 + index * 4)..]);
            int displacedPosition = FindTrackedPosition(displacedId);
            if (displacedPosition != 0 && _tracked[displacedPosition] is { } displaced)
            {
                _tracked[displacedPosition] = null;
                _inventory[displacedId] = displaced;
                ClearActive(displacedId);
            }
        }

        _inventory.Remove(itemId);
        _tracked[position] = item;
        if (position <= 18)
        {
            _active[position] = item;
        }

        return true;
    }

    private bool UnequipItem(uint position)
    {
        if (position is < 1 or >= 30 or 19 or 20 ||
            _tracked[position] is not { } item)
        {
            return false;
        }

        _tracked[position] = null;
        _inventory[item.Id] = item;
        ClearActive(item.Id);

        if (position is 4 or 24 && item.TypeId % 10_000_000 / 100_000 == 5 &&
            item.TypeId % 100_000 / 1000 == 0)
        {
            int pairedPosition = (int)position + 1;
            if (_tracked[pairedPosition] is { } paired)
            {
                _tracked[pairedPosition] = null;
                _inventory[paired.Id] = paired;
                ClearActive(paired.Id);
            }
        }

        return true;
    }

    private bool UpdateDurability(uint durability, uint position)
    {
        if (position is < 1 or >= 30 or 19 or 20 ||
            _tracked[position] is not { } item)
        {
            return false;
        }

        WorldHeroItem updated = item with { Durability = unchecked((ushort)durability) };
        _tracked[position] = updated;
        for (int slot = 1; slot < _active.Length; slot++)
        {
            if (_active[slot] is { } active && active.Id == item.Id)
            {
                _active[slot] = updated;
            }
        }

        return updated != item;
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
