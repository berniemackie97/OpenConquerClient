using OpenConquer.Networking.Game.World;

namespace OpenConquer.Client.World;

internal readonly record struct WorldHeroItem(uint Id, uint TypeId, ushort Durability,
    byte AdditionLevel, byte EnchantmentLifeBonus);

internal sealed class WorldHeroEquipment
{
    private const int InventoryCapacity = 40;
    // 1009 subtype 46 offset->slot map, ascending (CP2.3b audit 0x6EC20A..0x6EC458; native call order is 5-then-4, 16-then-15).
    private static ReadOnlySpan<byte> SnapshotSlots => [1, 2, 3, 4, 5, 6, 7, 8, 9, 15, 16, 17, 18];

    private readonly Dictionary<uint, WorldHeroItem> _inventory = [];
    private readonly WorldHeroItem?[] _tracked = new WorldHeroItem?[30];
    private readonly WorldHeroItem?[] _active = new WorldHeroItem?[19];

    internal WorldHeroItem? GetActiveItem(int slot) => slot is >= 1 and <= 18
        ? _active[slot] : throw new ArgumentOutOfRangeException(nameof(slot));

    /// <summary>Applies a 1008 record. Only add (1) and update (3) are consumed.</summary>
    internal bool Apply(GameItemRecord1008 record)
    {
        byte kind = record.Action;
        if (kind is not GameItemRecord1008.AddAction and not GameItemRecord1008.UpdateAction)
        {
            return false;
        }

        uint id = record.ItemId;
        if (id == 0 || record.ItemTypeId == 0)
        {
            throw new InvalidDataException("Item snapshot has an invalid identity.");
        }

        int previous = FindTrackedPosition(id);
        if (kind == GameItemRecord1008.UpdateAction && previous == 0 && !_inventory.ContainsKey(id))
        {
            return false;
        }

        byte position = record.Position;
        if (position is 19 or 20 or >= 30)
        {
            return false;
        }

        if (position == 0 && !_inventory.ContainsKey(id) && _inventory.Count >= InventoryCapacity)
        {
            throw new InvalidDataException("World inventory exceeds its slot capacity.");
        }

        WorldHeroItem item = new(id, record.ItemTypeId, record.Durability, record.AdditionLevel,
            record.EnchantmentLifeBonus);

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

    /// <summary>Applies a 1009 item mutation. Only subtypes 3, 5, 6, 17 and 46 are consumed.</summary>
    internal bool Apply(GameItem1009 packet)
    {
        ushort subtype = packet.Subtype;
        if (subtype is not (GameItem1009.RemoveItem or GameItem1009.EquipItem or GameItem1009.UnequipItem or
            GameItem1009.UpdateDurability or GameItem1009.EquipmentSnapshot))
        {
            return false;
        }

        uint itemId = packet.ParameterA;
        uint position = packet.ParameterB;
        switch (subtype)
        {
            case GameItem1009.RemoveItem:
                return RemoveItem(itemId);
            case GameItem1009.EquipItem:
                return EquipItem(packet, itemId, position);
            case GameItem1009.UnequipItem:
                return UnequipItem(position);
            case GameItem1009.UpdateDurability:
                return UpdateDurability(itemId, position);
            default:
                ReadOnlySpan<byte> slots = SnapshotSlots;
                for (int i = 0; i < slots.Length; i++)
                {
                    uint id = packet.GetSnapshotItemId(i);
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

    private bool EquipItem(GameItem1009 packet, uint itemId, uint position)
    {
        if (position is < 1 or >= 30 or 19 or 20 ||
            !_inventory.TryGetValue(itemId, out WorldHeroItem item))
        {
            return false;
        }

        int count = packet.ListCount;
        for (int index = 0; index < count; index++)
        {
            uint displacedId = packet.GetListedId(index);
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

        WorldHeroItem updated = item with
        {
            Durability = unchecked((ushort)durability),
        };
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
