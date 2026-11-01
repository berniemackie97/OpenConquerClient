# WORLD-011: Camera projection and puzzle frames

The Newbie Village camera contract operates on interpolated hero world-pixel coordinates. It does not infer
camera position from the last network tile. `WorldCameraProjection.ProjectTile` converts a tile coordinate
to world pixels independently. This slice does not activate the world scene.

## Projection

`WorldCameraProjection.FromHeroWorldPosition` calculates the visible area and clamps the camera against
the ground-puzzle bounds, including the map-height parity offset. The upper bound is applied before the
lower bound. Its result contains the camera center used for parallax and the offset supplied to ground
tile enumeration.

The scale denominator is 256. For a requested zoom percentage, the divisor is
`zoomPercent * 256 / 100`, and the visible dimensions are
`screenWidth * 256 / divisor` and `screenHeight * 256 / divisor`.

The Newbie Village test fixture uses a 132×132 cell grid and a 15×9 ground puzzle with base
coordinates (2304, 960). At 800×600 and 100% zoom, tile (90, 35) projects to (5984, 2016),
giving a clamped center of (5344, 1716) and ground offset of (−3040, −756).
These expectations are asserted in `WorldCameraProjectionTests`.

## Drawing and frame timing

`WorldPuzzleDrawList.PositionedPuzzle` enumerates one map overlay and one puzzle child at a time.
The caller supplies a frame for both positioned and ground puzzle enumeration; each bitmap keeps
its own animation counter.

`WorldPuzzleAnimationClock` starts on its first render. When the unsigned millisecond interval
has elapsed, it advances by at most one frame, after drawing. The interval comes from
`[PuzzleBmp]FrameIntervalMilliSec` (default 1000). The clock is not started by construction.

`WorldPuzzleTextureRenderer.Draw` requires the map's world color in its render input. The sprite
renderer applies alpha blending rather than implicitly treating each puzzle bitmap as white.

Map components are ordered as ground, floor, surface, scenery and sky; type-4 overlays are
inserted at their encoded component index. Newbie Village's type-4 overlay uses index zero,
placing its background ahead of the ground. Complete component dispatch remains outside this slice.

## Remaining integration

Live world entry is still disabled. The scene requires the remaining content dependencies,
scenery spatial ordering, character composition, complete component dispatch, window lifetime
management and render-output testing. Edge scroll, oscillation and world-color transitions belong
to their owning input and state systems; projection receives their calculated values.
