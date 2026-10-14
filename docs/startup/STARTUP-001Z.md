# STARTUP-001Z — Login panel completion and credential viewport fidelity

## Immutable baseline

Main SHA `954cd921fffcb9147ab885ab5358bde273fffc15` after merged PR #106. The update is local-only and carries no login authorization, outbound URLs, server endpoint mutations, new data formats or published retail media. The patch deliberately does **not** claim to have reconstructed the original Flash authoring/timeline or additional unsupplied bitmaps.

## Native evidence and behavior

- The verified native 5517 `CLoginFlashDlg_HandleFlashCommandString` branches include `CMD_RECORDACCOUNT` (bounded history of 10), `CMD_SELECTACCOUNT`, `CMD_CLEARACCOUNT` and the host-seeded `SELECTBGMUSIC` value. Source: `20_notes/Architecture/03_login_flash_startup_command_contract.md` and the supplied `flashLogin.ini`. History selection resets the password, never logs its content, and does not trigger a login request.
- Four lightweight account-panel controls provide **SAVE, CLEAR, LIST, BGM ON/OFF**. Save is **explicit only** and stores the current account name, never the password. Clear removes the current saved name, clears both editable buffers and dismisses selection; list allows pointer selection or Up/Down/Enter/Escape in an expanded popup. The account list is ordered most-recent-first, deduplicated, and capped at ten. Disabled controls cannot mutate account history before a signed server is selected.
- Audio: music is user-toggleable without tearing down/recreating an OpenAL source or losing focus/lifecycle safety. Existing `Button_1` and `Button_2` cues are used for enabled control clicks and hover, respectively. The music default and source volume still derive from original `[bgMusic]` config. The BGM setting is persisted only when changed.
- Preferences are local user data (`~/Library/Application Support/OpenConquer/Client/login-preferences.json` on macOS; equivalent OS app-data directory on other systems). A closed JSON schema stores `schemaVersion`, `musicEnabled`, and `accounts`; strictly bounded to 4096 bytes and max ten validated 32-unit native-accepted ASCII account names. A malformed/unsupported document is not overwritten. Files are written to a unique, user-restricted temporary file, flushed and atomically renamed. No password is ever serialized. Storage is not a source of signed server or login authority. Files must not be distributed with game assets.
- The visual control boxes are **new native presentation chrome**, not images or exact SWF `PlaceObject` positions. The original `account-panel.rgba.br` remains the sole source for that retail panel; the popup overlaps underlying realm artwork only while open. Original Flash control artwork for save/clear/pop and register/help/charge, moving dragon, poetry and full timeline animation is unavailable in the checked-in content, so claiming pixel-exact completion would be inaccurate. Commercial rights to currently embedded 5517 media still require review.
- Caret/text/selection are clipped at the GPU to each credential field using a scoped OpenGL scissor on the logical render target. Mouse selection retains its capture across host letterbox margins; a stationary pointer beyond the field auto-advances by one code unit per rendered frame until the appropriate boundary. Scissor is always disabled on scope exit and at frame finalization. No new per-frame GPU resource or I/O is introduced. Pointer mapping remains framebuffer-to-logical and clamps drag X independently of Y.

## Security and lifecycle boundaries

- On saved-account selection, reset the password buffer and account cursor/selection; no automatic submission, login packet, credential encryption, account token, or network connection.
- Malformed, newer-schema and oversized preference files are not silently reset or overwritten. File system failures disable subsequent SAVE availability and log only a generic message (never the account name or other personal data). `File.Replace`-equivalent atomic rename is used for the small private file; cross-process optimistic write concurrency is intentionally not implemented. A hostile same-user process controlling the preferences directory remains a local threat outside this runtime's trust guarantees.
- Database, launcher preferences, content-set manifests, and signed catalog identity remain unchanged.

## Required verification before merging

1. On the exact immutable baseline and clean checkout, run the included guarded installer, locked restore, formatting check, Release build, targeted and full tests, content set verification, `git diff --check`, and Windows/macOS CI.
2. Run 1024x768 and 800x600. Confirm controls fit without covering credential text, background music toggles immediately and survives relaunch, and the originally embedded cursor still animates.
3. Enter an account and password; Save, open List, pick a different saved account: password clears. Test duplicates and overflow to ten, Clear removal, key navigation, cancel outside, and restart history. No automatic credential saving.
4. Hold pointer beyond either field edge and verify text scrolls while selecting across the letterbox and beyond field boundaries. Verify pixel clipping against the original 508x98 panel and no scissor leakage to other sprites.
5. Corrupt `login-preferences.json`, verify that its bytes remain untouched and client remains usable; verify no password or server endpoint enters the preferences JSON. Test without OpenAL and with focus loss.

## Deferred only because retail source evidence or authorization is missing

Exact original Flash button sprites/motion (including register/help/charge, bottom/side dragons, poetry), allowlisted contemporary URLs for external actions, mouse context-specific cursor ANI skins, and enabling Enter to connect. Finish these with authentic source media and explicit endpoint/URL policy, not invented first-party assets or unreviewed publisher-era HTTP URLs. This is a substantial page-completion slice, **not** a declaration of finished 5517 pixel/timeline parity.
