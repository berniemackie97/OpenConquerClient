# STARTUP-001Z — Original region and server effects

## Evidence and scope

Based on the clean retail 5517 `data/main/start.dat`, `data/main/flash/Group1.swf`, `data/main/flash/defaultR.swf`, and the existing Codex source-derived `OpenConquer-5517-Login-UI-Assets.zip` (`20_notes/Architecture/11_login_ui_assets_and_interactions.md`). The exact image/frame provenance, source PNG hashes and derived RGBA atlas hashes are in `STARTUP-001Z-selection-provenance.json`. No artwork was synthesized and no executable SWF parser runs in production.

Region selection plays the source 25-frame `Group1` sprite 60 fire loop at the verified 30 fps. Its source frames are pixel-identical in Group1, Group2, Group3 and the default region movie. Region rollover uses the outer artwork from root frames 2–8, then 10–12 on exit. Because those exports contain a hard-coded *MYTHS* label, only the untouched original outer border pixels are composited; the movie's baked text center is transparent so the signed catalog's dynamic label remains authoritative. This border-only composition is a documented fidelity limitation, **not** a claim that the complete region hover movie has been reproduced.

Server rollover is the original `start.dat` sprite 83 (34 frames) placed at the sprite 179 native offset (`-22.65`, `-9`). The selected server uses sprite 178 (47 frames) placed at the sprite 179 frame-12 native offset (`-26`, `-29`). The first full selected loop runs frames 1–47; subsequent loops start at the source `aa` label at frame 23. Drawing the effects after icons and before catalog labels preserves real artwork layering and readable, authenticated server names.

All three native effects and region-rollout border states are packed offline into four hash-checked RGBA/Brotli atlases. Width ≤ 1760, height ≤ 720. Atlases are loaded once per GL context, not on render frames. No per-tick allocations, dynamic remote asset IDs or mutable global caches. Resources follow existing one-context disposal and rollback rules. The fixed 30-fps state machine is advanced by bounded wall-clock frame time. Authenticated catalog/region/server identity changes reset their animation counters; menu overlays suppress hover targeting; window focus loss clears pointer hover. The original signed directory and disabled/unimplemented login transport remain unchanged.

## Remaining STARTUP-001Z release gates

- Native 5517 server-slot staggered fade-out / external icon load / fade-in (source `Strong.easeIn`, `(row+column+5)/(row+column+4)` frames). Input gating must apply **only to affected server cells**, not the credential fields. These must be modeled and tested before claiming the region transition complete.
- Region hover currently uses source-derived **border-only** frames because the retail movie bakes the old Chinese/English region title into the original interior. Completing the full root movie requires composition from source symbols without rendered hard-coded text and a pixel comparison on real Windows; never patch over text via generated replacement pixels.
- Source-defined 5517 loading and error sequences, caps-lock alpha overlay, device font alignment/wrapping, 1:1 Windows GDI/Flash comparison, and final 800x600/1024x768 runtime screenshots.
- The separate `rsDragon.swf` remains intentionally unused; verified retail never loads it. Top/bottom/poetry named holders are empty. Do not invent animations for those movie clips.

Do **not** merge PR #108 solely because automated tests pass. Original media distribution rights must be reviewed before shipping a public client.
