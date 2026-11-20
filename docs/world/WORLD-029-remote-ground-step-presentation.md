# WORLD-029: Remote ground-step presentation

## Evidence and scope

Native contract: `20_notes/Network/15_msgwalk_10005_direction_index_order.md`, section
"Remote step presentation" (5517 `Conquer.exe`, verified 2026-11-20). This covers relayed
10005 ground walk and run steps for visible players. Mounted 10005 steps and jump
presentation are not reconstructed here; remote ground jumps are covered by [WORLD-030](WORLD-030-remote-ground-jump-presentation.md).

## Behaviour

Each visible player owns a `WorldRolePlayback` that reproduces the native remote-role step pipeline:

| Stage | Client behaviour | Native anchor |
|---|---|---|
| Arrival | Commit the unfinished step to its end, then queue the new step. The registry tile updates at once, so visibility culling is unchanged. | `Role_HandleIncomingActionStateBatch` 0x6720EE, label 0x672276 |
| Tick | Interpolate while the step runs. Once it is complete, start one queued step, or stand when none is queued. | `Role_TickCommandStateAndDispatchPlayback` 0x672662..0x6726E0 |
| Staging | Start at the current cursor; end at the projection of the cursor tile plus the direction delta. | 0x73631C |
| Action | Walk: 111 after 110/120, else 110. Run: 121 after 120, else 120. Keyed on the last physical action, which persists across standing. | 0x736329, 0x736AEC |
| Timing | Clip frames × 25 ms. The frame index is rounded `(elapsed + 12) / 25`, and the step completes when it reaches the clip length. | 0x64EB0C, 0x64EB70 |
| Position | Linear: `start + roundHalfStep(delta × elapsed / total)`, where `roundHalfStep(v)` is `t = trunc(v); u = trunc(v + 0.5); u > t ? t + 1 : t` (`Math_RoundDoubleByHalfStepCompare` 0x768AA1). ActionCtrl.ini has no record for actions 110/111/120/121. | 0x64EE59..0x64EED4, 0x768AA1 |
| Depth | Mid-step, the start tile when the direction index ≤ 3, otherwise the end tile. | 0x672784 |
| Drain | Standby with both endpoints at the cursor. | 0x7335BA..0x7335D6 |

The scene rebuilds its depth slots only when the registry revision changes (spawn, removal,
first placement, step boundary). The interpolated anchor is supplied every frame through
`WorldMapScene.SetRemoteRolePositions`.

## Modern policies

- The native deque is unbounded. The client keeps at most 64 pending steps; a deeper backlog
  is committed instantly so a flooding peer cannot grow memory.
- If a peer's appearance is not prepared yet, its clip length is unknown, so the step runs for
  one 25 ms frame instead of blocking.
- Mounted steps and ground jumps place the role without a stride. They do not change the last
  physical action.

## Open

- The rendered animation frame still uses the floor sample `elapsed / 25`. The native frame
  passed to RoleView is in a `Role_TickAndRender` tail that Binary Ninja splits, and it is not
  yet verified.
- Native handling of a 10014 re-spawn for a role that is mid-step is not traced. The client
  replaces the role.
- `CRole+0x24` frame-interval override writers outside init are not enumerated. Init sets 0,
  and commands 0x16/0x17 do not write it.

## Verification

Golden values in `WorldRolePlaybackTests` come from the native projection and interpolation
formulas. On 2026-11-20 a live session had the original 5517 client and OCC on one OCS realm.
The native character's walk, as drawn by OCC, matched native movement in a frame burst.
