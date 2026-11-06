# Client session ownership — CP2.1c

This completes the world / role / login ownership split begun in #235 and #236.
`ClientApplication` hosts startup, the window, graphics context/device and frame
loop. It holds one `ClientSession`, with no login/world flags or coordinator fields.
Its separate Created/Started/Disposed lifetime only guards running and disposing
the desktop host itself.

| Owner | Responsibility |
| --- | --- |
| `ClientLogin` | Login screen, authenticated catalog binding, login attempts, registration, loading presentation, startup audio and credential dragging. |
| `ClientRoleCreation` | Role screen, cached role assets, input, admission/return and close lifetime. |
| `ClientWorld` | Admitted session, ingress, map load, world state, scene, hero/HUD rendering and close lifetime. |
| `ClientSession` | Explicit phase transitions and routing among those owners; frame order and ordered shutdown. |
| `ClientApplication` | Startup/window/context/device/loop hosting and graphics-resource group. |

The login owner persists behind role/world presentation so existing catalog,
feedback, cached assets and return-to-login behavior survive. Keeping a resource
until its cleanup completes is distinct from choosing the active presentation.

## State transitions

| From | Trigger | To |
| --- | --- | --- |
| Login | Coordinator transfers accepted `NEW_ROLE` session | RoleCreation |
| Login / RoleCreation | Admitted session and world snapshot transfer | WorldLoading |
| WorldLoading | Scene/first textures presentable and authored loading gate complete | World |
| RoleCreation | Return, connection loss or failed screen construction | ReturningToLogin |
| WorldLoading / World | World begins closing | ReturningToLogin |
| ReturningToLogin | Role close observed, or world close observed and canceled load reaped | Login |
| Any live phase | Shutdown begins | Closing |
| Closing | All cleanup stages attempted | Closed |

Input and drawing route by this phase. ReturningToLogin draws login UI but blocks
another attempt until old ownership is released. A world constructor that closes
synchronously cannot be promoted back to WorldLoading. A failed world factory
leaves the transferred socket in a tracked cleanup task, observed during shutdown.

The frame order remains catalog/attempt/registration collection, handoffs, close
observation, role update, world ingress/load/appearance, feedback, first textures,
loading progression and drawing. World presentation still waits for the authored
loading completion (frame 99) when loading media exists; missing optional loading
media retains the existing direct presentation path. Login timing uses an injected
`TimeProvider`; the remaining game/render clock work stays in CP2.6.

## Shutdown and failure behavior

Shutdown stays on the desktop thread and runs sequentially:

1. Cancel catalog refresh; close role creation, login attempt, world/failed handoff
   and registration operations.
2. Close the window, releasing world graphics and the context resource group in
   their original order.
3. Release catalog/audio services, role fonts and login UI; clear session ownership.

`ClientShutdown` continues after cleanup failures. One failure keeps its original
exception/stack; multiple failures are retained in a flattened aggregate. Closed
session and host disposal are idempotent, including reentrant window callbacks.
Closed sessions reject subsequent frame updates.

The review found a cleanup gap in the extracted world owner: a failed map load
could skip ingress/session disposal. World disposal now reaps each stage even when
earlier stages fail and coalesces repeated calls. World graphics similarly attempt
all owned releases. Beginning close retains the network close task even if graphics
release fails. These changes affect cleanup failures, not packet/gameplay rules.

## Evidence and validation

This is a modern ownership design, not a claim that native classes use this enum.
Existing native behavior remains anchored in `20_notes/Architecture/03_login_flash_startup_command_contract.md`,
`11_login_ui_assets_and_interactions.md` (login loading presentation: frames 17/36/99,
`CMD_ENTERGAME`, `CMD_GAMESTART` and `CMD_CLOSE`) and `12_create_role_flow.md`.
No new native symbol edit or BNDB save is required.

Transition tests use the existing encrypted account redirect and pinned game
handshake/proof/creation/world-entry fixtures. They exercise both admission routes,
loading readiness versus the authored gate, return and connection-loss classification,
delayed load reaping, failed construction, shutdown in login/role/loading/world,
pending account/catalog cancellation and cleanup failures. The role route sends and
receives a walk on the original admitted socket after the handoff.

Headless world/screen boundaries control readiness and failure timing without
requiring a GPU for lifecycle tests. The loading-gate test uses a synthetic placement
timeline with the real loading state machine; existing native-asset tests remain
separate. This is not a running-client or native-framebuffer comparison.

Local validation on SDK 10.0.403: locked restore, zero-warning Release build,
full format verification and 2,820 focused tests pass (Client 1,485, Content 750,
Rendering 499, Platform 86). Twenty-two client cases are new. Networking, native
role commands, authentication/registration coordinators and gameplay calculations
remain unchanged.

Gameplay-model extraction, packet parsing, remaining clocks/logging and broad doc
refresh retain their later checkpoints. CI continues to condition private-asset
gates on retail content availability.
