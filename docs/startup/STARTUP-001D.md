# STARTUP-001D — Login chrome and account-panel alignment

## Scope

Separates the original retail-derived background, header, footer, account panel and top dragon into independent draw layers. This corrects the vertical misalignment in PR #85's initial composited backdrop without introducing any server data, UI interaction, Flash runtime, authentication, or game-world access.

## Provenance

All artwork comes from offline-extracted `data/main/start.dat` sprites. The original composite BMP has been replaced with the original 1024×637 background. All other additional artwork is losslessly stored as Brotli-compressed, straight-alpha RGBA. Original retail imagery is derived and embedded in the content assembly, **not** represented as a fabricated exact-retail payload entry.

| Layer | Sprite | Original dimensions | Reference-stage placement |
| --- | --- | --- | --- |
| Background | `start/symbol-0010.png` | 1024×637 | `(0,40)` |
| Header | `start/symbol-0398.png` | 1024×116 | `(0,0)` |
| Footer | `start/symbol-0399.png` | 1024×140 | `(0,660)` (clips at viewport edge) |
| Account panel | `start/symbol-0327.png` | 508×98 | `(258,615)` |
| Top dragon | `start/symbol-0397.png` | 494×140 | `(267,26)` (verified topDragon anchor) |

Background, header, footer, and account-panel positions are **reference-screen reconstruction estimates**, not claims of exact native timeline placement. They reflect the supplied reference screenshot and must be checked at 1024×768 and 800×600. Top-dragon placement retains its native configuration anchor.

Source hashes (decoded RGBA SHA-256):

- Background: `e33bf0450c7a386aa28d6d90a1f71a8b7d8b64e1e4ec92fdfad48d7f3880a2b9`
- Header: `80fb919c05a73751f7418e26b0849f43a8a4b949dc9355f98b56aff04c68ce12`
- Footer: `25b59064c644823574410e2277d1fbc67bc26965224e79e97de38975f98a981c`
- Account panel: `3a1e4698639f2c4af2eace1c9b48e81d4898d279831c128bbe65e76e3eb240c2`
- Top dragon: `b1e80e39a4a91d952510e826c9e8c0f0e0f83a353c6f546f322c970f3875d6b1`

## Runtime contract

- `LoginArtworkSet` is the validated startup visual asset set; no render-loop decoding or dynamic reads.
- `LoginVisualLayout` applies reference-stage coordinates to supported 4:3 logical resolutions.
- `LoginPresentation` creates and releases five independently owned OpenGL textures in one context lifetime, disposing partial constructions in reverse order.
- No HUD instantiation, login state mutation, protocol messages, placeholder regions or servers, or user-input routing.
- Original `ClientContentClosure` and retail manifest remain unchanged.

## Acceptance

Confirm header and footer placement, account-panel overlap with footer, correct transparency/compositing, and unchanged dragon position. Verify both 1024×768 and 800×600, Release restore/build/tests/format, retail-content closure, and native macOS/Windows OpenGL smoke test before merging.
