# WORLD-005: Retail map puzzle resources

WORLD-004 loads the DMap grid and scene sections by the server-provided map-data ID.
WORLD-005 introduces a content-source reader for the ground and positioned-overlay
puzzle bitmap paths already present in those sections. It reads actual .pul files
and retains their tile indices, ANI path, and PUZZLE2 scroll velocities without
constructing sprites, substituting textures, or changing map render order.

The 5517 client calls CMapPuzzleBitmap_LoadFromPath at 0x63A603 for both ground
and type-8 positioned puzzle overlays. The verified PUZZLE/PUZZLE2 format is an
8-byte signature, 0x100-byte ANI path, two little-endian dimensions, a row-major
ushort tile-index grid, and (for PUZZLE2) two little-endian scroll velocities.
0xFFFF denotes an empty tile. See
20_notes/Rendering/05_dmap_positioned_overlay_system.md.

The original 5517 retail archive contains 203 .pul files: 30 PUZZLE and 173
PUZZLE2. The largest is map/puzzle/Gulf.pul (95,830 bytes); the reader's
128 KiB content limit covers that corpus. The reader rejects invalid paths,
headers, truncated grids, mismatched lengths, and oversized content. Paths are
normalized through the established content path contract and opened only through
the existing bounded content source.

For map-data ID 1010, the unchanged retail newbie.7z references both
map/puzzle/newbie.pul (15x9 ground) and map/puzzle/newbiebg.pul
(6x6 positioned backdrop). Both original puzzle files reference
ani/newbie.ani; this has been verified from their bytes, despite the older
rendering note naming ani/newbiebg.ani. Tests use these retail .pul files
and retail map/catalog fixtures, along with original PUZZLE2
map/puzzle/jokul01.pul.

This is not yet a displayed map. The resource loader does not pre-activate
world entry, expand the shipped content closure, load ANI frame textures,
resolve type-1 scenery, or introduce a GPU draw path. Those dependencies
must be verified and imported through the existing retail content tooling
before live world admission activates map loading.
