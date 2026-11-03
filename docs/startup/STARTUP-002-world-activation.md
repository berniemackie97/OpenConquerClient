# STARTUP-002: World activation and frame presentation

The main client frame loop runs at a 25 ms cadence, but its world stages
remain disabled until the login/role-creation branch has completed. Login
progress callbacks are visual state, not permission to enter the world.
Account authentication, game admission and the map-entry exchange precede
world activation.

| Phase | Observed contract | Current implementation |
| --- | --- | --- |
| Data preparation | Packages, resource indexes, role-part databases and resource workers initialize before the shell's normal frame loop | Package registration precedes the logo; required C3 resources retain package-only lookup while most other artwork is loaded from extracted content |
| Startup presentation | Logo, interactive login stage and its own background music and control sounds | Splash then login stage, with OpenAL startup playback |
| Character admission | Existing-character and create-character flows are distinct; only admitted existing or newly created characters may enter the map | Authenticated world-entry snapshot and separate role-creation flow |
| World-frame gate | Startup close/enable transition follows verified world-ready state, not progress text | Content load, incremental world texture preparation and loading-state gate precede the first world draw |
| World frame | Network intake precedes world component updates and map/role rendering; foreground interface renders after the scene | Continuous bounded network intake handles selected packet types; map, local character and HUD render, while other world entities and gameplay updates remain unsupported |
| Vitals | Life, mana, stamina, skill and experience gauges consume current character state and their effective ranges | Life has been observed during a live session; mana and experience need nonzero-value smoke tests, and stamina/skill lack complete authoritative server updates |
| Sound ownership | Login music ends on world transition; map scenery audio instances start once when loaded and update position with the view | Startup playback is paused for world visibility; map ambience and world effects are outstanding |
| Exit | Reset disables world rendering and releases session-scoped resources | World scene, character graphics, HUD and world framebuffer are released on return and context teardown |

The visible initial character and HUD do not establish complete
startup-to-world behavior. Mana and experience have not passed nonzero-value
smoke tests. Stamina, skill updates, live item mutations, functional HUD
actions, character motion and world audio remain open. A verified content set
proves asset integrity, not rendering or audio parity on a running device.
An absent optional source archive is not an error by itself: the actual
package-only resource lookup remains authoritative.

The asset index contains a recorded idle clip key `2_000_100` for body
source `2`. The separately observed role-action request builder uses a
different composite key, so action-request translation must be reconciled
through the full resource request path before generalizing the initial clip
lookup to arbitrary actions.
