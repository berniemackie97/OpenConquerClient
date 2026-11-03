# WORLD-020: Character mesh composition and world-depth placement

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

The world scene activates a character depth entry after animated and static
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
