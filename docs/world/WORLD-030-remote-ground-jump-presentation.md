# WORLD-030: Remote ground-jump presentation

## Evidence and scope

Native contract: `20_notes/Network/15_msgwalk_10005_direction_index_order.md`, section "Remote jump presentation"
(5517 `Conquer.exe` and `RoleView.dll`, verified 2026-11-20; float constants read from the PE). This covers
MsgAction 137 ground jumps with flag 0 (command 0x14) for visible players. The local hero's jump presentation,
flag-nonzero jumps (command 0x15), mounted (0x2C9) and status-0x1B (0x1FE) variants are not covered.

## Behaviour

A relayed jump goes into the same per-role queue as walks (`Role_HandleIncomingActionStateBatch` 0x6720EE): the
unfinished step is committed, and the jump starts on the next tick after the current motion completes.

| Stage | Client behaviour | Native anchor |
|---|---|---|
| Staging | Action 130. Direction toward the target via the native 8-way selector. Start = cursor, end = projected target tile. | 0x73597B, 0x61E478, 0x6E4ACC |
| Interval | `trunc(33 · distance / 300 + 1)`, floored to 33, where distance is between the projected endpoints. | 0x64DEF4..0x64DF50 |
| Stretch | A clip shorter than 20 frames: `ceil(I · 19.5 / (frames − 0.5))`, so the jump lasts as long as 20 frames. | 0x64E727..0x64E7E2 |
| Position | Linear with native half-step rounding, completing on the rounded frame (same as walks). | 0x64EE59, 0x768AA1 |
| Depth | Tile under the moving cursor (unclamped inverse projection each tick). | 0x64EEFD, 0x61D707 |
| Facing | `−270 − trunc(atan2(endY − startY, endX − startX) · 57.29578)` degrees while airborne. Standing afterwards uses the staged direction. | 0x64F6FB |
| Arc | Terrain-aware half-sine: `apex = trunc(dist/3 + peakAdjusted) + 20` at the start/end midpoint. Peak = first maximum tail elevation on the rounded source-excluding line. | 0x6505B3, 0x61E886, 0x620171 |
| Transform | `terrainDelta = adjusted(cursor tile) − arc`. Anchor y = `cursorY − terrainDelta − arc`; RoleView elevation = `2 · terrainDelta` (non-project camera), applied as `Scale · Translate(0,0,elev) · RotZ`. The shadow pass shares the same base transform. | 0x64F811, RoleView 0x1000329A |

Tail-link elevation (`GameMapTerrainElevation`) composes the cell-grid base elevation with every terrain-object cell
delta in map order. The object anchor is the group anchor plus the object offset, and cell (x, y) lands at tile
`anchor − (x, y)` with 16-bit wraparound (0x635FC5, 0x63F4D3, 0x644856).

## Modern policies

- If the clip is not prepared, it counts as one frame, which the 20-frame stretch then gives the native duration.
- Peers' status flags and transform state (`CRole+0xEC`) are not modelled. The jump always uses action 130 and the stretch always applies.

## Open

- Native returns an unspecified value when the cursor tile has no map entry (`Role_GetTerrainHeightDeltaFromProjectedScreenPosition`). The client uses adjusted elevation 0 off the grid.
- When the rounded line resolves no tile, native reads uninitialised peak coordinates. The client returns arc 0.
- The rendered animation frame still uses the floor sample, as in WORLD-029.
- Zoom other than 100% subtracts the terrain adjustment in world pixels. Native subtracts it from the projection-centre offset.

## Verification

`WorldJumpArcTests`, `GameMapTerrainElevationTests` and `WorldRolePlaybackTests` use hand-computed golden values
from the native formulas. On 2026-11-20 a live session had the original 5517 client and OCC on one OCS realm on
map 1010. The native character's Ctrl+click jump, as drawn by OCC, rose on the arc in the jump pose, landed on the
target tile and stood.
