# STARTUP-002: World activation and frame presentation

The main client frame loop runs at a 25 ms cadence, but its world stages
remain disabled until the login/role-creation branch has completed. Login
progress callbacks are visual state, not permission to enter the world.
Account authentication, game admission and the map-entry exchange precede
world activation.

| Phase | Observed contract | Current implementation |
| --- | --- | --- |
| Data preparation | Packages, resource indexes, role-part databases and resource workers initialize before the shell's normal frame loop | Package registration precedes the logo; resource indexes and meshes are resolved on demand |
| Startup presentation | Logo, interactive login stage and its own background music and control sounds | Splash then login stage, with OpenAL startup playback |
| Character admission | Existing-character and create-character flows are distinct; only admitted existing or newly created characters may enter the map | Authenticated world-entry snapshot and separate role-creation flow |
| World-frame gate | Startup close/enable transition follows verified world-ready state, not progress text | Content load, incremental world texture preparation and loading-state gate precede the first world draw |
| World frame | Network intake precedes world component updates and map/role rendering; foreground interface renders after the scene | Prepared map, local body and HUD render in order; the continuous world packet and component update loop is outstanding |
| Sound ownership | Login music ends on world transition; map scenery audio instances start once when loaded and update position with the view | Startup playback is paused for world visibility; map ambience and world effects are outstanding |
| Content package ownership | Package declarations resolve archived sources before the game shell loads | Import consumes package declarations; curated runtime content excludes those declarations and keeps the character-mesh database archive |
| Exit | Reset disables world rendering and releases session-scoped resources | World scene, character graphics, HUD and world framebuffer are released on return and context teardown |

This slice activates an initial idle clip and gameplay HUD without claiming
complete launch-to-world parity. Current HUD appearance lacks all the
authoritative gameplay state needed to populate health, mana, experience and
skills. Character action switching, facing, attachments and world sound
ownership still need dedicated implementation and running-client visual/audio
comparison. The original content source's resource closure must be imported
before the character's C3 geometry can render.

The asset index contains a recorded idle clip key `2_000_100` for body
source `2`. The separately observed role-action request builder uses a
different composite key, so action-request translation must be reconciled
through the full resource request path before generalizing the initial clip
lookup to arbitrary actions.
