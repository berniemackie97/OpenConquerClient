# Client compatibility evidence index

This index maps the client citation backlog from CP0.3 to the affected documents.
Anchors refer to clean retail 5517 binaries, original assets and verified captures.
Addresses use the named binary's preferred image base. `20_notes` paths refer to
the separate native research workspace. Historical server material and screenshots
from other client versions can orient research. Retail behavior needs retail evidence.

The linked evidence sections identify which claims the cited source supports.
Managed ownership, security policies, input bounds, async scheduling and OpenGL
resource handling are product decisions. Historical slice notes describe what was
available when the slice landed. The [architecture overview](../architecture/architecture.md)
and [capabilities](../../README.md#current-capabilities) describe the current tree.

| Document | Evidence scope |
| --- | --- |
| [native-graphics](native-graphics.md#evidence-and-scope) | Retail and managed |
| [native-text](native-text.md#evidence-and-scope) | Retail and managed |
| [server-dat](server-dat.md#evidence-and-scope) | Retail and managed |
| [retail-5517-inventory](../content/retail-5517-inventory.md#evidence-and-scope) | Retail corpus and native |
| [STARTUP-001C](../startup/STARTUP-001C.md#evidence-and-scope) | Retail assets and historical managed |
| [STARTUP-001D](../startup/STARTUP-001D.md#evidence-and-scope) | Retail assets and historical managed |
| [STARTUP-001E](../startup/STARTUP-001E.md#evidence-and-scope) | Retail assets and historical managed |
| [STARTUP-001F](../startup/STARTUP-001F.md#evidence-and-scope) | Retail identity and historical managed |
| [STARTUP-001H](../startup/STARTUP-001H.md#evidence-and-scope) | Managed |
| [STARTUP-001M](../startup/STARTUP-001M.md#evidence-and-scope) | Managed and historical |
| [STARTUP-001P](../startup/STARTUP-001P.md#evidence-and-scope) | Retail assets and historical managed |
| [STARTUP-001T](../startup/STARTUP-001T.md#evidence-and-scope) | Retail assets and historical managed |
| [STARTUP-001X](../startup/STARTUP-001X.md#evidence-and-scope) | Retail assets and historical managed |
| [STARTUP-002-world-activation](../startup/STARTUP-002-world-activation.md#evidence-and-scope) | Retail and managed |
| [WORLD-001-map-cell-grid](../world/WORLD-001-map-cell-grid.md#evidence-and-scope) | Retail format and managed bounds |
| [WORLD-002-scene-sections](../world/WORLD-002-scene-sections.md#evidence-and-scope) | Retail format and managed divergence |
| [WORLD-003-map-archive](../world/WORLD-003-map-archive.md#evidence-and-scope) | Retail archive dispatch and managed bounds |
| [WORLD-004-map-content-loading](../world/WORLD-004-map-content-loading.md#evidence-and-scope) | Retail identity and historical managed |
| [WORLD-006-puzzle-animation-binding](../world/WORLD-006-puzzle-animation-binding.md#evidence-and-scope) | Retail PUZZLE and managed cache |
| [WORLD-007-dds-compression](../world/WORLD-007-dds-compression.md#evidence-and-scope) | Retail decoder and corpus |
| [WORLD-009-puzzle-layer-drawing](../world/WORLD-009-puzzle-layer-drawing.md#evidence-and-scope) | Retail drawing and historical managed |
| [WORLD-010-scenery-ani-binding](../world/WORLD-010-scenery-ani-binding.md#evidence-and-scope) | Retail binding and managed validation |
| [WORLD-011-camera-and-puzzle-frames](../world/WORLD-011-camera-and-puzzle-frames.md#evidence-and-scope) | Retail projection and historical managed |
| [WORLD-012-terrain-scene-resources](../world/WORLD-012-terrain-scene-resources.md#evidence-and-scope) | Retail format and managed validation |
| [WORLD-013-terrain-scene-drawing](../world/WORLD-013-terrain-scene-drawing.md#evidence-and-scope) | Retail draw and managed timing scope |
| [WORLD-014-world-scene-activation](../world/WORLD-014-world-scene-activation.md#evidence-and-scope) | Retail gate and managed world lifetime |
| [WORLD-015-role-geometry](../world/WORLD-015-role-geometry.md#evidence-and-scope) | Retail container and managed bounds |
| [WORLD-016-phy-geometry](../world/WORLD-016-phy-geometry.md#evidence-and-scope) | Retail geometry and managed reader |
| [WORLD-017-c3-motion](../world/WORLD-017-c3-motion.md#evidence-and-scope) | Retail motion formats and managed bounds |
| [WORLD-018-phy-deformation](../world/WORLD-018-phy-deformation.md#evidence-and-scope) | Retail deformation and managed ownership |
| [WORLD-019-indexed-mesh-submission](../world/WORLD-019-indexed-mesh-submission.md#evidence-and-scope) | Retail submission and managed GPU API |
| [WORLD-020-character-mesh-world-depth](../world/WORLD-020-character-mesh-world-depth.md#evidence-and-scope) | Retail camera and depth; scoped role parity |
| [WORLD-021-local-role-playback-and-world-presentation](../world/WORLD-021-local-role-playback-and-world-presentation.md#evidence-and-scope) | Retail resource evidence and managed playback |
| [WORLD-022-initial-character-parts](../world/WORLD-022-initial-character-parts.md#evidence-and-scope) | Retail visual and worker evidence |
| [WORLD-023-authenticated-world-ingress](../world/WORLD-023-authenticated-world-ingress.md#evidence-and-scope) | Retail wire and managed lifecycle |
| [WORLD-024-ground-walk-transport](../world/WORLD-024-ground-walk-transport.md#evidence-and-scope) | Retail movement wire and managed transport |
| [WORLD-025-first-world-hero-attributes](../world/WORLD-025-first-world-hero-attributes.md#evidence-and-scope) | Retail hero field semantics |
| [WORLD-026-initial-equipment-state](../world/WORLD-026-initial-equipment-state.md#evidence-and-scope) | Retail item identity and managed scope |
| [WORLD-028-first-world-experience](../world/WORLD-028-first-world-experience.md#evidence-and-scope) | Retail progression and managed ownership |

## Outstanding boundaries

- **World movement and rendering.** Ordinary local walk echoes and action 108 are supported.
  Role interpolation, broader actions, remote entities and exact runtime depth across arbitrary
  scenes require their own implementation and evidence. [Multiplayer world epic](https://github.com/berniemackie97/OpenConquerClient/issues/285).
- **Map format and visibility.** The archived map 1999 layout and the native reader diverge.
  Unclassified fields, adopted-child ordering and arbitrary scene visibility retain the bounds
  in [scene sections](../world/WORLD-002-scene-sections.md) and [terrain drawing](../world/WORLD-013-terrain-scene-drawing.md).
- **Item state.** The active equipment projection does not imply complete inventory semantics.
  Two-handed displacement and snapshot ownership have their own issues.
  [Displacement](https://github.com/berniemackie97/OpenConquerClient/issues/262),
  [snapshot ownership](https://github.com/berniemackie97/OpenConquerClient/issues/264).
- **Correction reply.** Native action 146 after action 108 remains tracked separately.
  [Correction reply](https://github.com/berniemackie97/OpenConquerClient/issues/263).
- **HUD and audio.** Control input/drawing, source media and real-driver captures establish
  scoped behavior. Gameplay effects, remaining tooltips and hints, and world audio remain
  in the [presentation epic](https://github.com/berniemackie97/OpenConquerClient/issues/286).
- **Credentials and trust.** The 16-byte retail server-name field and the deployed managed
  1–15 ASCII realm policy are distinct. Credential exchanges remain signed local IPv4
  loopback. [Packet 1060](../startup/STARTUP-001L.md),
  [login flow](../networking/FLOW-001A-live-login-bootstrap.md).
