# WORLD-005: Retail map puzzle resource loading

WORLD-004 loads the DMap grid and scene sections by the server-provided map-data
ID. WORLD-005 uses the existing GameMapPuzzleBitmap parser to load ground and
positioned type-8 puzzle bitmaps through the bounded content source, keeping one
parsed instance per resource path. It preserves the source tile indices, ANI path,
and PUZZLE2 scroll velocities. It does not construct sprites or change render
order.

The retail client calls CMapPuzzleBitmap_LoadFromPath at 0x63A603 for both
ground and type-8 positioned puzzle overlays. The verified PUZZLE/PUZZLE2 format
is an 8-byte signature, 0x100-byte ANI path, two little-endian dimensions, a
row-major ushort index grid, and (for PUZZLE2) two little-endian scroll
velocities. 0xFFFF denotes an empty tile. See
20_notes/Rendering/05_dmap_positioned_overlay_system.md.

The retail 5517 archive contains 203 .pul files: 30 PUZZLE and 173 PUZZLE2.
The largest is map/puzzle/Gulf.pul (95,830 bytes). The content reader uses a
128 KiB upper bound for these files. The existing bitmap parser rejects unsafe
ANI paths, malformed headers, truncated grids and trailing data.

The original 5517 newbie.7z for map-data ID 1010 references both
map/puzzle/newbie.pul (15x9 ground) and map/puzzle/newbiebg.pul
(6x6 positioned backdrop). Both puzzle files reference ani/newbie.ani,
verified directly from the retail bytes; an older rendering note instead
names ani/newbiebg.ani. Tests use those unchanged retail .pul files and the
retail PUZZLE2 map/puzzle/jokul01.pul fixture.

The map puzzle resource loader remains inactive during live world entry.
The existing content closure is unchanged: ANI frames, scenery, sprites and
textures must still be sourced, imported and resolved before activating the
actual world renderer. This does not claim a rendered or playable world.
