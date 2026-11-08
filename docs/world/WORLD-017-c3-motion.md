# WORLD-017: C3 motion decoding and matrix sampling

## Evidence and scope

`C3_CORE_DLL.dll:0x100092F0` parses the MOTI tags and implicit matrices, and `0x10009A20` samples keyed bone matrices. See `20_notes/Rendering/c3_core_dll/12_motion_load_formats.md` and `13_motion_sampling_and_blend.md` for the trailing seek, interpolation and secondary blend. Frame-major storage and bounded allocation are managed choices.

A `MOTI` section stores a bone count, frame count and a matrix-key encoding.
The reader uses the existing C3 section boundaries and accepts four layouts:

| Layout | Key index | Matrix data |
| --- | --- | --- |
| `KKEY` | 32-bit | 16 little-endian floats per bone |
| `ZKEY` | 16-bit | quaternion XYZW and translation XYZ per bone |
| `XKEY` | 16-bit | twelve affine matrix floats per bone |
| Implicit frames | frame position | 16-float matrices stored bone-major |

The implicit layout begins its matrices immediately after the first two
counts. Its first matrix's initial four bytes are not a separate encoding
field; the reader reuses those bytes when no recognized encoding is found.
Decoded matrices are stored frame-major for constant-time bone access. The
source key indices are kept in stream order.

The loader enforces the 1,000-bone limit, limits the payload to the existing
C3 content budget, checks array sizes before allocation, and validates the
trailing section's relative seek. A motion's trailing section may use signed
32-bit wrapped arithmetic; the seek itself must remain within its section.

`C3MotionSampler` returns the first or final key outside the keyed range.
Inside the range, it interpolates all 16 matrix elements as
`(1 - t) * previous + t * next`. A secondary motion uses that same
element-wise operation with the secondary weight, without clamping.
Missing bone/motion samples yield identity; a missing primary blend leaves
the destination unchanged.

The two-bone, 101-frame independent geometry fixture records the first
bone matrix as constant across all 101 frames. The implicit-frame test
retains those recorded matrix values and checks bone-major addressing.

This slice decodes and samples MOTI data; it does not apply matrices to PHY
vertices, resolve motion attachment chains, or submit GPU draw commands.
Those require their own verified integration and runtime tests.
