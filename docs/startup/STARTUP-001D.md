# STARTUP-001D: Login chrome and account-panel alignment

## Scope

Draws the retail background, header, footer, account panel and top dragon as separate layers. This fixes the vertical misalignment of the first, single composited backdrop. No server data, UI interaction, Flash runtime, authentication or game-world access is added.

## Provenance

All artwork comes from sprites extracted offline from `data/main/start.dat`. The earlier composite BMP is replaced by the original 1024x637 background. The other layers are stored losslessly as Brotli-compressed, straight-alpha RGBA. This derived retail imagery is embedded in the content assembly; it is not listed as a retail payload file, because it is not one.

| Layer | Sprite | Original dimensions | Reference-stage placement |
| --- | --- | --- | --- |
| Background | `start/symbol-0010.png` | 1024x637 | `(0,40)` |
| Header | `start/symbol-0398.png` | 1024x116 | `(0,0)` |
| Footer | `start/symbol-0399.png` | 1024x140 | `(0,660)` (clips at viewport edge) |
| Account panel | `start/symbol-0327.png` | 508x98 | `(258,637)` |
| Top dragon | `start/symbol-0397.png` | 494x140 | `(267,26)` (confirmed topDragon anchor) |

The background, header, footer and account-panel positions are estimates reconstructed from a reference screen. They are not claimed to be the retail timeline placements. The account panel was lined up by eye against the supplied reference at 1024x768, and the same reference-stage placement scales to 800x600. The top dragon keeps its anchor from the retail configuration.

Source hashes (decoded RGBA SHA-256):

- Background: `e33bf0450c7a386aa28d6d90a1f71a8b7d8b64e1e4ec92fdfad48d7f3880a2b9`
- Header: `80fb919c05a73751f7418e26b0849f43a8a4b949dc9355f98b56aff04c68ce12`
- Footer: `25b59064c644823574410e2277d1fbc67bc26965224e79e97de38975f98a981c`
- Account panel: `3a1e4698639f2c4af2eace1c9b48e81d4898d279831c128bbe65e76e3eb240c2`
- Top dragon: `b1e80e39a4a91d952510e826c9e8c0f0e0f83a353c6f546f322c970f3875d6b1`

## Runtime

- `LoginArtworkSet` is the validated set of startup artwork. Nothing is decoded or read from disk in the render loop.
- `LoginVisualLayout` applies reference-stage coordinates to the supported 4:3 logical resolutions.
- `LoginPresentation` creates and releases five separately owned OpenGL textures within one context lifetime. If construction fails partway, it disposes the textures already created in reverse order.
- The HUD is not created and login state does not change. There are no protocol messages, no placeholder regions or servers, and no user-input routing.
- `ClientContentClosure` and the retail manifest are unchanged.

## Testing

Check the header and footer placement, the account panel overlapping the footer, transparency and compositing, and that the dragon has not moved. Check both 1024x768 and 800x600. Run Release restore, build, tests and format check, the retail-content closure check, and an OpenGL smoke test on real macOS and Windows machines.
