# STARTUP-001Q — Retail 5517 Enter / Exit artwork and safe Exit command

## Why this slice

Resume **retail startup/login-screen reconstruction** after the merged STARTUP-001P credential alignment. The earlier STARTUP-001O proposal was not merged and is superseded by this rebased, verified STARTUP-001Q delivery. The signed-catalog, pagination, directory-status, and credential-alignment slices are retained unchanged; none shipped the Enter and Exit images present in the original clean 5517 login movie. This slice restores these exact retail-derived assets and a safe host-window close action. It does **not** authorize an authentication request: the native account transport is not yet integrated with credential entry, so `Enter` remains visually disabled and consumes no submit command. The separate type-1060 framing utility is unchanged and still dormant.

## Source evidence and provenance

- Binary asset source: clean-5517 `data/main/start.dat` (CWS SWF version 6) in `OpenConquer-5517-Login-Reference.zip`.
- Relevant native evidence: `20_notes/Architecture/03_login_flash_startup_command_contract.md` (`CMD_ENTERGAME` / `CMD_GAMESTART` -> `0x480`; `CMD_EXIT` -> `0x55`) and `05_startup_data_files_and_native_replacement.md` (loginFlash presentation / `flashLogin.ini`). A native exit command closes the client through the host action, not account-server transport.
- Offline extraction: `DefineBitsJPEG3` (SWF tag 35), decompress the CWS root, read the JPEG RGB payload, apply the decompressed per-pixel alpha mask, convert to straight-alpha RGBA, Brotli-compress each frame. No SWF interpreter, ActionScript, runtime JPEG decoding, remote image, or dynamic asset file path is shipped.

| Original SWF bitmap character | State | Source size | SHA-256 decoded RGBA |
| --- | --- | --- | --- |
| 311 | Enter up | 132×40 | `41fdea3a98e653cb2c991ce9b656d41ac97d3e828ce32e1f5362174a51d3e404` |
| 313 | Enter hover | 132×40 | `8ca3dffe0f2f1850ea61872e137003e54ec8c6a16d68606157ad6cbd85946b83` |
| 315 | Enter pressed | 132×40 | `5056f3f7098a2d935af7faf550cd3cff1ce5a4957832935c5fc2cdca5db212a8` |
| 318 | Exit up | 132×40 | `57121511c04031b7cde18efc93a23728adc19bae695280f4d6a9678de6283730` |
| 320 | Exit hover | 132×40 | `fe32a09db879dda1b73e8c5012ca8fab5aa45b9fa845ccf4a03508660d89d794` |
| 322 | Exit pressed | 132×40 | `37442d367079c3f6b1ad3e6c9e8fb7eabc615f10b10491ad631e4b9518a3bd1a` |

The SHA-256 values are independently checked at runtime-test level after decompressing embedded resources. The **asset pixels are native-derived**. The previous first-party placement incorrectly stacked Enter at `(768,642)` and Exit at `(768,690)`. The screenshot-aligned replacement places Enter at `(654,726,124,34)` and Exit at `(862,726,124,34)` on the 1024×768 reference stage. Both are in one bottom row, matching the supplied native client screenshot's visible button positions. That screenshot identifies version 5631, so these are visually calibrated 5517 reconstruction coordinates, **not verified 5517 Flash `PlaceObject` matrices**. The 132×40 source textures remain unchanged and are rendered at the calibrated visual dimensions. Neither position is represented as proven native `PlaceObject` evidence. Existing region/server grids, header/footer, dragon and account-panel placements are unchanged. Enter overlaps only the fully transparent bottom-right edge of the account-panel **sprite rectangle**; it does not cover source-art pixels or either credential interior. Input-layout validation therefore rejects collisions with the actual credential fields rather than forbidding all overlap with the image's transparent bounds. The client regression test verifies the overlapping bitmap pixels have zero alpha. All placements scale deterministically at 800×600 and 1024×768 and are outside account fields. Source-art image files are not added to the retail content-set manifest under fictitious retail paths.

## Interaction and authorization boundaries

- The Enter sprite is always rendered in a visibly muted disabled state. Pointer activation within its bounds is consumed but never connects, formats credentials, grants world entry, or posts a speculative command. The hover/pressed Enter resources are packaged now for future verified, authorized transitions; they are not used to imply a wired authentication path.
- Exit uses the original bitmap states, not procedural substitutes. A left pointer press/release within the exact Exit bounds requests an orderly desktop close. Pointer capture is cancelled on focus loss or when release falls outside logical viewport; releasing a stale press does nothing. Letterboxing remains inert. No global keyboard shortcut is hijacked.
- `DesktopWindow.RequestClose()` calls the normal Silk.NET `IWindow.Close()` on the existing desktop event thread. It does not call `Environment.Exit`, dispose the window from inside a callback, or bypass normal OpenGL shutdown. The existing application teardown continues to clear credential buffers and cancel catalog refresh.
- All image textures are created once on OpenGL context initialization, with reverse-order partial-construction cleanup, and disposed before the graphics device. Draw does not decode assets, generate per-frame fonts, or allocate new GPU textures.
- There is no registration action in this slice; native `CMD_GETURL` must be assessed separately with explicit allowlisted HTTPS destination policy instead of launching publisher-era arbitrary URLs.

## Upcoming authenticated directory / icons (separate retail startup work)

The original clean 5517 data is named **`Server.dat`**, not `GameServer.dat`: the decoded `outenserver` hierarchy has `Child`, `FlashName`, `FlashIcon`, `FlashHint`, `ServerName`, `ServerIP`, and `ServerPort`, per `20_notes/Architecture/05_startup_data_files_and_native_replacement.md`. The modern client already uses a signed directory for endpoint authority. Next directory/icon work should preserve that signed trust boundary and add a bounded *closed set of icon identifiers*, mapped to preconverted native-derived images in the published content assembly. The catalog must not supply file paths, remote URLs, executable Flash, or fallback unverified endpoints. Asset IDs, cryptographic signature freshness, and display metadata will need separate test coverage before enabling live realm/icon selection.

## Acceptance

1. `dotnet restore` locked, format verification, Release build, focused client/content tests, full solution tests, content-set closure, Windows/macOS CI.
2. Run the actual client at 1024×768 and 800×600. Both Enter and Exit should now be visible side-by-side in the same bottom row at the right side of the retail-derived footer, matching the supplied visual reference, on the empty-catalog login screen. Enter must be muted and inert; Exit should close the window cleanly after a completed click.
3. Verify Exit press cancellation, letterbox release, repeated clicks, no socket activity, and no new login authority. Verify that all resource ownership and startup splash/HUD behavior remain unchanged.
4. Do not declare **frame-exact** parity until `start.dat` placement matrices and real-driver screenshots are independently compared. This slice is genuine artwork restoration and safe host action, not wholesale Flash-timeline emulation.

## Baseline / exclusions

This slice is authored against `OpenConquerClient` merge `746ca2e93ad9a4533ee73a0ced3aeedc82fb5d7f` (PR #96). It does not replace `LoginCredentialLayout.cs` or `LoginCredentialPresentation.cs` and intentionally retains the precise bitmap-calibrated credential field coordinates established in STARTUP-001P. The six sprite frames are extracted from clean 5517 retail reference material, not downloaded dynamically, and are verified by SHA-256 on decoded RGBA bytes. **This module is not an authentication implementation.**
