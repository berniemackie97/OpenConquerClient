# WORLD-022: Initial local-character part assembly

## Evidence and scope

Character information supplies appearance and hair fields. Part decomposition
and ordering are documented in `20_notes/Entity_System/role_visual_construction.md`
and `Rendering/11_3d_role_part_loaders.md`. The worker/finalization claim in
the later section still requires a retained, independently inspectable trace;
the supplied research archive does not contain the previously cited worker
note. Application preparation preserves presentation until the chosen assets
are installed.

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
