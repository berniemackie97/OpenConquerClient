using OpenConquer.Networking.Game.World;

namespace OpenConquer.Client.World;

internal sealed class WorldHeroEquipment
{
    private const int InventoryCapacity = 40;
    private static ReadOnlySpan<byte> SnapshotSlots => [1, 2, 3, 4, 5, 6, 7, 8, 9, 15, 16, 17, 18];

    private readonly Dictionary<uint, WorldHeroItem> _inventory = [];
    private readonly WorldHeroItem?[] _tracked = new WorldHeroItem?[30];
    private readonly WorldHeroItem?[] _active = new WorldHeroItem?[19];

    internal WorldHeroItem? GetActiveItem(int slot)
    {
        return slot is >= 1 and <= 18 ? _active[slot] : throw new ArgumentOutOfRangeException(nameof(slot));
    }

    internal bool Apply(GameItemRecord1008 record)
    {
        if (record.ItemId == 0 || record.ItemTypeId == 0)
        {
            return false;
        }

        return record.Action switch
        {
            GameItemRecord1008.AddAction => Add(record),
            GameItemRecord1008.UpdateAction => Update(record),
            _ => false,
        };
    }

    private bool Add(GameItemRecord1008 record)
    {
        uint id = record.ItemId;
        byte position = record.Position;

        if (position != 0 && !IsTrackablePosition(position))
        {
            return false;
        }

        if (position == 0 && (_inventory.ContainsKey(id) || (_inventory.Count >= InventoryCapacity && !IsArrow(record.ItemTypeId))))
        {
            return false;
        }

        WorldHeroItem item = new(record);
        int previous = FindTrackedPosition(id);

        if (previous != 0 && previous != position)
        {
            _tracked[previous] = null;
        }

        if (position == 0)
        {
            _inventory[id] = item;
        }
        else
        {
            _inventory.Remove(id);
            Track(position, item);
        }

        return true;
    }

    private bool Update(GameItemRecord1008 record)
    {
        uint id = record.ItemId;
        if (_inventory.TryGetValue(id, out WorldHeroItem? inventoryItem))
        {
            inventoryItem.UpdateStats(record);
            return true;
        }

        int previous = FindTrackedPosition(id);
        if (previous == 0 || _tracked[previous] is not { } item)
        {
            return false;
        }

        byte position = record.Position;
        item.UpdateStats(record);
        if (!IsTrackablePosition(position))
        {
            return true;
        }

        if (position != previous)
        {
            _tracked[previous] = null;
        }

        Track(position, item);

        return true;
    }

    private void Track(int position, WorldHeroItem item)
    {
        _tracked[position] = item;
        if (position is (>= 10 and <= 12) or (>= 15 and <= 18))
        {
            _active[position] = item;
        }
    }

    private static bool IsTrackablePosition(uint position) => position is (>= 1 and <= 18) or (>= 21 and <= 29);

    private static bool IsArrow(uint typeId) => typeId % 10_000_000 / 100_000 == 10 && typeId % 100_000 / 10_000 == 5;

    internal bool Apply(GameItem1009 packet)
    {
        ushort subtype = packet.Subtype;
        if (subtype is not (GameItem1009.RemoveItem or GameItem1009.EquipItem or GameItem1009.UnequipItem or GameItem1009.UpdateDurability or GameItem1009.EquipmentSnapshot))
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
        if (position == 0)
        {
            return removed;
        }

        _tracked[position] = null;
        ClearActive(id);
        removed = true;

        return removed;
    }

    private bool EquipItem(GameItem1009 packet, uint itemId, uint position)
    {
        if (position is < 1 or >= 30 or 19 or 20 || !_inventory.TryGetValue(itemId, out WorldHeroItem? item))
        {
            return false;
        }

        int count = packet.ListCount;
        for (int index = 0; index < count; index++)
        {
            uint displacedId = packet.GetListedId(index);
            int displacedPosition = FindTrackedPosition(displacedId);
            if (displacedPosition == 0 || _tracked[displacedPosition] is not { } displaced)
            {
                continue;
            }

            _tracked[displacedPosition] = null;
            _inventory[displacedId] = displaced;
            ClearActive(displacedId);
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
        if (position is < 1 or >= 30 or 19 or 20 || _tracked[position] is not { } item)
        {
            return false;
        }

        _tracked[position] = null;
        _inventory[item.Id] = item;
        ClearActive(item.Id);

        if (position is not (4 or 24) || item.TypeId % 10_000_000 / 100_000 != 5 || item.TypeId % 100_000 / 1000 != 0)
        {
            return true;
        }

        int pairedPosition = (int)position + 1;
        if (_tracked[pairedPosition] is not { } paired)
        {
            return true;
        }

        _tracked[pairedPosition] = null;
        _inventory[paired.Id] = paired;
        ClearActive(paired.Id);

        return true;
    }

    private bool UpdateDurability(uint durability, uint position)
    {
        if (position is < 1 or >= 30 or 19 or 20 || _tracked[position] is not { } item)
        {
            return false;
        }

        return item.SetDurability(unchecked((ushort)durability));
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
