# WORLD-026: Initial equipment state

## Evidence and scope

Item identity, packet 1008 and packet 1009 equipment behavior are documented in `20_notes/Network/MsgItem1008-domain-contract-5517.md` and `inbound-tolerance-cp24.md`. The supported identity store does not establish all inventory mutations or two-handed displacement handling.

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

The same equipment store receives live world packets. Item 1008
subtype 1 creates a new item instance; subtype 3 updates an existing instance.
An inventory update ignores the incoming placement. A tracked update uses an
accepted placement (1..18 or 21..29); an unsupported placement leaves its current
tracking intact while still updating its fields. System message 1009 subtype 3 removes an
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

The inventory UID table skips a new ordinary inventory item at forty entries;
native arrows are exempt. It also skips a UID already in inventory, preserving
the existing instance. These are native admission refusals, not disconnects
(`CHeroItem__TryAddInventoryItem`, `0x6D935F..0x6D9399`). Arrow classification
uses category/subtype helpers (`0x5681F9`, `0x67AF1A`, `0x67B0B3`). The store
serves equipment transactions only: it is not a full bag,
stack, slot-layout or item-interaction implementation. Equipment
set-switch notifications, refinement and soul-item updates, other item
status mutations, and outbound equipment requests are still outstanding.

## Identity and reference behavior — CP2.4b

UID 0, type 0 and types missing from ItemType.dat are skipped before world-model
mutation (`CItem_InitIdentityFromItemType`, `0x67AD6B`; lookup failure
`0x679197..0x6791C6`). A failed type change preserves the existing item and equipment
revision. Table validation belongs to WorldHeroModel; WorldHeroEquipment owns
placement and item-instance mutation. Neither boundary loads content or renders.

Native Add allocates a fresh CItem (`0x762D84/0x762D9C`), while Update mutates the
located object (`0x763570..0x7638A4`). WorldHeroItem therefore has reference identity
and mutable stat inputs. Changing tracked placement preserves active references to
that same instance (`0x6DB0C0/0x6DB0E7`). Every accepted tracked update re-tracks,
including the same position (`0x763867`), allowing a special slot to reactivate
after a snapshot cleared it. A fresh Add at an ordinary/alternate tracked position
sharing an old active UID leaves the old active instance intact until a snapshot
selects the new tracked instance. Direct-activation placements replace their
corresponding special slot immediately.
Durability updates mutate the located instance, including its active references.

The current store still keeps one tracked location per UID and normalizes
inventory/tracked UID overlap. Native can retain older tracked keys. This broader
lookup/removal ownership gap is parked in issue #264; two-handed displacement is
separately tracked in #262. Full item parity is not claimed. Native evidence and
the CP2.4b reference audit are in `20_notes/Network/inbound-tolerance-cp24.md` and
`Network/evidence/handler-tolerance-2026-11-06/` (static byte verification only).
