# Texture project and build contracts

## Project layout

```text
project/
  project.ocstudio
  .studio.lock
  Inputs/
    <sha256-of-pixels>.rgba
  Builds/                         optional local build destinations
```

Original source files stay outside the project and are never written by import. `Inputs`
holds normalized top-left RGBA8 pixels with straight alpha. Pixel blobs are immutable and
addressed by lowercase SHA-256; files are not asset identities. Authoring edits are settings
in the project document. Builds are independent runtime files, never editable sources.

`project.ocstudio` is UTF-8 JSON, version 1. It contains a project GUID, name, and asset
records with GUID, name, dimensions, pixel digest, source format/digest, optional source key,
horizontal flip, and clockwise quarter turns. Stable GUIDs survive renames. Source keys are
tool metadata, not runtime dependency references or client replacement bindings. Source file
paths are not persisted. Replacing an image preserves the original source key as its import
association while updating the encoded-source digest and format. Assets serialize in GUID order with invariant JSON numbers and
fixed property order, without machine paths or timestamps.

Source formats are `tga`, `dds`, `rgba` (a decoded image adapter), and `created`. File imports
record the encoded source digest; decoded-image adapters and generated images record the
original pixel digest. Flip is applied before clockwise rotation. Names are 1–128 characters
without surrounding whitespace or control characters. Paths are generated only from fixed
segments, GUIDs, and validated lowercase digests.

The document is limited to 1 MiB, 64 assets, 16 MiB of pixels per image, 4096 pixels per axis,
and 64 MiB of aggregate source pixels per project. Encoded image input is limited to 16 MiB.
These are explicit first-slice allocation budgets, not measured optimal game-content limits.
Transforms, import snapshots, decoder arrays, preview bitmaps, and verification may require
additional bounded copies; the aggregate source-pixel budget is not a process memory limit.

Readers reject duplicate/unknown JSON properties, missing required properties, malformed
identities, unsupported versions/settings, invalid digests, and inconsistent dimensions.
Unsupported project versions are refused without writing; there is no automatic migration.
A future schema change needs an explicit migration and compatibility policy.

## Save and failure behavior

An exclusive `.studio.lock` lease prevents simultaneous Studio/CLI editing sessions. The
manifest digest also detects outside edits before saving. New pixel blobs are written and
flushed first; the complete manifest is flushed in a sibling temporary file and atomically
renamed over the previous manifest. The manifest is the commit point. Failed/canceled saves
retain the previous manifest and unsaved session state. They can leave unreferenced immutable
input blobs, which are safe to retain. No speculative garbage collector is implemented.

Existing symbolic links/reparse points are rejected in selected paths and all ancestors,
including input and destination files. Use real filesystem paths, not linked shortcuts; on
macOS a temporary path may need its `/private` spelling. Projects must reside in directories
the user controls. Checks and leases do not defend against a hostile process with the same
filesystem privileges replacing directories between checks, deleting lease files, or racing
external writes. Network filesystems must provide the required locking and rename semantics;
local filesystems are the supported authoring storage for this slice. Flush and rename provide
atomic visibility, not a claimed power-loss recovery protocol for every filesystem.

## Compiled content

Compilation requires saved data, emits assets in GUID order, stages output next to the final
destination, validates it, then publishes by a non-overwriting directory rename. Failure or
cancellation removes the staging directory. Existing destinations are rejected. Validation
checks the exact directory file set, lengths, SHA-256 digests, identities, dimensions, and
each runtime texture payload. No directory recursion, archive extraction, timestamps, or
source-format dependencies are involved in runtime loading.

`content.json` version 1 contains the project GUID and ordered texture entries: asset GUID,
name, compiled dimensions, exact byte length, and SHA-256 of the entire `.octex` file.
Filenames are `<asset-guid-without-hyphens>.octex`; the manifest cannot inject paths.

The current uncompressed `.octex` transport is deliberately small:

| Offset | Bytes | Meaning |
| --- | --- | --- |
| 0 | 4 | ASCII `OCTX` |
| 4 | 4 | Version 1, little-endian signed integer |
| 8 | 16 | Asset GUID, big-endian GUID byte order |
| 24 | 4 | Width, little-endian signed integer |
| 28 | 4 | Height, little-endian signed integer |
| 32 | 4 | Pixel byte count, little-endian signed integer |
| 36 | 32 | SHA-256 of pixels |
| 68 | width × height × 4 | Top-left row-major RGBA8, straight alpha |

The runtime reader bounds dimensions before allocation and requires exact coverage, a
nonempty identity, supported version, and matching pixel digest. It leaves caller streams
open. The build manifest additionally binds header bytes and identity to an expected asset.
Digests do not authenticate content from an untrusted publisher. This transport is not an
optimized GPU texture format or a frozen universal scene/map format; compression, streaming,
and GPU upload decisions need real consumers and representative measurements.
