# STARTUP-001T — Original 5517 selection artwork

## Verified baseline and bounded intent

Applies after PR #99 merge `6bbe0e6b29945138109eb51626b8ea5b6104f165`. Restore original clean-5517 `data/main/start.dat` bitmap assets for an authenticated selected region and server, replacing procedural gold selected-slot outlines. Keyboard-focus borders are retained as a separate accessibility input state; no catalog/endpoint, protocol, cursor, audio, footer command, or account-login behavior changes.

## Binary provenance

- Asset source: `OpenConquer-5517-Login-Reference.zip`, `data/main/start.dat`, SWF CWS version 6.
- SWF symbol 210: DefineBitsJPEG3, 150×50 pixel selected-region gold tile; decoded straight-alpha RGBA SHA-256 `a45876d402185d925ee78e0b91037078a45e6e35a7115511d650715fd256661d`.
- SWF symbol 38: DefineBitsLossless2 format 5, 200×100 server-selection luminous surround; ARGB channels are premultiplied and were converted to straight-alpha RGBA before Brotli packing. Decoded RGBA SHA-256 `380ae2e578206b0eff23cacfd87dd37b8294b3f7ed1212eb78cba9d8b92fc9a5`.
- Packaged original pixels are loaded through bounded, fixed-name `LoginArtwork` embedded resources. No runtime Flash interpreter, lookup path, remote image, GIF, JPEG, or runtime Brotli transcoding on the render loop.

## Placement and compositing

Selected region tile is centered inside the existing region slot (150×50 over 154×50 at reference stage), server-selection glow is centered over a 158×58 server slot at original 200×100 dimensions. Layer order: backdrop, header/footer, unselected region/server cells, selected region and server glow, account panel and dragon, labels and page controls, then server icon textures and the action buttons. The selected glow is behind the actual icon to preserve the icon art; region and server label text retains distinct contrast-appropriate color styles (dark sepia normal, reddish selected region). The position is calibrated as a native-artwork reconstruction, not asserted to match a specific 5517 Flash timeline's complete motion curve. Native 5631 screenshot is visual reference only.

Textures are allocated once at context creation, included in reverse teardown, and cleaned on partial-construction failures. Constant-time slot bounds and exact image sizes prevent drawing past 800×600 or 1024×768 stages. Selection can only come from the publisher-signed directory; empty catalogs do not produce highlights.

## Remaining work

This slice does not implement original glow frame-by-frame animation, region font face/parity, retail online-state dots, branding, footer links, cursor, sound, or native authentication. Each requires separate verified assets and behavior. Do not enable Enter to send credentials while the native handshake remains dormant. Local Rider must pass `OpenConquerCatalogAuthorityFile` in MSBuild settings to load the same signed realm directory as the terminal build.

## Gates

Locked restore, dotnet format verify, Release build, `LoginNativeSelectionArtworkTests` and `LoginSelectionArtworkLayoutTests`, existing catalog/login tests, entire solution tests, content-set verification, Windows/macOS CI, real-driver screenshot 1024×768 and 800×600. Verify selected region has a gold illuminated fill and dark label, selected server has the original luminous surround behind its icon, focus-only navigation is distinct, and changing signed catalogs invalidates highlights.
