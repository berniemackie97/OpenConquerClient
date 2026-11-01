# WORLD-012: Terrain scene resources

A map's type-1 scenery placements each reference a `.scene` file. The file lists terrain objects: animated
sprites with a footprint of map cells. This change parses those files, resolves each object's animation section,
and validates a map's terrain scenes from the content tool. It does not draw them, attach their footprints to the
cell grid, or order them for drawing.

## `.scene` format

All integers are little-endian 32-bit. Strings are fixed-length, NUL-terminated Latin-1 fields.

| Offset | Size | Field |
|---:|---:|---|
| 0 | 4 | object count |

Each object follows, `0x164 + 12 × width × height` bytes:

| Offset | Size | Field |
|---:|---:|---|
| `0x000` | `0x100` | animation index path |
| `0x100` | `0x40` | animation section name |
| `0x140` | 4 + 4 | render offset X, Y (screen pixels, added to the projected anchor cell) |
| `0x148` | 4 | frame interval in milliseconds (a value below 1 draws as 1) |
| `0x14C` | 4 + 4 | footprint width, height (cells) |
| `0x154` | 4 | authored colour (stored; drawing tints with the map world colour) |
| `0x158` | 4 + 4 | anchor offset X, Y (cells, from the placement's anchor cell) |
| `0x160` | 4 | draw parameter (stored; terrain objects draw with the default blend) |
| `0x164` | 12 each | footprint cells, row by row: passability, surface, elevation delta |

Only the low 16 bits of each cell value are significant. An object's anchor cell is the placement's anchor cell
plus the object's anchor offset.

## Loading

- `GameMapTerrainSceneFile` parses a file. It rejects truncation, trailing bytes, counts or grids larger than the
  file, empty or unterminated names, and animation paths that are absolute or leave the content root. Files are
  capped at 1 MiB.
- `GameMapTerrainSceneResources` loads each referenced scene file once, resolves every object's section through
  `AniSectionResolver`, and keeps the map's placements in authored order. Placements of the same file share one
  object list. Authored order is not a draw order.
- `AniSectionResolver` loads each animation index once and resolves sections by exact name, rejecting a section
  whose name only shares a hash with the requested one, and sections without frames. Scenery animation binding
  and puzzle resources use it too.

The validation command checks the scene resources and animation sections referenced by the selected map.

## Validation

```bash
dotnet run --project tools/OpenConquer.Content.Tool -c Release -- \
  validate-map-terrain-scenes --source "/absolute/path/to/client" --map-data-id 1010
```

The command is read-only. It loads the map, its scene files and their animation sections, and decodes every
referenced frame. Newbie Village (1010) has 20 placements of six scene files (`stand01`, `stand02`, `stand03`,
`stand05`, `stand06`, `stand08`), each with one object and one frame.

## Tests

Parser tests cover every field and each rejection case. Resource tests load the Newbie map and check placement
order, shared scene files, and missing files and sections. Resolver and resource tests cover caching, path normalisation, missing and empty sections,
and hash collisions. Scene, animation and image contents in tests are generated.

## Next

Drawing needs the scenery spatial index (fixed 256-pixel buckets for static objects, always-ticked moving
objects) and the depth sort, in which a terrain object adopts the objects standing in its footprint.
