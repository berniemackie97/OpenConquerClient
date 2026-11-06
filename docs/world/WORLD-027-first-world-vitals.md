# WORLD-027: Initial effective stats and HUD activation

Native authority: `20_notes/Rendering/14_hero_effective_max_life_mana.md`,
`13_hud_progress_gauges.md` and the CP2.2 revalidation in
`hero-model-cp22.md`. Effective life/mana are `0x65D06D`/`0x65D25A`;
their equipment loops are `0x65D12B..0x65D1C3`/`0x65D2EC..0x65D309`.

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
The extended physical-force bar uses bit 33 of the low status qword
(`0x66F766`, status indexing `0x6E50D4`). The supported initialized maximum is
100 (`0x6570A9`), increased to 150 by the unsigned multiply/shift at `0x66F792`.
Current stamina uses a signed upper-only cap (`0x66F77C`); dynamic maximum-stamina
inputs remain unsupported. Follower arithmetic is signed and wrapping
(`0x65E128..0x65E167`, `0x65E1C9..0x65E24C`). Skill attribute 27 is capped at
100 before storage (`0x757C10..0x757C1F`).

The initial renderer projection is restricted to characters whose admission
snapshot has no transformation look source. A completed transform lookup is
needed before opening that branch. The lifetime of status and equipment state
remains tied to the authenticated world session.

Still missing: refinement and soul-item mutations, nonzero packed life tier
from world state, full transformation-state updates, equipment-set switches
and additional item status subtypes. Local item create/update, equipment
equip/unequip/removal, durability and active-equipment snapshots update the
equipped statistics and appearance, including the post-snapshot current-life cap. The level-specific experience maximum is
supplied by the progression table in WORLD-028. The remaining gaps must
be closed before declaring complete gameplay vitals production-ready.

The thirteen equipment snapshot UIDs follow packet offsets 32 through 80
in ascending order and activate slots 1 through 9, then 15 through 18.
The right-hand and right-accessory entries precede their left counterparts.

## Hero model boundary — CP2.2

| Component | Responsibility |
| --- | --- |
| `WorldInboundState` | `IGameWorldPacketHandler` for typed world frames; replays hydration item frames in order (CP2.3). |
| `WorldHeroModel` | World-lifetime hero attributes, equipment/revision, followers and the bound item table/map flags; clamps on the 1009 subtype-46 snapshot and advances vitals through `AdvanceVitals`. |
| `WorldHeroMaxStats` / `WorldHeroItemStats` | Pure stat calculations over attributes, equipment and already-parsed item tables. |
| `WorldHeroVitals` | Resolved model values, without HUD or graphics types. |
| `ClientWorld` | Supplies current context and parsed tables, then maps resolved values to HUD input. |

`AdvanceVitals` performs no rendering or content I/O. Item/experience tables are
read-only inputs; experience lookup supplies the level requirement. Model state
is used on the existing desktop frame thread and discarded with the world. No
new tasks, resource lifetimes or cross-thread mutation are introduced.

The model is still advanced once at the existing guarded draw point, after
`scene.Draw`, when the HUD, tables and untransformed admission state are ready.
Moving that call to every update would change follower cadence and loading/hidden
frame behavior. Content loading and draw timing are unchanged by this extraction.

Native 1009 subtype 46 compares current life and effective maximum as signed
int32, without a nonnegative-maximum guard (`0x6EC49A/0x6EC49C`). An actual clamp
sets both current life and the HP follower to maximum (`0x6EC4B8/0x6EC4C7`);
equal/below cases do not reset the follower, and mana is unaffected. The model now
matches these rules at its existing deferred consumption boundary.

## Snapshot clamp order — CP2.3

`ClientWorld` loads the item tables before it builds world state; a load failure
closes the world as content-unavailable. The model then clamps inside the 1009
subtype-46 handler, after the equipment slots change, as native does at
`0x6EC479..0x6EC4C7`. Hydration snapshots clamp against the admission life, and a
later life write in the same ingress batch is never clamped. There is no pending
flag, queue or extra buffering. While a transform-look source is active the clamp
is skipped, because that maximum-life input is not modeled; CP2.2 never consumed
the clamp in that state either.

The snapshot offset-to-slot map is ascending (`+0x2C`→4, `+0x30`→5, `+0x44`→15,
`+0x48`→16). Native calls `CHeroItem__SetEquipSlot` 5 before 4 and 16 before 15.
The audit and the open two-handed displacement question are in
`20_notes/Network/typed-world-dispatch-cp23.md`.

`WorldHeroModelTests` adds 12 cases, including static native-instruction golden
vectors and encoded item/progression composition. Fixture provenance and limits
are in `tests/OpenConquer.Client.Tests/Fixtures/Hero/README.md`. Local SDK 10.0.403
validation: locked restore, Release build with zero warnings/errors, format
verification, and 2,832 focused tests (Client 1,497; Content 750; Rendering 499;
Platform 86). These checks do not establish native runtime or interactive client
smoke-test parity.
