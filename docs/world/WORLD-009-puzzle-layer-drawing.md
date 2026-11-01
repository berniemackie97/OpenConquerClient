# WORLD-009: Puzzle layer drawing

The first map-puzzle drawing path uses the loaded `GameMapContent` and
`GameMapPuzzleResources` as its only scene inputs. It produces separate
ground and positioned-puzzle draw lists so the owning world scene can insert
scenery, actors, and animated overlays at their encoded component slots.

## Drawing contract

Puzzle bitmap tiles are 256 pixels square. Positioned puzzle overlays use
their two parallax header values to offset drawing relative to the projected
camera center; the ground layer uses the supplied projection offset.
Tile enumeration uses a fixed 800×600 logical range, regardless of host
window size. The fixture assets include the Newbie Village DMap, puzzles,
and ANI index. The separate full-content preflight decoded 94 referenced
textures.

## Code boundary

`WorldPuzzleDrawList.Ground` uses the projection offset supplied by the
future camera owner. `PositionedPuzzles` uses each map record's parallax
headers, the viewport and projected center, then centers type-8 bitmap
children. Empty cells are omitted; ground animation uses the supplied frame
counter while positioned puzzles request frame zero. The output
is culled to the logical viewport. The 800×600 range remains fixed regardless of host window size.

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
locally regenerate and verify the tracked content set before enabling
the world scene. Rendering still requires independent frame-output tests and real-driver testing.

The automated tests exercise the Newbie DMap, puzzles and ANI fixtures,
tile placement, parallax output, view clipping, and the fixed tile iteration
range. Generated texture fixtures do not replace the operator's full-content
preflight.
