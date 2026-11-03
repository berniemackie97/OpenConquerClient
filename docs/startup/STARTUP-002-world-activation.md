# STARTUP-002: World activation and frame presentation

The main client frame loop runs at a 25 ms cadence, but its world stages
remain disabled until the login/role-creation branch has completed. Login
progress callbacks are visual state, not permission to enter the world.
Account authentication, game admission and the map-entry exchange precede
world activation.

| Phase | Observed contract | Current implementation |
| --- | --- | --- |
| Data preparation | Packages, resource indexes, role-part databases and resource workers initialize before the shell's normal frame loop | Content is imported before launch; curated content does not register source-only archives, and meshes are resolved on demand |
| Startup presentation | Logo, interactive login stage and its own background music and control sounds | Splash then login stage, with OpenAL startup playback |
| Character admission | Existing-character and create-character flows are distinct; only admitted existing or newly created characters may enter the map | Authenticated world-entry snapshot and separate role-creation flow |
| World-frame gate | Startup close/enable transition follows verified world-ready state, not progress text | Content load, incremental world texture preparation and loading-state gate precede the first world draw |
| World frame | Network intake precedes world component updates and map/role rendering; foreground interface renders after the scene | Bounded continuous world intake applies selected packets before map, character and HUD rendering; most entity and gameplay updates remain unhandled |
| HUD state | Life, mana, stamina, skill and experience gauges draw from current character state and their respective ranges | Life is observed on screen; mana and experience need nonzero-value smoke tests, while stamina and skill updates lack complete server producers |\n| Sound ownership | Login music ends on world transition; map scenery audio instances start once when loaded and update position with the view | Startup playback is paused for world visibility; map ambience and world effects are outstanding |
| Content package ownership | Package declarations resolve archived sources before the game shell loads | Import consumes package declarations; curated runtime content excludes those declarations and keeps the character-mesh database archive |
| Exit | Reset disables world rendering and releases session-scoped resources | World scene, character graphics, HUD and world framebuffer are released on return and context teardown |

The visible initial idle character and HUD do not establish complete
startup-to-world behavior. The life gauge has been observed during a live
session, but mana and experience have not passed nonzero-value smoke tests.
Stamina, skill updates, live item mutations, functional HUD actions, character
action switching and world sound remain open. Content validation confirms the
required assets are installed; it does not replace running-client
visual/audio comparisons or authoritative gameplay update tests.

The asset index contains a recorded idle clip key `2_000_100` for body
source `2`. The separately observed role-action request builder uses a
different composite key, so action-request translation must be reconciled
through the full resource request path before generalizing the initial clip
lookup to arbitrary actions.
