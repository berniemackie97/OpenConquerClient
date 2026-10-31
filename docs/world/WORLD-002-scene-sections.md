# WORLD-002: Decoded DMap scenery and positioned overlays

The native 5517 `CGameMap_LoadMapFileFromRelativePath` parses four blocks
in order: cell grid, exit-marker triplets, type-3 scenery records, then
positioned overlay records. This change reads the **post-grid sections**
at `GameMapCellGrid.BytesConsumed` without changing the renderer or networking.

The scenery record types and encoded byte lengths are evidence-backed:
`1` (0x10C external terrain-object group), `4` (0x1A0 map sprite),
`10` (0x48), and `15` (0x114). The reader preserves each entire
scenery payload, rather than inventing meanings for fields not yet proven.
Type 1 paths and anchor coordinates and type 4 resource paths, part names,
and tile coordinates are exposed from native reader offsets.

The positioned-overlay block has an explicit insertion index, type 4
record, two parallax header integers, and child entries.
Child type 3 includes two strings, tile position, draw offsets, frame
interval, and a final unclassified integer. Child type 8 references a
`.pul` puzzle bitmap. The child and record orders are preserved for the
future component renderer. Unsupported types fail closed instead of
skipping unknown-length payloads.

The test fixture contains **only** the original post-grid bytes from
clean 5517 `map/map/newbie.7z` / `newbie.DMap`, zlib-compressed to
keep unrelated retail textures out of the repository. It has
0 exit markers, 38 scenery entries (18 type 4 and 20 type 1), and one
positioned overlay (insert index 0, header values 18 and 35) containing
one type-8 puzzle child at `map/puzzle/newbiebg.pul`. The parsed
cursor ends at the real end of the decoded map (118,644 bytes).

Input is limited to 32 MiB of decoded map data, which covers the
measured 5517 source archive set (largest observed decompressed
map 24,754,428 bytes). Counts must fit the actual remaining input;
file-relative resource paths are validated. Unknown record types,
invalid paths, truncation, and unexplained trailing bytes are rejected.

The real runtime **still** needs verified bounded `.7z` extraction,
dependent `.scene`, `.pul`, ANI and texture loading, component
ownership, projection, and frame drawing. No client content closure,
manifest or payload changes are appropriate yet. The operator will run
the Content Tool import from the clean retail client when the first
runtime consumer is connected. No server packet changes are involved:
packet 1110 already communicates the independent world map and terrain
data identifiers.

Native evidence: `20_notes/Game_Map/cgamemap_class.md`
(`0x61DEE5..0x61DFBA`, `0x635808`, `0x639BD6`,
`0x64B674`, `0x64B8F5`).
