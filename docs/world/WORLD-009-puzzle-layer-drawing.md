# WORLD-009: Retail puzzle layer draw path

The first map-puzzle drawing path uses the loaded `GameMapContent` and
`GameMapPuzzleResources` as its only scene inputs. It produces separate
ground and positioned-puzzle draw lists so the owning world scene can insert
scenery, actors, and animated overlays at their actual native component slots.

## Native evidence

- `20_notes/Rendering/05_dmap_positioned_overlay_system.md`: the shared
  `CMapPuzzleBitmap_RenderAtOffset` at `0x63A04B` draws 256-pixel tiles;
  `CGameMap2DPuzzleOverlay_Render` at `0x64B848` centers its bitmap against
  the map dimensions; the containing type-4 component applies percentage
  parallax using its two header integers.
- `20_notes/Game_Map/cgamemap_class.md`: map components receive the
  projection offset, which comes from the client camera calculation.
- The archived `CMapGroundPuzzleBitmapRender` and
  `CMapPuzzleBitmapFromAssets` implementations document the native
  800×600 tile-enumeration range and the three projection-scale branches.
  Those implementations are supporting reference material, not substitutes
  for the instruction-level findings.
- Existing retail Newbie Village fixtures supply `GameMap.dat`,
  `newbie.7z`, both `.pul` bitmaps, and `newbie.ani`. The separate
  operator preflight verified the 94 actual referenced source textures
  decoded successfully from the user's clean retail 5517 installation.

## Code boundary

`WorldPuzzleDrawList.Ground` uses the projection offset supplied by the
future camera owner. `PositionedPuzzles` uses each map record's parallax
headers, the viewport and projected center, then centers type-8 bitmap
children. Empty cells are omitted, each animation selects its authored frame
by index, and the output is culled to the logical viewport. The native
800×600 range remains fixed regardless of host window size.

`WorldPuzzleTextureRenderer.Prepare` resolves frames with the existing
`AniFrameLoader`, decodes actual DDS/TGA bytes through
`LooseThenPackage`, and uploads a texture once per distinct frame path.
`Draw` performs GPU draws only; `Dispose` releases its owned textures.
Preparation and drawing must run on the graphics-context owner thread.

The draw path currently rejects time-scrolling PUZZLE2 bitmaps instead of
silently rendering them stationary. The API accepts an explicit frame index,
projection center, offset and scale; it does not invent a camera, scrolling
clock, or player position. It does not render type-3 animated overlays,
type-3 scenery, the hero, or weather.

## Integration boundary

The published runtime content closure does not yet contain all world-map
assets. No live entry transition or product content payload is changed in this
slice. The owner must add the complete required map scene dependencies and
locally regenerate and verify the tracked retail content set before enabling
the world scene. A drawing implementation alone does not establish visual
parity; rendered frames still require comparison with independent retail
5517 captures and real-driver testing.

The automated tests exercise the original retail Newbie DMap, puzzles and
ANI file, native tile placement, parallax output, view clipping, and the fixed
tile iteration range. They do not substitute generated graphics for the
user's independent retail texture preflight.
