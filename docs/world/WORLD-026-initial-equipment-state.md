# WORLD-026: Initial equipment state

The authenticated world-entry hydration contains 1008 item rows and 1009
equipment snapshots. A subtype-1 item row with a tracked placement establishes
the item identity and mutable durability, addition level and enchantment inputs.
For ordinary equipment slots, a row does not by itself activate the item.

1009 subtype 46 resolves thirteen slot UIDs against tracked item identities.
Offsets 32 through 64 activate slots 1 through 9, and offsets 68 through 80
activate slots 15 through 18. A zero or unresolved UID clears the active slot. Placements
10 through 12 and 15 through 18 activate their items directly on the subtype-1
path; the snapshot can subsequently replace slots 15 through 18. Inventory items, inspected-item
rows and unrelated 1009 subtypes do not activate local equipment.

The initial character-part resolver reads this same equipment state instead of
parsing the hydration frames independently. Unresolved active equipment therefore
does not prevent the world from using its base body and linked-hair appearance.

The same bounded equipment store now receives live world packets. Item message
1008 subtype 1 creates a tracked row, subtype 3 updates an existing tracked
item's fields or placement, and system message 1009 subtype 3 removes an item
by UID. System message 1009 subtype 46 replaces the active UID projection.
All mutations are processed in received order on the world render thread.

Durability, addition and enchantment updates become visible to the effective
stat projection in the next frame. A changed equipped headwear or armor source
replaces the local character render resources after the new resources load,
so an unsuccessful load cannot discard the previous renderer. Subtype-46
snapshots also clamp current life against the recalculated effective maximum.
These operations remain scoped to authenticated local equipment.

Other system-message item routing, soul-item/refinement state, item-status
mutations and complete inventory state are not implemented. They require
their own bounded state and wire-transaction integration.
