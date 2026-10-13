# STARTUP-001X — Desktop cursor semantics for the authenticated 5517 login screen

## Audit and rationale

Baseline: merged STARTUP-001W / PR #103, `95d71d7a1a851e94b3a073295bd04ba30e3be115`. The client already presents the curated native 5517 startup artwork, action sprites, signed realm directory and bounded credential fields, but the desktop pointer never reflects those controls. This work returns to the unfinished startup UI; it is not another networking or server-protocol slice.

Native evidence: `20_notes/Architecture/02_conquer_client_startup_chain.md` and `20_notes/Architecture/05_startup_data_files_and_native_replacement.md` identify the Flash-hosted login presentation, its control commands and its asset provenance. The ancestor material in the supplied `Archive.zip` documents the verified Windows cursor manager (`Conquer.exe` 0x595671, 0x595A14 / 0x5B27DE), the default `Data/Cursor/Normal.ani` and system cursor fallbacks. It does **not** contain `Normal.ani`. The curated client content set does not contain that retail cursor image either. Accordingly this slice introduces native **host operating-system** arrow, text-I-beam and hand affordances; do not call the result exact 5517 cursor-artwork parity. No cursor images are fabricated or introduced from the network.

## Behavior

- `LoginPointerCursor` is a deterministic, allocation-free hit-test policy over the *existing* validated login layout. It does not change click handlers or synthesize a selection. Only occupied, signed catalog region/server slots receive the hand pointer; empty slots retain the arrow. A server slot becomes interactive only after selecting its region.
- The hand appears for enabled pagination controls only; disabled/invisible directions stay an arrow. Exit uses the hand; Enter is deliberately disabled and stays an arrow. Credential fields use the text cursor only when an authenticated server is selected, never for an empty/revoked selection.
- Pointer coordinates follow the existing framebuffer-to-logical viewport mapping. Letterbox and off-stage points always restore the arrow. After focus loss the arrow is reset immediately; subsequent frames continue to keep it neutral until focus returns.
- `DesktopWindow.SetPointerCursor` owns platform cursor changes on the existing window/input thread. It selects Silk.NET `StandardCursor` shapes and uses an arrow when the backend does not support the requested shape. It never uses Win32 platform handles directly, loads untrusted cursor files, or creates OpenGL textures. It avoids backend updates when the effective cursor has not changed.
- The client reconciles pointer shape on render ticks so catalog expiry/renewal, keyboard pagination, selection changes, focus and window resize cannot leave a stale hand/I-beam under a stationary mouse. This work is constant-bounded by the 16+12 login cells, with no disk access, allocations or GPU uploads per frame.

## Out of scope / remaining retail startup work

The retail `Data/Cursor/Normal.ani` artwork, additional native cursor state table `ini/Cursor.ini`, Flash UI audio and native `sound/music.mp3`, `sound/Glide.mp3`, `sound/click_2.mp3`, `sound/page.mp3`, `sound/Button_1.mp3` etc. are not present in the available tracked content. `20_notes/Architecture/03_login_flash_startup_command_contract.md` establishes that startup sound is Flash-owned and receives `SELECTBGMUSIC`; `20_notes/Architecture/05_startup_data_files_and_native_replacement.md` documents the native media paths/loop semantics. Those remain work to complete **next**, after sourcing and verifying original media bytes and adding a cross-platform playback boundary. The client must not silently substitute a fake MP3, procedural click sound, an arbitrary external download, or native `NDsound.dll` on macOS.

This change does not alter account-panel Y=620, original selection glow/labels, signed authority, catalog refresh, credential lifetime, login-network activation or Enter disablement. No new third-party dependency or packages.lock change.

## Quality gates

Verify locked restore, format verify, Release build, focused cursor tests, full solution tests, retail content-set closure, macOS and Windows CI. Driver acceptance: 1024×768 and 800×600, both windowed/fullscreen; occupied/empty cells, server before and after choosing a region, no credentials before realm selection, Exit/disabled Enter, pagination edges, no signed catalog, mouse over letterbox bars, focus changes, refresh/revocation while stationary, retina framebuffer scaling and cursor teardown. The source-level suite does not replace the real-driver test.
