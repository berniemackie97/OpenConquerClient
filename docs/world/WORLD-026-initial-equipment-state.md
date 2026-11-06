# WORLD-026: Initial equipment state

The authenticated world-entry hydration contains 1008 item rows and 1009
equipment snapshots. A subtype-1 item row with a tracked placement establishes
the item identity and mutable durability, addition level and enchantment inputs.
For ordinary equipment slots, a row does not by itself activate the item.

1009 subtype 46 resolves thirteen slot UIDs against tracked item identities.
Offsets 32 through 64 activate slots 1 through 9, and offsets 68 through 80
activate slots 15 through 18. A zero or unresolved UID clears the active slot. Placements
10 through 12 and 15 through 18 activate their items directly on the subtype-1
path; the snapshot can subsequently replace slots 15 through 18. Inventory items do not activate equipment until an equipment packet moves
them to a slot. Inspected-item rows and unrelated 1009 subtypes never
activate the authenticated hero's equipment.

The initial character-part resolver reads this same equipment state instead of
parsing the hydration frames independently. Unresolved active equipment therefore
does not prevent the world from using its base body and linked-hair appearance.

The same bounded equipment store receives live world packets. Item 1008
subtype 1 creates an inventory or tracked row; subtype 3 updates an existing
item and can change its placement. System message 1009 subtype 3 removes an
item by UID, subtype 5 moves a known inventory item to an equipment position
and returns listed displaced items to inventory (the +20 count is unsigned;
entries past the message are UID 0 and the list stops at 235 entries, the
0x400 buffer edge), subtype 6 unequips by
position, and subtype 17 updates durability for the tracked position.
Subtype 46 reconciles the active UID projection. Equipped two-handed bows
also clear their paired off-hand position on unequip. State changes are
applied in received order on the world render thread.

Durability, addition and enchantment updates become visible to the effective
stat projection in the next frame. Equipped headwear, armor and hand-item sources
participate in the local character's render-source identity. When it changes,
replacement resources load before the previous renderer is released.
Hand sources bind to the body animation's right/left weapon attachment tracks.
The right-hand 500 and 370 source families route to the left attachment; a
left-hand composite 105 source routes to the right attachment. Both are
evaluated before the six-digit part-source lookup. Subtype-46 snapshots also
clamp current life against the recalculated effective maximum.
These operations remain scoped to authenticated local equipment.

The inventory UID table enforces the forty-entry pack limit for new item
identities and serves equipment transactions only: it is not a full bag,
stack, slot-layout or item-interaction implementation. Equipment
set-switch notifications, refinement and soul-item updates, other item
status mutations, and outbound equipment requests are still outstanding.
