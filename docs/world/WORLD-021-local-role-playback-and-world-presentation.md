# WORLD-021: Local-role idle playback and world presentation

## Evidence and scope

The authored motion index identifies the initial idle clip. Action interval and transition behavior are documented in `20_notes/Rendering/roleview_dll/07_action_playback_transition_state.md` and `13_role_compute_frame_interval.md`. The initial 66 ms application clock does not establish arbitrary action selection or transition timing.

## World activation

The authenticated map is loaded before world texture preparation. Once
the world scene is visible, rendering submits the map and role before the
gameplay HUD. The world renderer follows the framebuffer's aspect ratio,
while the HUD retains the configured, verified UI layout dimensions. The HUD
draws and handles pointer input in the world renderer's logical coordinates,
using its fixed UI layout and without rebuilding assets on viewport resizing.
For aspect ratios that reduce the world viewport below the authored UI extent,
the existing artwork can be clipped; extending that layout requires separate
verified geometry. HUD resources are released on world exit and graphics-context
teardown. Login music is paused while the world is visible and resumes on
return to the login interface, subject to application focus.

## Remaining contracts

This integrates the initial idle motion, not the complete local-role action
state machine. Movement-driven action transitions, equipment/helmet/hair
part bindings, facing, action-speed modifiers, and blended playback remain
unimplemented. World ambience and effect playback need separate content
and lifecycle ownership: muting login music is not equivalent to implementing
the map-sound subsystem. The installed content package must include the C3
database, motion resources and mesh resources before the animated role can
appear; framebuffer tests alone cannot verify the live map appearance.
