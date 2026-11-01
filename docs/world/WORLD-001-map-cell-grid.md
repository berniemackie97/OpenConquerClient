# WORLD-001: DMap cell-grid input

The map-data ID is resolved through `ini/GameMap.dat`. Catalog entries reference
`.7z` archives; their **decompressed** payload begins with
two header words, a fixed `0x104`-byte puzzle path, grid dimensions, and
row-major cells. The next sections contain exit markers and scenery. The
archive payload is authoritative: companion `.DMap` files are not universally
byte-identical and some are absent.

`GameMapCellGrid` parses only the initial decoded section. It preserves
surface ID, passability flag, and signed elevation in their encoded field order,
verifies the per-row checksum, and returns `BytesConsumed` for the
next section parser. It validates the referenced puzzle path and rejects
truncated or implausible dimensions before allocating a grid. Remaining
decoded bytes are not rejected because they belong to subsequent sections.

The decoded format does not have one universally required magic header:
the Newbie Village map begins with `DMAP101\\0`, while the Desert map
uses `1004u, 0u`. Both variants are accepted; the first two words are retained
rather than treated as a universal signature.

The reader enforces a maximum of 4,194,304 cells before allocating a grid.
The bound covers the inspected set of 192 map
archives, the largest decoded payload is 24,754,428 bytes (`task06.DMap`),
so its cell grid cannot exceed 4,125,738 six-byte cells. Larger input
fails explicitly instead of requiring a potentially unbounded allocation.

The `map/map/newbie.7z` fixture contains a
118,644-byte `newbie.DMap`. Its grid is 132 × 132, uses
`map\\puzzle\\newbie.pul`, and ends the first section at offset 105,348.
Every row is checked against the per-row checksum. The boundary test reconstructs those observed uniform first-grid
values; it does not claim to bundle a full DMap asset.

This slice does **not** implement archive decompression, exit-marker or
scenery records, map scene ownership, camera, terrain draw, or loading-screen
transition. In particular, it does not read `.DMap` companions in place of
the catalog's `.7z` archive.

Map extraction and subsequent sections are handled by the archive and scene readers.
Activating the world scene still requires the remaining resource dependencies and
an updated content manifest. The operator uses
`OpenConquer.Content.Tool import-client-content` when that closure changes.
