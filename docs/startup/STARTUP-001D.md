# STARTUP-001D — Independent login ornament layer

## Scope

Adds the verified top-dragon ornament above the pre-world login backdrop. The account panel and parchment remain unchanged. This is visual-only: no region catalog, server rows, credentials, registration, network traffic, or world entry.

## Verified placement

| Resource | Source | Layout reference | Dimensions |
| --- | --- | --- | --- |
| Top dragon | `data/main/start.dat`, `start/symbol-0397.png` | `ini/flashLogin.ini [topDragon] (267,26)` | 494×140 |

The separately verified `(150,381)` and `(685,373)` translations are **root-timeline anchors for `sr` and `ss` submovie clips**, not coordinates for individual group/server buttons. The earlier placement of `Group1/symbol-0007.png` at these anchors has been removed. Region and server visual grids require a separate verified layout and server catalog; no placeholder rows or invented endpoints are rendered.

The embedded image is an offline-extracted, losslessly converted RGBA image compressed with Brotli. It is first-party **derived** content, not a loose retail file. No SWF decoding or Flash runtime ships with the client, and the exact-retail content manifest remains unchanged.

## Lifetime and correctness

- Login texture ownership is scoped to the active OpenGL context; failed construction disposes any previously created textures in reverse order.
- No per-frame image decoding, texture creation, or string processing.
- Layout scales deterministically from a 1024×768 reference to the supported 4:3 logical resolutions.
- No HUD activation, authentication, or input-routing changes.

## Evidence

- `start/symbol-0397.png`: 494×140, source PNG SHA-256 `e3159ca601fddb006350bb9e6d2c4ad5604a98795669b3b96afc6bfc32c3df9c`.
- Extracted RGBA SHA-256: `b1e80e39a4a91d952510e826c9e8c0f0e0f83a353c6f546f322c970f3875d6b1`.
- Embedded Brotli bytes: 74769.
- Native `sr`/`ss` contract: `20_notes/Architecture/03_login_flash_startup_command_contract.md`, startup `loadMovie` target-anchor section.

## Verification

Run locked restore, formatting, Release build/tests, retail-content verification, and desktop 1024×768 / 800×600 smoke tests. Confirm no stray selection rectangles, correct alpha blending, clean OpenGL shutdown, and unchanged HUD gate. Do not claim full native login fidelity until the missing UI and authorized catalog are implemented.
