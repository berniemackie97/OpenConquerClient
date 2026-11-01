# WORLD-011: Native 5517 camera and puzzle frame contract

The Newbie Village (MapDataId 1010) camera path is derived from the native
\`Conquer.exe\` disassembly in
\`20_notes/Game_Map/world_render_contract_5517.md\` (2026-10-31) and the
corresponding camera/animation evidence exports. This work does not start a
live world scene.

## Camera/projection

\`WorldCameraProjection.FromHeroWorldPosition\` consumes **interpolated hero
world pixels** from the future actor owner. It does not infer those pixels from
uninterpolated network tile coordinates. A separate \`ProjectTile\` method
implements the native static tile projection.

Native anchors: \`0x61D850\` (tile projection), \`0x5ACAE8\` (hero − half
viewport), \`0x61E375\` (puzzle base), \`0x61E0E7..0x61E300\` (camera
clamp/commit), \`0x61F0BD\` (zoom), and \`0x61E3D8\` (ground offset).

The native scale denominator is 256; the zoom divisor is
\`zoomPercent*256/100\`, and visible dimensions are
\`screenWidth*256/divisor\` and \`screenHeight*256/divisor\`. Clamp
bounds derive from the actual ground-puzzle dimensions, including the
map-height parity offset on Y. Upper bounds are applied before lower bounds.
The returned \`WorldPuzzleProjection\` supplies both camera center for
parallax and the ground bitmap offset to the existing tile enumerator.

Retail Newbie dimensions: grid 132×132, ground puzzle 15×9; ground
base (2304,960). The real fixture and an independently checked hero tile
(90,35) produce world (5984,2016), clamped center (5344,1716), and
ground offset (−3040,−756) at 800×600 and native 100% zoom.

## Drawing and timing

\`WorldPuzzleDrawList.PositionedPuzzle\` now works **one DMap record and
one puzzle child at a time**. Each bitmap owns its own frame counter; a
single combined list would erase the component insertion and timing
boundaries. The caller supplies the frame to both puzzle draw-list
methods.

\`WorldPuzzleAnimationClock\` implements the exact native post-draw
advance at \`0x63A5B0..0x63A5D9\`: a per-bitmap frame counter, initialized
on its first render; increment **once**, after drawing, only if the
unsigned millisecond interval elapsed. It accepts the interval from the
native \`[PuzzleBmp]FrameIntervalMilliSec\` configuration (default 1000)
rather than inventing a timer. No clock is started automatically.

\`WorldPuzzleTextureRenderer.Draw\` now requires the map's native world
color (ARGB \`map+0x13C\`, adapted to \`SpriteColor\` by its future
owning scene). The sprite renderer applies alpha blending; it no longer
implicitly draws everything white.

Native map component order is
\`[ground, floor, surface, scenery, sky]\`, with DMap type-4 records
inserted at their encoded index. Newbie's only type-4 record has index 0:
**the parallax puzzle background must draw before ground**. This
source ordering is asserted in the fixture test but the final full
component dispatcher remains out of scope: it must not flatten scenery
into puzzle batches or silently skip required layers.

## Still gated

Actual live world entry remains disabled: full retail scene-content closure,
scenery spatial-bucket ordering, role/3D composition, complete component
dispatch, window lifetime, and independent GPU screenshot parity are not
established by this isolated draw path. Runtime camera edge-scroll
conditions, oscillations and world-color transitions require their owning
input/state systems; this API accepts already-calculated offsets,
zoom percentage and color but does not synthesize those systems.
