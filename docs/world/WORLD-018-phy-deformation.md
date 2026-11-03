# WORLD-018: PHY vertex deformation

The mesh decoder retains the normalized vertex fields used by the runtime
vertex calculation:

| Internal offset | Field |
| --- | --- |
| +0x00 | Position (three floats) |
| +0x0C | UV (two floats) |
| +0x14 | Reserved float |
| +0x18 | Unsigned bone index 2 |
| +0x1C | Unsigned bone index 1 |
| +0x20 | Bone weight 2 |
| +0x24 | Bone weight 1 |
| +0x28 | Normal (three floats) |

These field types are independent of the PHY container's discriminator. A mesh
with generated normals can still have weighted, animated vertices.

`C3PhySkinner` samples the mesh's used-bone palette through the existing
`C3MotionSampler` and calculates each vertex with an element-wise weighted
matrix: `weight1 * matrix[index1] + weight2 * matrix[index2]`.
Positions use row-vector homogeneous coordinate transforms, dividing XYZ by W
unless W is within float epsilon of one. Normals use only the linear 3x3
matrix portion, without translation, homogeneous divide, or renormalization.

An output vertex is 36 bytes: position, normal, ARGB diffuse, and UV. The UV
includes the caller's resolved step offset. All output buffers and the
per-motion matrix palette are caller-owned; updating a frame does not create
vertex or bone arrays. The original loaded vertices are never mutated.

The first vertex of the independently captured SimpleObj 19 fixture, including
its 101-frame constant primary bone, is checked against its recorded position
and normal following vertex calculation. Tests also cover two weighted bones,
unclamped secondary-motion blending, absent bone indices, rigid vertices,
invalid buffers and source preservation.

This calculation does not resolve texture/change keys or perform GPU uploads.
Mesh-to-role bindings, motion-chain resolution and indexed OpenGL drawing
remain separate implementation and validation steps.
