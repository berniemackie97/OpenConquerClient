# WORLD-007: Newbie Village DDS compression support

The `ani/newbie.ani` fixture references 96 unique DDS frame
paths. All 96 resolve through the existing `data.wdf` package, not as
loose source files. The referenced DDS headers declare:

- 70 DXT1 files (32,896 bytes each, 256×256).
- 26 DXT3 files (65,664 bytes each, 256×256).
- No mipmaps in these 96 images.

The content system already supports `PackagedClientContentSource` and ANI
frame decoding, but the DDS reader previously only recognized DXT3. The
smallest independent change is DXT1 decompression in the existing reader
and FourCC dispatch in the existing image loader. No second WDF reader,
image cache, texture registry, or rendering abstraction is needed.

DXT1 uses 8-byte 4×4 blocks, RGB565 endpoints, and two selector modes.
When the first endpoint exceeds the second, the four palette entries are
opaque and use the same two-thirds interpolation as DXT3 color blocks.
Otherwise, entry 2 is the midpoint and entry 3 is transparent black.
The decoded output remains RGBA8, with clipped partial blocks. The
same bounded DDS header, dimensions, flags, and image-memory validation
apply to both formats. Unsupported formats fail explicitly.

A regression test includes the unchanged first compressed 8-byte block
from `data/map/puzzle/newbie/newbie005.dds` (inside `data.wdf`),
with pixel expectations independently checked using Pillow's DDS decoder.
The complete DDS fixture is 32,896 bytes and has SHA-256
`f414793043f137e30a458bfba645724ac711f87a9224b08a097eb2edcfdfb451`.
Additional tests cover both BC1 color modes, transparency, partial-edge
blocks, malformed input, and resolution through the existing WDF content
source. Tests do not replace content files in the product.

This slice is independent of WORLD-006 and does not activate map rendering.
Runtime content closure, actual DDS asset staging, GPU resources and
rendering, and the authenticated world transition remain unchanged.
