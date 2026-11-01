# WORLD-013: Terrain scene sprite drawing

`WorldTerrainSceneDrawList` projects the existing type-1 terrain objects into drawable animated sprites.
It uses map-tile anchors plus object offsets, the camera's viewport-center zoom, an eight-cell
visibility margin, and each object's frame interval. Elapsed animation time is relative to
scene loading; a zero interval draws with a one-millisecond interval.

Visible terrain objects use the scenery insertion rules for overlapping footprints and tile
ordering. Children such as actors and other scenery kinds are not yet part of this draw list;
they must participate in the same depth ordering when world-scene integration is implemented.

`WorldMapTextureRenderer` prepares unique image frames before an OpenGL frame, then draws
puzzle tiles or scaled terrain sprites from its cached textures. Its owning graphics context
must outlive it. The caller supplies the current map world color.

This slice does not activate a world scene in `ClientApplication`, add map assets to the
published content closure, or claim a complete world renderer. Those changes belong to
world-scene integration, using the original content import pipeline.
