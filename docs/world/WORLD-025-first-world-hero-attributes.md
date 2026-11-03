# WORLD-025: First-world hero attribute state

The authenticated character-information message initializes the local hero
before the map-entry request sequence. Its current life, current mana,
experience, profession, level, and attributes are direct values, not
derived gauge ranges. Current stamina and the optional life/mana overrides
start at zero.

After world entry, attribute message 10017 applies a bounded array of
20-byte records in wire order. The packet's entity ID determines its
recipient. Local records update the hero state before world preparation
and rendering in the same frame. Remote attributes require a separate
entity owner and must not change the local hero. An invalid record count
is rejected before any mutation.

The initial implemented direct writers are life (0), life override (1),
mana (2), mana override (3), experience (5), profession (7),
stamina (8), unspent points (10), level (12), rebirth count (22),
status flags (25), skill-gauge percentage (27, capped at 100), and the
map-overlay life bonus (43). Unknown identifiers remain diagnosed, not
mapped to unrelated fields. The full 128-bit status mask is preserved.

This stage does not publish derived maximum life/mana or HUD gauges.
Their verified formulas depend on equipped item modifiers, map flags,
appearance transformation, and status. Current life or mana must never
be substituted for those maxima. The subsequent HUD binding must apply
equipment and status inputs in their actual order before updating gauge
ranges and followers.

The map-1010 fixture has 20 terrain scenery records and 18 animated
scenery records, with no sound-emitter record. Other maps can contain
sound emitters loaded when the map is constructed; audio behavior for
those maps remains an explicit unimplemented component. Earlier source
implementations that schedule repeated map-sound playback are not the
contract for this client.
