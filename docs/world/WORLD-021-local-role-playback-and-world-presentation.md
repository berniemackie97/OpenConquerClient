# WORLD-021: Local-role idle playback and world presentation

## Evidence and scope

The authored motion index anchors the initial body idle clip. The native action interval calculation at `Conquer.exe:0x64DE26..0x64DE82` and RoleView transition evidence in `20_notes/Rendering/roleview_dll/07_action_playback_transition_state.md` and `13_role_compute_frame_interval.md` show a broader action path. The initial 66 ms managed clock does not establish arbitrary action lookup, pacing or transitions.

The default first-world-frame role action is idle (100); alternate idle
actions remain dependent on the command-state selection. The body source is the
authenticated appearance composite modulo 1000. The first-world-frame idle
resource key is `bodySource * 1000000 + 100`. The original C3 resource index
contains `2000100 -> c3/0002/000/100.c3` for body source 2, as well as matching
keys for body sources 1, 3, and 4. The motion database uses
two 32-bit key halves and a resource path; the loader resolves that record
from the body's configured motion table and binds the MOTI entries by ordinal
submesh position. Geometry and motion files remain separate resource
requests. Missing action records retain the default static pose rather
than starting an unrelated animation. The higher-level role-action key builder
uses a different composite formula. This initial clip selection is grounded
in the recorded asset index; reconciling that builder with the resource-query
and fallback sequence remains necessary before action transitions can be
claimed equivalent.

The initial idle clock advances at 66 milliseconds per frame. It starts
with the first visible role draw, wraps by the motion's frame count and uses
unsigned elapsed time to handle tick-count rollover. The existing skinning
and indexed GPU submission paths consume the selected sample; no second
vertex decoder or renderer is introduced.

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
