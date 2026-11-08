# Client compatibility evidence index

This index maps the CP0.3 client citation backlog to the affected documents.
The linked sections identify directly observed behavior, application-specific
choices and proof that remains outstanding. `20_notes` is a separate research
workspace; the cited note paths are not files in this repository. Earlier
client versions and screenshots may orient research but do not establish
the 5517 behavior described here.

The application owns its security policy, resource limits, asynchronous
scheduling and OpenGL lifetime. Historical slice documents describe their
implementation at that point in the project; the
[architecture overview](../architecture/architecture.md) and
[current capabilities](../../README.md#current-capabilities) describe the
present boundaries.

| Document | Evidence scope |
| --- | --- |
| [native-graphics](native-graphics.md#evidence-and-scope) | Observed behavior and application design |
| [native-text](native-text.md#evidence-and-scope) | Observed behavior and application design |
| [server-dat](server-dat.md#evidence-and-scope) | Observed behavior and application design |
| [retail-5517-inventory](../content/retail-5517-inventory.md#evidence-and-scope) | Corpus and observed consumers |
| [STARTUP-001C](../startup/STARTUP-001C.md#evidence-and-scope) | Source assets and earlier implementation |
| [STARTUP-001D](../startup/STARTUP-001D.md#evidence-and-scope) | Source assets and earlier implementation |
| [STARTUP-001E](../startup/STARTUP-001E.md#evidence-and-scope) | Source assets and earlier implementation |
| [STARTUP-001F](../startup/STARTUP-001F.md#evidence-and-scope) | Selected identity and earlier implementation |
| [STARTUP-001H](../startup/STARTUP-001H.md#evidence-and-scope) | Application design |
| [STARTUP-001M](../startup/STARTUP-001M.md#evidence-and-scope) | Application decisions; earlier stage |
| [STARTUP-001P](../startup/STARTUP-001P.md#evidence-and-scope) | Source assets and earlier implementation |
| [STARTUP-001T](../startup/STARTUP-001T.md#evidence-and-scope) | Source assets and earlier implementation |
| [STARTUP-001X](../startup/STARTUP-001X.md#evidence-and-scope) | Source assets and earlier implementation |
| [STARTUP-002-world-activation](../startup/STARTUP-002-world-activation.md#evidence-and-scope) | Observed behavior and application design |
| [WORLD-001-map-cell-grid](../world/WORLD-001-map-cell-grid.md#evidence-and-scope) | Format and validation bounds |
| [WORLD-002-scene-sections](../world/WORLD-002-scene-sections.md#evidence-and-scope) | Format and parser divergence |
| [WORLD-003-map-archive](../world/WORLD-003-map-archive.md#evidence-and-scope) | Archive dispatch and validation bounds |
| [WORLD-004-map-content-loading](../world/WORLD-004-map-content-loading.md#evidence-and-scope) | Selected identity and earlier implementation |
| [WORLD-006-puzzle-animation-binding](../world/WORLD-006-puzzle-animation-binding.md#evidence-and-scope) | Puzzle behavior and cache decisions |
| [WORLD-007-dds-compression](../world/WORLD-007-dds-compression.md#evidence-and-scope) | Decoder and corpus |
| [WORLD-009-puzzle-layer-drawing](../world/WORLD-009-puzzle-layer-drawing.md#evidence-and-scope) | Draw behavior and earlier implementation |
| [WORLD-010-scenery-ani-binding](../world/WORLD-010-scenery-ani-binding.md#evidence-and-scope) | Binding and validation |
| [WORLD-011-camera-and-puzzle-frames](../world/WORLD-011-camera-and-puzzle-frames.md#evidence-and-scope) | Projection and earlier implementation |
| [WORLD-012-terrain-scene-resources](../world/WORLD-012-terrain-scene-resources.md#evidence-and-scope) | Format and validation |
| [WORLD-013-terrain-scene-drawing](../world/WORLD-013-terrain-scene-drawing.md#evidence-and-scope) | Draw and timing boundaries |
| [WORLD-014-world-scene-activation](../world/WORLD-014-world-scene-activation.md#evidence-and-scope) | Frame gate and world lifetime |
| [WORLD-015-role-geometry](../world/WORLD-015-role-geometry.md#evidence-and-scope) | Mesh container and validation bounds |
| [WORLD-016-phy-geometry](../world/WORLD-016-phy-geometry.md#evidence-and-scope) | Geometry and application reader |
| [WORLD-017-c3-motion](../world/WORLD-017-c3-motion.md#evidence-and-scope) | Motion formats and validation bounds |
| [WORLD-018-phy-deformation](../world/WORLD-018-phy-deformation.md#evidence-and-scope) | Deformation and buffer ownership |
| [WORLD-019-indexed-mesh-submission](../world/WORLD-019-indexed-mesh-submission.md#evidence-and-scope) | Mesh submission and OpenGL |
| [WORLD-020-character-mesh-world-depth](../world/WORLD-020-character-mesh-world-depth.md#evidence-and-scope) | Camera/depth; scoped role equivalence |
| [WORLD-021-local-role-playback-and-world-presentation](../world/WORLD-021-local-role-playback-and-world-presentation.md#evidence-and-scope) | Resource evidence and playback |
| [WORLD-022-initial-character-parts](../world/WORLD-022-initial-character-parts.md#evidence-and-scope) | Appearance evidence; worker trace outstanding |
| [WORLD-023-authenticated-world-ingress](../world/WORLD-023-authenticated-world-ingress.md#evidence-and-scope) | Wire behavior and session lifetime |
| [WORLD-024-ground-walk-transport](../world/WORLD-024-ground-walk-transport.md#evidence-and-scope) | Movement wire and transport |
| [WORLD-025-first-world-hero-attributes](../world/WORLD-025-first-world-hero-attributes.md#evidence-and-scope) | Hero field semantics |
| [WORLD-026-initial-equipment-state](../world/WORLD-026-initial-equipment-state.md#evidence-and-scope) | Item identity and supported scope |
| [WORLD-028-first-world-experience](../world/WORLD-028-first-world-experience.md#evidence-and-scope) | Progression and HUD ownership |

## Outstanding boundaries

- **World movement and rendering.** Ordinary local walk echoes and action 108 are supported.
  Role interpolation, broader actions, remote entities and exact runtime depth across arbitrary
  scenes require their own implementation and evidence. [Multiplayer world epic](https://github.com/berniemackie97/OpenConquerClient/issues/285).
- **Map format and visibility.** The archived map 1999 layout and the source reader diverge.
  Unclassified fields, adopted-child ordering and arbitrary scene visibility retain the bounds
  in [scene sections](../world/WORLD-002-scene-sections.md) and [terrain drawing](../world/WORLD-013-terrain-scene-drawing.md).
- **Item state.** The active equipment projection does not imply complete inventory semantics.
  Two-handed displacement and snapshot ownership have their own issues.
  [Displacement](https://github.com/berniemackie97/OpenConquerClient/issues/262),
  [snapshot ownership](https://github.com/berniemackie97/OpenConquerClient/issues/264).
- **Correction reply.** Action 146 after action 108 remains tracked separately.
  [Correction reply](https://github.com/berniemackie97/OpenConquerClient/issues/263).
- **HUD and audio.** Control input/drawing, source media and real-driver captures establish
  scoped behavior. Gameplay effects, remaining tooltips and hints, and world audio remain
  in the [presentation epic](https://github.com/berniemackie97/OpenConquerClient/issues/286).
- **Credentials and trust.** The 16-byte retail server-name field and the deployed managed
  1–15 ASCII realm policy are distinct. Credential exchanges remain signed local IPv4
  loopback. [Packet 1060](../startup/STARTUP-001L.md),
  [login flow](../networking/FLOW-001A-live-login-bootstrap.md).

## Research-note discrepancies and current authority

The `20_notes` research archive is maintained outside this repository. These
entries resolve its older statements against its own later recorded checks;
they do not change packet code or claim new executable observations.
When consulting older notes, use the narrower claims below.

| Source note | Earlier statement | Current interpretation | Status |
| --- | --- | --- | --- |
| `Network/00_protocol_overview.md`, dispatch table and CP0.1 class-proof section | Packet 1009 appears as `CMsgItem ✓TU`, but the associated assert-path row names `Msg1008`. The rename history uses `CMsgItem` for 1009. | The Process routine is associated with packet **1009**, while its assert file is **`Msg1008.cpp`**. A file-path match establishes the source file, not an independently verified C++ class declaration. `CMsgItem` remains the applied analysis label; do not promote it to a separately proven class name or confuse it with packet 1008. | Packet/Process association and TU path verified in the research registry; class declaration unverified |
| `Server/tq-server-binaries-2008.md`, opening summary versus CP0.2 era section | The opening calls the 15.10 binary set a `≤5095`-era source. | The inspected build has an October 21, 2008 compiler date and a `CQServer-15.10-1021` source-root marker. The exact game-build era is **unknown**; neither `≤5095` nor `~518x` is established. This server set cannot override observations of the target client. | Calendar/build metadata verified; game-build-era classification unresolved |
| `Rendering/hero-model-cp22.md`, earlier CP2.2 residual against later CP2.3b resolution | Historical text describes deferred snapshot clamps and a postponed slot-order audit even after the note marks CP2.3b resolved. | The signed life clamp now occurs synchronously in subtype-46 handling, as accepted in [CP2.3 #239](https://github.com/berniemackie97/OpenConquerClient/issues/239) and merged [PR #260](https://github.com/berniemackie97/OpenConquerClient/pull/260). Packet field layout, equipment-slot mapping and handler operation order are different claims; a verified slot map does not establish displaced-item ownership. | Clamp and mapping slices accepted; [two-handed displacement #262](https://github.com/berniemackie97/OpenConquerClient/issues/262) and [tracked-item ownership #264](https://github.com/berniemackie97/OpenConquerClient/issues/264) remain open |

This record does **not** amend the external archive or its analysis database.
The original note text should be corrected in that workspace at its next
controlled revision. Until then, use this record when a stale heading,
class label or implementation-era statement conflicts with a later verified
section. See [tracking task #257](https://github.com/berniemackie97/OpenConquerClient/issues/257).
