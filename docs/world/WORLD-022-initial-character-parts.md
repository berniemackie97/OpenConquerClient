# WORLD-022: Initial local-character part assembly

The authenticated character-info snapshot supplies the body appearance source
and hair composite. At first world entry, the renderer resolves the base body
and, when the linked hair selector is active, the corresponding `armet` part.
The linked source identifier is constructed from the body source, the hair
composite's low three digits, and the recorded `0x1D0D8` hair family offset.
The part catalog resolves mesh and texture identifiers to their actual C3
resources. The `armet` part follows the body's `v_armet` motion attachment,
with bone zero sampled from the same initial idle frame as the body.

World-entry item snapshots (1008 subtype 1) describe tracked equipment, while
the active-equipment snapshot (1009 subtype 46) selects the item IDs to apply.
A tracked item alone does not activate its visual. The initial primary armor
and headwear sources use the equipped item type's low six digits, normalized
to tens and combined with the body appearance source. Headwear replaces the
linked hair source in the primary `armet` slot. Missing active-equipment IDs
are rejected rather than mapped to unrelated items.

The first world view still does not implement the full role-part system:
additional body and headwear variant layers, garment overrides, mounts,
head attachment records, texture/material overrides and action-driven motion
transitions remain outstanding.
The runtime continues to require the authorized C3 archive and role-part
indexes in its content set. Source package declarations can report omitted
archives even when the selected resource closure is installed.

## Appearance preparation during world play

The admitted world requests a decoded appearance from a worker. It keeps the
displayed role while the worker reads the C3 catalog, meshes, motion tracks and
DDS textures. At most one appearance is being prepared. The next frame that
finds completed work uses it only if it matches the current equipment sources.
The frame thread creates the OpenGL resources, swaps the renderer and releases
the old resources. The first world view waits for initial appearance
preparation. Leaving the world cancels and observes the outstanding load.

This follows the native division between worker execution
(DataThread.dll `0x1000113A`) and the frame pump's completion drain and
finalization (GraphicData.dll `0x10003A32`, `0x10003F1C`, `0x10003F44`).
The single outstanding request and replacement rule are client lifecycle
policies. See `20_notes/Rendering/hero-appearance-worker-cp25.md`.

A macOS OpenGL probe alternated two armor appearances using the curated 5517
content. The synchronous appearance operation took 3.725–14.269 ms over 30
repeat changes. With worker preparation, the context-thread upload and swap
took 0.081–0.603 ms, with pending polls at or below 0.048 ms. Three framebuffer
hashes (map only, base body and alternate armor) matched across the two builds.
The probe measured the appearance operation on Apple M4. Entry catalogs and
map/HUD texture streaming remain part of CP2.5.
