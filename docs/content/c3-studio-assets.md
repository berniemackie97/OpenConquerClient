# C3 compatibility assets for Studio

The existing Content project now provides a self-contained `.ocmodel` compatibility resource.
Rendering owns the existing animated mesh helper and a preview host. No additional assembly was
created. This is a library foundation for Studio; desktop character import and bone-editing UI
are not delivered by this client PR.

## Preservation

The resource retains complete original C3, texture and relevant catalog files, not only the
current decoder's output. Source paths, lengths and SHA-256 digests are recorded in sorted order.
This retains the file header, section order, unknown sections, raw vertex layouts and count pairs,
opaque PHY fields, key arrays, optional tags, MOTI encodings and trailing regions byte for byte.
Material/texture IDs (including secondary texture IDs), blend/state bytes, named virtual submeshes
and external motion bindings are explicit metadata. A missing required dependency rejects import.
Playback timing and repeat settings are editable without resampling or rewriting original data.

`C3ModelImporter.Character` captures the currently implemented body and initial idle selection.
Its 66 ms frame timing and body/action lookup follow WORLD-021. It does not guess other action
key formulas. `C3ModelPreview` decodes CPU data through the existing PHY/MOTI readers; rendering
uses the same matrix sampler, CPU skinner and indexed OpenGL draw as the client.

## Binary layout, version 1

All integers are little-endian. The 48-byte header contains magic `OCM3` (4 bytes), version
(int32), JSON length (int32), payload length (int32), and SHA-256 of JSON plus payload (32 bytes).
JSON contains the model GUID, part descriptors, clip settings, virtual mesh identifiers and a
sorted source-file table. File bytes follow in exactly that table order, with no compression,
timestamps, padding or host paths. Equal model identities/settings/sources produce equal bytes.
Hashes provide integrity, not publisher authenticity.

Limits: 1 MiB JSON, 64 MiB source payload, 128 files, four parts, 64 clips, 16 ordinal geometry
tracks per part, one million decoded vertices and one million decoded matrices. Unsupported
versions, invalid lengths/digests/paths, absent dependencies, nonfinite motion matrices and
unsampleable key sequences fail explicitly. Inputs are owned snapshots, not mutable source views.

## Native evidence and remaining rendering work

Contracts come from `client-5517/20_notes/Rendering/c3_core_dll/05_phy.md`,
`12_motion_load_formats.md`, `13_motion_sampling_and_blend.md`, and
`role3d_dll/12_linked_motion_chain_resolution.md`. Existing source notes are authoritative;
this change adds no newly reverse-engineered native claim.

Full 4x4 matrices and element-wise interpolation remain intact. Indexed matrix channels are
not converted to a fabricated parent hierarchy or a TRS-only skeleton. Named virtual meshes
remain available for the documented linked-part chain. Source retention is broader than preview
support: complete equipment/hair composition, linked-part rendering, secondary texture stages,
PHY alpha/draw/texture-key playback, billboard/UV-step rendering and action transitions remain
separate implementation work. The preview does not establish complete native draw parity.

This compatibility resource is not a frozen universal format for new games. A later native
mesh/rig/material authoring schema must retain these semantics and must not replace this lossless
source preservation until real-corpus round trips and render comparisons cover its projection.
