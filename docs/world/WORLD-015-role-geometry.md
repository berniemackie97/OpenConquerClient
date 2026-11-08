# WORLD-015: C3 mesh container

## Evidence and scope

`graphic.dll:0x10004960` creates the C3DObjX mesh. Its load loop caps the loaded PHY count at `0x10003907`, dispatches the four variants and skips non-PHY chunks at `0x100039D6`. See `20_notes/Rendering/graphic_dll/12_meshcreate_and_c3dobjx.md`. Effect-container counts do not expand the role mesh contract.

Role graphics use C3 containers with a 16-byte header followed by sections. Each section
has a four-byte identifier, a 32-bit little-endian payload length, and that number of
payload bytes. PHY identifiers use the byte sequence `PHY` followed by one variant byte:
`0x20`, `0x32`, `0x33`, or `0x34`. Mesh loading accepts at most 16 recognized PHY sections.

`C3MeshFile` reads these boundaries without copying payloads. It retains the complete
section sequence, including motion chunks, so a subsequent geometry/motion consumer can
resolve the sections from the same owned content buffer. It rejects truncated sections,
out-of-bounds payload lengths, oversized inputs, and excessive section counts. The
content source's existing package lookup owns resource resolution.

The file reader is scoped to mesh containers. Effect containers use a different
resource consumer and can exceed the mesh PHY limit; they must not be routed through
this loader. An independent content scan examined 6,068 C3 files; containers with more than
16 PHY sections were effect assets, not role mesh assets. Mesh bytes are not
added to the public content set.

This establishes the container boundary only. PHY vertex decoding, motion sampling,
role-part model lookup, texture/material binding, and the OpenGL world draw pass are
not implemented by this slice. The local hero remains absent from the world scene.
