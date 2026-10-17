# STARTUP-001Z: recovered opening / bird playback (PR #108, not merge-ready)

Evidence: retail `data/main/flash/loginFlash.swf` SHA-256 `8cc1d5a15d159f8b2c0d1c47109046324d34187f456710dfffbb8e0ca9fb9f6a` and `data/main/start.dat` SHA-256 `db4b4550bc4b3fa91b2fd7cc173bb9a6f8109dbc65221e7f0df290610655da20`. Confirmed in `20_notes/Architecture/11_login_ui_assets_and_interactions.md`. See `STARTUP-001Z-motion-provenance.json` for every decoded source frame and atlas rectangle.

- Intro: first 24 source video frames plus original sprite-25 frames 25–27, at 30 fps, stopping after frame 27. The last three sprite renders are clipped onto a 1024×768 RGBA stage preserving original `(-10,-336.85)` symbol origin; they are *authored-frame* reconstructions, not executed AVM1 recordings. Input is blocked during the opening. Textures are released upon completion. The original Flash movie async load delay, root-visible transfer and source masks remain a visual/runtime comparison gate.
- Bird: sprite 22 root depth 2, 602 frames at 30 fps. Packaged as one 1024×1024 atlas, with 448 nontransparent source frames and bounded draw rectangles. Position incorporates the native source registration `(-113.4,-168.8)` and root placement `(49,315.85)`. No artificial dragon motions are added; native named top/bottom/poetry holders are empty, and selection-dragon loading is absent.
- Dialog font: original dynamic Clear message now begins at (365,293) with Arial 14, following DefineEditText 284. Save warning remains part of the source-derived frame-6 bitmap; we have not replaced its authored text with an invented approximation. The exact Windows GDI device-font pixel capture is still an outstanding acceptance gate.

## Remaining acceptance before merging

Original region/server staged transitions (including disabled hit tests during animation, authenticated realm authority), load/error callback timelines, Caps Lock/tooltip presentation and a 1:1 Windows GDI font comparison remain incomplete. Authentication and legacy external publisher URLs remain disabled; no packet emission was added. Validate screen pixels and timing on macOS/Windows at 1024×768 and 800×600; CI does not establish retail visual parity. Authored SWF exports are not an executable replay.

## Performance and security

Offline-derived, hash-validated fixed-size resource decoding; one GPU bird atlas (not 602 textures); intro GPU allocations are bounded (unique hashed frames) and disposed when the 900-ms sequence completes. No runtime SWF parser, executable Flash, filesystem traversal, network requests or password persistence. The original media distribution license requires separate review before public shipping.
