# WORLD-026: Initial equipment state

The authenticated world-entry hydration contains 1008 item rows and 1009
equipment snapshots. A subtype-1 item row with a tracked placement establishes
the item identity and mutable durability, addition level and enchantment inputs.
For ordinary equipment slots, a row does not by itself activate the item.

1009 subtype 46 resolves thirteen slot UIDs against the tracked item positions,
in packet order. A zero or unresolved UID clears the active slot. Placements
10 through 12 and 15 through 18 activate their items directly on the subtype-1
path; the snapshot can subsequently replace slots 15 through 18. Inventory items, inspected-item
rows and unrelated 1009 subtypes do not activate local equipment.

The initial character-part resolver reads this same equipment state instead of
parsing the hydration frames independently. Unresolved active equipment therefore
does not prevent the world from using its base body and linked-hair appearance.

This stage owns the initial equipment snapshot only. Live 1008/1009 equipment
mutations, base item-type and ItemAdd stat lookup, refinement effects, and the
effective HP/MP projection remain separate dependencies. The HUD must not use
current HP/MP as inferred maximum values.
