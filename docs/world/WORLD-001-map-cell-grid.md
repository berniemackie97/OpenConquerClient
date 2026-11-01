# WORLD-001: Retail DMap cell-grid input

The world map data ID is resolved through `ini/GameMap.dat`. Retail 5517
entries use `.7z` map archives; their **decompressed** payload begins with
two header words, a fixed `0x104`-byte puzzle path, grid dimensions, and
row-major cells. The next sections contain exit markers and scenery. The
archive payload is authoritative: companion `.DMap` files are not universally
byte-identical and some are absent.

`GameMapCellGrid` parses only the initial decoded section. It preserves
surface ID, passability flag, and signed elevation in their native field order,
verifies the native per-row checksum, and returns `BytesConsumed` for the
next section parser. It validates the referenced puzzle path and rejects
truncated or implausible dimensions before allocating a grid. Remaining
decoded bytes are not rejected because they belong to subsequent sections.

The decoded format does not have one universally required magic header:
the Newbie Village map begins with `DMAP101\\0`, while the independently
verified retail Desert map begins with `1004u, 0u`. The native loader reads
these first two dwords but does not reject the latter header variant. We
preserve them rather than incorrectly enforcing one signature.

The reader enforces a maximum of 4,194,304 cells before allocating a grid.
That bound covers the shipped 5517 archive census: across all 192 map
archives, the largest decoded payload is 24,754,428 bytes (`task06.DMap`),
so its cell grid cannot exceed 4,125,738 six-byte cells. Larger input
fails explicitly instead of requiring a potentially unbounded allocation.

The clean retail `map/map/newbie.7z` archive extracted to a
118,644-byte `newbie.DMap`. Its grid is 132 × 132, uses
`map\\puzzle\\newbie.pul`, and ends the first section at offset 105,348.
Every one of the 132 source rows was checked against the native checksum
formula. The boundary test reconstructs those observed uniform first-grid
values; it does not claim to bundle a full DMap asset.

This slice does **not** implement archive decompression, exit-marker or
scenery records, map scene ownership, camera, terrain draw, or loading-screen
transition. In particular, it does not read `.DMap` companions in place of
the retail `.7z` archive.

Next: bounded extraction of the retail `.7z` map payload using an audited
codec, followed by post-grid sections and original puzzle/ANI texture
dependencies. The runtime content closure and tracked payload stay unchanged
until a real consumer is wired. The operator will run
`OpenConquer.Content.Tool import-client-content` from the clean retail source
when that closure changes.

Evidence: `20_notes/Game_Map/cgamemap_class.md`
(`CGameMap_LoadMapFileFromRelativePath`, `0x61DC3D`).
