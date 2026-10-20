# STARTUP-001T: Original 5517 selection artwork

Draws the selected region and server with the original bitmaps from the clean 5517 `data/main/start.dat`, for selections made in the signed realm directory. These replace the gold outlines that the client used to draw in code around selected slots. Keyboard-focus borders stay, as a separate accessibility state. Catalog and endpoint handling, the protocol, the cursor, audio, footer commands and account login are unchanged.

## Source assets

- Source: `data/main/start.dat` from `OpenConquer-5517-Login-Reference.zip`, a compressed SWF (CWS), version 6.
- SWF symbol 210 (DefineBitsJPEG3) is the 150x50 gold tile for the selected region. SHA-256 of the decoded straight-alpha RGBA: `a45876d402185d925ee78e0b91037078a45e6e35a7115511d650715fd256661d`.
- SWF symbol 38 (DefineBitsLossless2, format 5) is the 200x100 glow drawn around the selected server. Its ARGB channels are premultiplied, so they were converted to straight-alpha RGBA before Brotli packing. SHA-256 of the decoded RGBA: `380ae2e578206b0eff23cacfd87dd37b8294b3f7ed1212eb78cba9d8b92fc9a5`.
- The original pixels ship as size-checked embedded resources with fixed names, loaded through `LoginArtwork`. The client runs no Flash interpreter, searches no paths and loads no remote images. Nothing decodes GIF or JPEG at run time, and the render loop never decompresses Brotli data.

## Placement and drawing order

The 150x50 selected-region tile is centered in its 154x50 region slot on the reference stage. The server glow keeps its original 200x100 size and is centered over the 158x58 server slot. When the scaled image and slot differ by an odd number of pixels, centering rounds toward the upper left. At 800x600 this puts the oversized server glow at (320,153), and nothing is stretched.

Layers are drawn in this order: backdrop, header and footer, unselected region and server cells, the selected-region tile and server glow, the account panel and dragon, labels and page controls, then server icon textures and the action buttons. The glow sits behind the server icon so the icon art stays intact. Region and server labels keep separate colors chosen for contrast: dark sepia normally, and reddish for the selected region.

The positions were calibrated to reconstruct the retail artwork. They are not claimed to match the full motion curve of any particular 5517 Flash timeline. A screenshot of the retail 5631 client served only as a visual reference.

The account panel sits at reference Y=620. This is a separate alignment fix, checked by eye, that replaces the Y=637 used by earlier tests. The credential fields inside the panel keep the retail panel's pixel insets: account at (472,658,120,21) and password at (472,689,142,19) on the 1024x768 stage. Enter starts at Y=726, eight pixels below the panel. At 800x600 the gap is six pixels. Neither action button overlaps the credential fields. Further visual calibration is left for later work.

Textures are created once with the OpenGL context, released in reverse order on teardown, and cleaned up if construction fails partway. Fixed slot bounds and fixed image sizes keep drawing inside the 800x600 and 1024x768 stages. A selection can only come from the publisher-signed directory, so an empty catalog shows no highlight.

## Not implemented

The frame-by-frame animation of the original glow, the retail region font, the retail online-status dots, branding, footer links, the cursor, sound and 5517 authentication are not implemented. Each needs its own confirmed assets and behavior. Enter does not send credentials, because the 5517 handshake is not active yet.

## Testing

Run a locked restore, `dotnet format` verification, a Release build, `LoginNativeSelectionArtworkTests` and `LoginSelectionArtworkLayoutTests`, the existing catalog and login tests, the full solution tests, the content-set verification and Windows and macOS CI. With a real graphics driver, take screenshots at 1024x768 and 800x600 and check that:

- The selected region has the gold lit fill and a dark label.
- The selected server has the original glow behind its icon.
- Keyboard focus without a selection looks different from a selection.
- Switching to a different signed catalog clears the highlights.

To load the same signed realm directory as a terminal build, a local Rider build must pass `OpenConquerCatalogAuthorityFile` in its MSBuild settings.
