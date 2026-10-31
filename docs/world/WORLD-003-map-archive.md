# WORLD-003: Retail seven-zip DMap extraction

The native 5517 client selects the map reader by the path stored for the
**map-data ID**, not the world-map ID. Its mode-2 reader checks the six-byte
7z signature, extracts the DMap into memory and hands the decoded bytes to
its map parser. The native entrypoints are documented in
`20_notes/Game_Map/cgamemap_class.md` (`0x7715A8`,
`0x771323`, `0x771426` and `0x771498`).

`GameMapArchive` uses SharpCompress 0.50.7 with centrally pinned NuGet
dependencies and checked-in lockfiles, not a custom LZMA implementation.

All 192 retail 5517 map archives were inspected directly. Each contains
exactly one .DMap entry with the same stem as its .7z archive. The largest
compressed archive is `halloween02.7z` at 74,962 bytes; the largest
decoded map is `task06.DMap` at 24,754,428 bytes. The loader enforces
a 128 KiB compressed input limit and a 32 MiB decoded limit, validates
container signature and entry identity, and rejects multiple entries,
directories, incomplete output and oversized payload declarations. It
never writes archive paths to the filesystem.

The regression fixture is the original, unmodified 1,275-byte retail
`newbie.7z`. Its 118,644 decoded bytes pass the source-backed
`GameMapCellGrid` and `GameMapSceneSections` readers, including the
132×132 cell grid, 38 scenery records and positioned backdrop.

The compressed input is copied to an owned buffer after the input-length
check; signature validation and decompression read from that same immutable
snapshot. SharpCompress is distributed under the MIT license, which is
included with published client files under
`third-party/sharpcompress/LICENSE.txt`.

This slice does not enable world rendering, alter network packet
contracts, or extend the runtime content closure, payload, or product
staging. The operator will regenerate retail runtime content locally
when the real map-consuming world pipeline is connected.
