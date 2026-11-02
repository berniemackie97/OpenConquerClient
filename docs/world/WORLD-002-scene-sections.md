# WORLD-002: Decoded DMap scenery and positioned overlays

The decoded DMap contains four blocks in order: cell grid, exit-marker
triplets, scenery records, and positioned overlay records. This change reads the **post-grid sections**
at `GameMapCellGrid.BytesConsumed` without changing the renderer or networking.

The scenery record payload lengths are `1` (0x10C), `4` (0x1A0),
`10` (0x48), and `15` (0x114). In maps with first header value `1005`,
type-4 records occupy 0x1A4 bytes. The additional four bytes are kept
uninterpreted in the encoded payload. This layout is observed in map-data
1999; the other examined maps use the shorter records. The header selects
the bounded decoding layout here, without asserting equivalent executable
dispatch. The reader preserves each entire scenery payload without
assigning meanings to unclassified fields.
Type 1 paths and anchor coordinates and type 4 resource paths, part names,
and tile coordinates are read from the encoded fields.

The positioned-overlay block has an explicit insertion index, type 4
record, two parallax header integers, and child entries.
Child type 3 includes two strings, tile position, draw offsets, frame
interval, and a final unclassified integer. Child type 8 references a
`.pul` puzzle bitmap. The child and record orders are preserved for the
future component renderer. Unsupported types fail closed instead of
skipping unknown-length payloads.

The test fixture contains only the post-grid bytes from
`map/map/newbie.7z` / `newbie.DMap`, compressed separately to
avoid bundling unrelated textures. It has
0 exit markers, 38 scenery entries (18 type 4 and 20 type 1), and one
positioned overlay (insert index 0, header values 18 and 35) containing
one type-8 puzzle child at `map/puzzle/newbiebg.pul`. The parsed
cursor ends at the real end of the decoded map (118,644 bytes).

Input is limited to 32 MiB of decoded map data, which covers the
measured 5517 source archive set (largest observed decompressed
map 24,754,428 bytes). Counts must fit the actual remaining input;
file-relative resource paths are validated. Unknown record types,
invalid paths, truncation, and unexplained trailing bytes are rejected.

The scene reader does not own `.7z` extraction, `.scene`, `.pul`, ANI or texture
loading, component composition, projection, or frame drawing. No client content closure,
manifest or payload changes are appropriate yet. The operator will run
the content importer when the runtime consumer is connected. No server packet changes are involved:
packet 1110 already communicates the independent world map and terrain
data identifiers.

The scene reader and parser tests define the supported encoded records.
