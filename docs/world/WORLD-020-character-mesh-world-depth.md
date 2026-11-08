# WORLD-020: Character mesh composition and world-depth placement

## Evidence and scope

Character drawing, camera setup and scenery insertion are documented in `20_notes/Rendering/06_hero_render_pipeline.md` and the signed-tile evidence below. Arbitrary mesh offsets, motion arcs and complete scene-depth equivalence remain unverified.

The authenticated character-info snapshot supplies the appearance composite.
The body source is the appearance composite modulo 1000; its model key is the
body source multiplied by 1,000,000. The content resolver loads the role-part
configuration and model descriptors from the existing encrypted C3 database,
then resolves the mesh and texture IDs through the source's 3D resource indexes.
Repeated resource IDs replace previous values in the recorded order.

The 3D content source preserves the two original lookup modes: databases come
from the loose resource directory; C3 and DDS resources use packaged lookup.
The content importer includes the database, package declaration, C3 package
and any source-supplied loose object/texture indexes. The embedded archive
indexes can omit model and texture IDs present in the source's loose files,
so importing them is necessary to preserve content resolution.
Older curated sets without
these resources retain their existing map-only display; missing 3D character
resources are reported rather than substituted with an unrelated model.

## Draw sequencing

The world scene activates a visible character depth entry after animated and static
scenery insertion, using the same overlap ordering and terrain-footprint
adoption. Its stored insertion index divides the scenery draw list into
two ranges. Rendering the character occurs between those ranges, without
duplicating the terrain or ANI texture loaders.

The 3D path uses the body model's PHY and MOTI sections, reuses the existing
CPU vertex calculation and indexed OpenGL mesh resources, and submits drawable
submesh geometry before the projected geometric shadow pass. PHY entries named
by the role-part virtual attachment table are not drawn; their geometry
marks attachment points rather than visible character surfaces. The shadow is
solid black with alpha 0.3, a strict less-than depth comparison and the
documented planar shadow matrix. It does not write depth or change the
destination alpha channel. The normal pass retains less-or-equal
depth comparison and the resolved DDS texture.

## Acceptance boundary

This PR establishes the startup body draw and its depth/shadow composition.
It does **not** establish complete animated-character parity: action-driven
motion selection, direction changes, equipment and hair attachment transforms,
secondary texture stages, the separate SimpleShadow sprite, and visual
regressions against a running 5517 world still require verification.
The default body pose uses frame zero until the authoritative action/frame
state is implemented; the renderer does not fabricate an idle animation clock.

An installation using the previous curated content set does not contain
the required C3 resources. Reimporting and publishing the content closure from
the source client is required before this body path can become visible.
Do not treat successful unit tests or a synthetic OpenGL framebuffer check
as proof that a character appears correctly in map 1010.

## Off-map placement — CP2.4c

Native role tiles are signed int32 values. Map projection wraps SUB/LEA/SHL before
signed division by2 (`Map_ProjectTileToScreenCoordinates`, `0x61D850`); using an
unbounded tile multiplication gives different results for extreme coordinates.
The shared projected-screen predicate (`0x61E00D`) accepts inclusive edges and
scales the role's margin2 by map zoom. It does not inspect map-cell bounds.

A visible off-map role enters the same `CMapSceneryComponent_ReorderSubcomponentForDepth`
path (`0x635AE6`), with its virtual tile pair (`0x651875`) and default 1×1 span.
Type2 terrain can adopt it through the normal half-open footprint (`0x644E0E`).
Otherwise axis overlap and wrapped signed tile sums select its position. Off-screen
roles are not inserted; drawing handles the absent role without a negative scenery
range or skipped scenery preparation.

Ordinary idle/walk rendering initializes vertical locals to zero (`0x64F66B/66E`)
and does not sample cell altitude. For the terrain-delta helper, NULL from the
inverse-projected tile lookup returns zero directly (`0x650940..0x650949`, epilogue
`0x65097C`). The actual RoleView elevation operand is traced at `0x64F832..0x64F854`.
This does not make independent native role offsets, motion arcs or every vertical
term zero. The existing supported flat-role transform needs no invented off-map
cell fallback; those additional action inputs remain unimplemented.

Fixture provenance: `tests/OpenConquer.Client.Tests/Fixtures/World/README.md`.
The 27 static-disassembly-derived cases cover projection, screen visibility and
depth; scene tests also check unclamped hero anchors with a clamped camera and
return to an ordinary tile. Raw evidence and scoped limits:
`20_notes/Network/off-map-tiles-cp24c.md`. No native runtime/framebuffer comparison
is claimed by these tests.
