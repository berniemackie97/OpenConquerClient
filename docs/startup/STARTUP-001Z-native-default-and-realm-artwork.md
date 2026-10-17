# STARTUP-001Z — Native startup selection and realm tile correction

Baseline: PR #108 at `eb9a9b6f4c7fef79a971f462820686c1d12b7fef`.

## Source authority

- `20_notes/Architecture/11_login_ui_assets_and_interactions.md`: stock INI `GroupRecord=0`, `ServerRecord=0`; Flash invokes first region then first server following the original transition; `DefineSprite_179` and status-light `DefineSprite_34`.
- `20_notes/Architecture/03_login_flash_startup_command_contract.md`: startup status field seed `3` (`0x4AD835`); a distinct refresh path resolves live status. The original `flushLight` accepts codes 1–4 and defaults invalid codes to state 4; missing entries hide the light.
- Clean 5517 `start.dat`: `DefineSprite_179` frame 1 and `DefineSprite_34` frames 1–4. SHA-256 provenance is in `STARTUP-001Z-native-default-and-realm-artwork.json`.

## Scope

- Original idle server frame (source-exact 155×59 crop, with independent status light removed from the crop).
- Original four-state 18×18 status-light atlas. Render retail initial state 3 **only on present signed-catalog entries**, and never use its color as a health check or routing authority. Genuine live status requires an authenticated producer/schema extension; currently catalog v2 has no state.
- Initial native `(0,0)` selection when the **first current authenticated** catalog is installed, not before and not automatically after revocation, renewal mismatch, or expiry. Missing signed directory remains fail-closed.
- Suppress the Group1-specific region rollover overlay in generic region lists. It was the cause of false startup hover visuals. The selected-region flame and realm hover/selection effects remain unchanged.

## Remaining PR #108 work (not falsely claimed complete)

Exact region-specific movie mapping and hover transitions, source-authentic region/server paging controls, 12-slot staggered fade/loading phases, loading/error dialogs, Caps Lock and tooltip skin, 1:1 Windows typography and GPU parity comparisons. Source note 11 retains the evidence boundaries for each item. No fake standalone controls, fabricated status telemetry, arbitrary links or simulated login transport.

## Acceptance

Release restore, formatting, build, complete tests and content-set verification; both 1024×768 and 800×600 runtime. With signed catalog, the first region and first valid server are selected on initial screen without a pointer hover. The 25-frame flame and 47-frame realm effect loop. Status lights appear only alongside actual servers. Confirm that signed catalog loss revokes the selection and that the native green startup status is not treated as an authenticated live-online assertion. Compare against native screenshots before merging.
