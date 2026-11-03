# WORLD-021: Local-role idle playback and world presentation

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
gameplay HUD. The HUD uses the world renderer's current logical viewport
for both drawing and pointer hit testing, and is recreated when that viewport
changes. Its resources are released on world exit and graphics-context
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
