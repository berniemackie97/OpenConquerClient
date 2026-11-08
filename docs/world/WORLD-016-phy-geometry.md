# WORLD-016: PHY geometry decoding

## Evidence and scope

`C3_CORE_DLL.dll:0x1000A300` loads the PHY variants and performs post-parse transform and normal preparation. The branch matrix, vertex strides, split layout and normalized fields are in `20_notes/Rendering/c3_core_dll/05_phy.md` §§Verified file parse order, post-parse processing and vertex payload. The SimpleObj fixture is independent of the managed loader.

A C3 mesh section contains a length-prefixed part name, section flags, two serialized
vertex counts, a vertex stream, two triangle counts, 16-bit indices, an opaque byte
region, bounds, a 4×4 object transform, frame count, three key streams, and
optional trailing tags.

The four supported PHY variants have vertex strides 76, 88, 52 and 40 bytes.
Split layouts skip the 36-byte intermediate region. The two vertex counts form
one vertex buffer and the two face counts form one triangle index buffer.

`C3PhyMeshReader` decodes the four layouts and applies the serialized object
transform to vertex positions. For generated-normal layouts it computes
unit face normals, accumulates equally weighted face contributions, and
smooths normals of vertices sharing positions within a 0.01 tolerance.
The decoded vertex keeps its reserved float, unsigned bone indices at internal
vertex offsets +0x18/+0x1C, float weights at +0x20/+0x24, and its normal.
The mesh captures the used-bone indices once during loading, including the
second index only when its weight is positive. Key streams, object transform,
and trailing bytes remain available to subsequent consumers. Mesh index and section boundaries are checked before
allocating geometry.

The independent SimpleObj 19 fixture establishes the first-vertex post-load
position (-79.603531, -39.801765, -4.999774) from an on-disk position of
(-6, -3, 0.0000170716) and uniform scale 13.26725578 with Z translation -5.
Tests also exercise every vertex layout and normal smoothing with generated data.

The mesh reader decodes and retains geometry independently of motion sampling,
texture binding, and indexed OpenGL submission. Animated vertex deformation
is described in WORLD-018; the local hero draw path is not implemented.
