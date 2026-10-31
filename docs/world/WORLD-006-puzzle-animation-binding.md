# WORLD-006: Bind retail puzzle tile indices to ANI frames

The DMap reader selects map-data ID 1010; WORLD-005 loads its ground and
positioned-overlay PUZZLE bitmaps. This slice completes the next content
lookup: each non-empty ushort tile index selects the `Puzzle<N>` section
in the ANI index named by that bitmap's `TextureIndexPath`.
The existing `AniIndexFile` parser remains the source of truth for ANI
section hashing, frame ordering, and frame-count limits.

`GameMapPuzzleResources.Load` resolves each distinct puzzle bitmap and ANI
index once per loaded map. It binds each distinct, referenced tile index to
the corresponding `AniIndexSection` once, then exposes its original
ordered `FramePaths` to the future scene renderer. The 0xFFFF empty-tile
sentinel is never resolved. An unavailable ANI file or missing/empty
referenced section fails map loading rather than silently dropping artwork.
Duplicate puzzle paths and shared ANI files do not trigger extra loads.

The unchanged retail `ani/newbie.ani` (6,860 bytes, SHA-256
`458d1714f455576df74c866bdd5027b1fc79d227bf6134353374bba2876c79c3`)
was extracted directly from `5517.7z`. Its 96 sections, `Puzzle0`
through `Puzzle95`, each contain one original DDS frame reference.
The ground `newbie.pul` references 58 distinct non-empty tile indices;
the positioned backdrop `newbiebg.pul` references 36. Every referenced
section exists in this retail ANI. Regression tests restore the original
ANI bytes from a compressed fixture and use the unchanged retail DMap and
puzzle files. Invalid ANI references are tested by mutating the fixture.

Evidence: `20_notes/Rendering/05_dmap_positioned_overlay_system.md`
(`CMapPuzzleBitmap_RenderAtOffset`, 0x63A04B),
`20_notes/Rendering/07_ani_frame_animation_runtime.md`, and the
original archived map-puzzle runtime's `Puzzle{puzzleIdx}` query path.

**Boundary at WORLD-006:** This binds authored ANI frame paths but does not itself
load DDS bytes or create GPU sprites. WORLD-007 added DXT1 decoding, and
WORLD-008 added operator-only puzzle-frame validation. Neither change activates
a world renderer, expands the shipped content closure or changes world admission.
The runtime path remains disabled until its scene and asset dependencies
are imported, verified and owned by a real world session.
