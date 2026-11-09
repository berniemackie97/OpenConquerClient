# WORLD-021: Local-role idle playback and world presentation

## Evidence and scope

The authored motion index identifies the initial idle clip. Action interval and transition behavior are documented in `20_notes/Rendering/roleview_dll/07_action_playback_transition_state.md` and `13_role_compute_frame_interval.md`. The initial 66 ms application clock does not establish arbitrary action selection or transition timing.

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

## Remote role receive ownership

The authenticated world ingress now decodes the server's 10014 player-spawn
message into owned name bytes and appearance/equipment fields, and retains
one bounded remote role record per entity ID. Repeated spawns replace the
same identity; 10010/135 removes it. The map's 18-tile visibility radius
removes peers outside the local hero's square on each world tick. An unknown
10005 player identity sends one 10010/102 lookup through the existing capped
outbound lane, resetting the request after a spawn or removal. Other actors
cannot change the authenticated hero's position.

This is the receive/state portion of [CP4.1](https://github.com/berniemackie97/OpenConquerClient/issues/249),
not complete peer presentation. Rendering still needs validated C3 resource
ownership, scene depth insertion, pointer/picking state if used, and actual
two-client and original-client comparison. The current fixtures are
server-writer contract cases, not observed original-server output. Full CP3.2
verification remains a release acceptance gate.

## Nearby character presentation and graphics ownership

The authenticated 10014 roster is the source of identity, tile position, facing
and equipment for nearby peers. Roster revisions invalidate the committed
world role order. Both the hero and visible peers enter the existing scenery
depth sorter as 1×1 role footprints, then draw at their assigned insertion
positions using the same map projection, C3 mesh renderer and shadow transform.
No second map renderer or independent depth algorithm exists.

`WorldRoleSources` resolves equipped models for both local and remote
characters. The world's asset reader serializes catalog and source access
across the hero and one remote preparation worker. Remote models are keyed by
body family and resolved equipment sources; identical appearances share a
single installed graphics renderer while retaining separate identities and
tile transforms. Model selection is deterministic by distance then entity ID
and caps the number of simultaneously prepared distinct appearances at 64.
That is a graphics resource policy, not a protocol or gameplay limit.
The 4096-entry remote identity registry remains authoritative even when
density exceeds the graphics preparation budget.

Role departure, replacement and the 18-tile visibility boundary update both
the scene order and the set of retained model resources. Outdated CPU loads
are canceled, the single active preparation is drained at shutdown, and
installed GL resources are released on the graphics owner thread. Context
recreation rebuilds the installed renderer set; ordinary window resizing
only updates scene projection and does not duplicate model ownership.

This slice does not establish independently captured source-server 10014
frames, original-client render timing, visual correctness, movement animation,
or two-client interoperability. Those remain acceptance requirements of
[#249](https://github.com/berniemackie97/OpenConquerClient/issues/249);
R1 [#248](https://github.com/berniemackie97/OpenConquerClient/issues/248)
continues to gate release acceptance.

## Remote walk relay application

World packet 10005 is applied in arrival order by the admitted world owner.
The authenticated hero retains its established echo behavior. A known peer
uses the same decoded direction and complete selector; selector 9 uses the
24-entry two-tile displacement table, while other selectors use the eight-way
table. The lower direction byte selects the displacement and the presented
facing is reduced to eight directions. The upper packed-direction bits remain
unmodified by parsing.

The peer registry replaces the moved identity's tile and facing while retaining
all other spawn fields. It refuses positions outside the unsigned wire tile
range without altering state. Accepted moves increment the roster revision so
the unified scene reorders the peer with scenery and recomputes its world
transform. Unknown identities continue through the single existing player
lookup path. Departed peers are removed by the existing visibility cull and
explicit action 135. These updates do not create a separate socket reader,
rendering pipeline, movement queue or per-peer task.

This is state and committed-tile presentation, not timed walking animation.
Movement cadence, supported role state gating, local movement input, content-
backed animation and same-map original-client/two-client comparison remain
separate acceptance work in
[#250](https://github.com/berniemackie97/OpenConquerClient/issues/250).
