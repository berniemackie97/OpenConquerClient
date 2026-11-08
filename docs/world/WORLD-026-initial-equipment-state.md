# WORLD-026: Initial equipment state

## Evidence and scope

Item identity, packet 1008 and packet 1009 equipment behavior are documented in `20_notes/Network/MsgItem1008-domain-contract-5517.md` and `inbound-tolerance-cp24.md`. The supported identity store does not establish all inventory mutations or two-handed displacement handling.

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
