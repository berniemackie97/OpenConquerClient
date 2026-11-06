using OpenConquer.Networking.Game.World;

namespace OpenConquer.Client.World;

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

    /// <summary>
    /// Applies a 1008 record. Only add (1) and update (3) are consumed. A record native skips is skipped: UID or type 0
    /// fails <c>CItem_InitIdentityFromItemType</c> 0x67AD6B. The item-table lookup belongs to <see cref="WorldHeroModel"/>.
    /// </summary>
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

    // Native kind 1: positions 1..18 and 21..29 are tracked (CHeroItem__TrackItemAtPosition 0x6DB042/0x6DB050); other
    // nonzero positions keep the item nowhere. Position 0 goes through CHeroItem__TryAddInventoryItem, which refuses a
    // UID already in the inventory (0x6D9399) and a 41st item that is not an arrow (0x6D936B). Native can also leave a
    // second reference at an old position; this store keeps one location per UID.
    private bool Add(GameItemRecord1008 record)
    {
        uint id = record.ItemId;
        byte position = record.Position;
        if (position != 0 && !IsTrackablePosition(position))
        {
            return false;
        }

        if (position == 0 && (_inventory.ContainsKey(id) ||
            (_inventory.Count >= InventoryCapacity && !IsArrow(record.ItemTypeId))))
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

    // Native kind 3 (0x763570..0x7638A4): finds the UID in the inventory, then the tracked items; unknown UIDs are
    // skipped. Fields are rewritten in place. An inventory item ignores +0x12. A tracked item is re-tracked at +0x12
    // (0x763867), which is a no-op for a position TrackItemAtPosition does not accept, including 0.
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
        if (IsTrackablePosition(position))
        {
            if (position != previous)
            {
                _tracked[previous] = null;
            }

            // Native always re-tracks, even the same position (0x763867), and retains matching active pointers.
            Track(position, item);
        }

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

    // CItem_IsArrow 0x5681F9: equipment category 0 (id % 10^7 / 10^5 == 10, 0x67AF1A) and subtype 50000
    // (id % 10^5 / 10^4 * 10^4, 0x67B0B3).
    private static bool IsArrow(uint typeId) => typeId % 10_000_000 / 100_000 == 10 && typeId % 100_000 / 10_000 == 5;

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
            !_inventory.TryGetValue(itemId, out WorldHeroItem? item))
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
