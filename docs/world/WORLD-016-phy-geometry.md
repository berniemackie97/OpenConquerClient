# WORLD-016: PHY geometry decoding

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
The original vertex bone fields, serialized normals, key streams, object
transform, and unconsumed trailing bytes remain available to later animation
and draw consumers. Mesh index and section boundaries are checked before
allocating geometry.

The independent SimpleObj 19 fixture establishes the first-vertex post-load
position (-79.603531, -39.801765, -4.999774) from an on-disk position of
(-6, -3, 0.0000170716) and uniform scale 13.26725578 with Z translation -5.
Tests also exercise every vertex layout and normal smoothing with generated data.

This step does not sample MOTI bone matrices, apply frame or alpha keys,
decode textures, or submit OpenGL triangles. The transformed static geometry
is a data boundary for the upcoming motion and rendering passes; this alone
does not make the hero visible.
