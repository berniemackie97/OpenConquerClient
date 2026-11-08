# WORLD-013: Terrain scene sprite drawing

## Evidence and scope

The terrain draw at `Conquer.exe:0x64450F` resolves ANI, uses construction-time elapsed intervals and map tint, projects the anchor plus offsets and scales around the viewport center. Depth insertion is `0x635AE6`. See `20_notes/Game_Map/world_render_contract_5517.md` §§3–5. The managed scene-load clock and texture-bounds clip are scoped to this renderer. They do not prove all visibility, adopted-child ordering or per-object timing parity.

`WorldTerrainSceneDrawList` projects the existing type-1 terrain objects into drawable animated sprites.
It uses map-tile anchors plus object offsets, the camera's viewport-center zoom, and
each object's frame interval. Elapsed animation time is relative to scene loading;
a zero interval draws with a one-millisecond interval.

Terrain objects use the scenery insertion rules for overlapping footprints and tile
ordering. Frame dimensions are not known when the draw list is built, so offscreen anchors
are retained; the texture renderer clips using the final scaled image bounds.
Children such as actors and other scenery kinds are not yet part of this draw list;
they must participate in the same depth ordering when world-scene integration is implemented.

`WorldMapTextureRenderer` prepares unique image frames before an OpenGL frame, then draws
puzzle tiles or scaled terrain sprites from its cached textures. Its owning graphics context
must outlive it. The caller supplies the current map world color.

This slice does not activate a world scene in `ClientApplication`, add map assets to the
published content closure, or claim a complete world renderer. Those changes belong to
world-scene integration, using the original content import pipeline.
