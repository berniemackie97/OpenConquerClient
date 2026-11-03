# WORLD-027: Initial effective stats and HUD activation

The item-type and item-addition tables are loaded from their verified source
files through the packaged-content reader. The item-type decoder applies the
128-byte seeded rotation and reads the signed life/mana fields; addition records
supply the unsigned life word under a two-part key. These source files belong in
the regular content closure, not as embedded copies or runtime provenance hashes.

The effective-life formula uses attributes, profession percentage, equipped
item contributions, map flags, transform state, overlay and life-tier inputs.
The mana formula has its distinct profession and map-override gates. The initial
equipment contribution includes static base life/mana, addition-level life and
enchantment. Broken equipment contributes mana but not life.

On world frames the live local hero updates the existing HUD's HP, MP, stamina
and skill gauges. Life and mana followers start at zero and use the recorded
per-frame step of max/80 (or one below 80), snapping immediately on a decrease.
The extended physical-force bar uses status bit 33.

The initial renderer projection is restricted to characters whose admission
snapshot has no transformation look source. A completed transform lookup is
needed before opening that branch. The lifetime of status and equipment state
remains tied to the authenticated world session.

Still missing: refinement and soul-item mutations, nonzero packed life tier
from world state, full transformation-state updates, and unhandled item
routing/status subtypes. Live item create/update/removal and active-equipment
snapshot inputs update equipped statistics and appearance, including the
post-snapshot current-life cap. The level-specific experience maximum is
supplied by the progression table in WORLD-028. The remaining gaps must
be closed before declaring complete gameplay vitals production-ready.

The thirteen equipment snapshot UIDs follow packet offsets 32 through 80
in ascending order and activate slots 1 through 9, then 15 through 18.
The right-hand and right-accessory entries precede their left counterparts.
