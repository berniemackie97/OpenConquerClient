# WORLD-010: Scenery ANI resource binding

The scenery binder resolves type-4 DMap records to animation index sections and
validates referenced frames. This stage does not determine visual positions,
animation clocks, draw order, visibility, or live scene activation.

## Resource contract

- The Newbie Village DMap fixture contains 38 scenery records. Eighteen type-4
  records reference 16 distinct animation sections.
- Those records use `ani/MapScene.ani` and section names such as `newbie17.tga`.
  The 129,051-byte index maps `newbie17.tga` to its single frame path,
  `data/map/mapobj/newbie/newbie17.dds`.
- The `mapscene-newbie-sections.ani` fixture contains the 16 referenced
  sections, rather than all 1,379 sections in the complete index.
- A type-4 record stores tile coordinates and authored depth spans.
  The records do not establish the final draw order.

`GameMapSceneryAniBindings.Load` retains each file association, loads each
distinct ANI index once, and rejects missing or empty referenced sections.
It does not resolve type-1 external `.scene` resources or interpret tile
coordinates as final screen positions.

## Operator validation

From the client repository root, run:

```bash
dotnet run --project tools/OpenConquer.Content.Tool -c Release -- \
  validate-map-scenery-ani --source "/absolute/path/to/client" \
  --map-data-id 1010
```

This read-only command decodes each distinct referenced DDS/TGA frame through
`LooseThenPackage`. Automated tests use committed DMap and selected ANI
section fixtures, with generated DXT1 images to exercise the decoder.
The generated images do not replace a full-content source preflight.

Live scene drawing additionally requires scene activation, spatial/depth
ordering, animation timing, projection, and external `.scene` resources.
This stage does not alter shipped content, GameServer behavior, camera state,
or world-entry transitions.
