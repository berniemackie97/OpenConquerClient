# STARTUP-001Z — Retail 5517 login page (in progress, not merge-ready)

## Evidence

Clean retail SWF `data/main/start.dat` SHA-256 `db4b4550bc4b3fa91b2fd7cc173bb9a6f8109dbc65221e7f0df290610655da20`, `logo.swf` SHA-256 `7165b9ed4fc84dfd54dea8ee750ab537de522a7b19c645e2313e03c9a251c6f7`. See `20_notes/Architecture/11_login_ui_assets_and_interactions.md` (2026-10-15 closure), and the extracted/verified `OpenConquer-5517-Login-UI-Assets.zip` (SHA-256 `ff442dd0003ea9e2bbb12f45e5269d3553d9b0872ab5348a48cfd68274e5cfb0`). This note supplements previous STARTUP-001Z viewport changes on PR #108, not a declaration of completeness.

## Restored in this change

- Removed realm-selection dependence from account/password editing; initial keyboard focus remains none. Tab advances Account→Password, password Tab does not cycle back, and Enter no longer changes focus or attempts network login. Region/server navigation does not invalidate editor state. Signed catalog replacement still zeroes the password in the modern client (explicit security deviation), preserving the account name.
- The retail edit symbol 364 has `maxLength=15`, preserved as a 15-unit password bound. Account-name edits zero the previous password. The 10–14 character language-dependent native login validator is not implemented, because the client still cannot submit login requests.
- Account-panel bitmap placement follows start.dat shape 328 origin `(256,605.9)` rounded to `(256,606)`; Enter/Exit use SWF child symbols 317/324 stage positions `(649.95,704.4)` / `(858.95,703.9)` rounded to `(650,704)` / `(859,704)` and their authentic `132×40` frame dimensions. These replace earlier screenshot-derived 5631 positions.
- Authored SWF `DefineButton2` symbols 299,305,310,335,342,349 are rendered in their original *up* state, at stage placements recorded in `metadata/key-placements.json`. No guessed SVG/icons, placeholders or hidden hit-test art. Register/Purchase/Help remain inert because original endpoints are unsafe and no contemporary allowlisted URLs have been authorized. Account dropdown/Save/Clear command handling, dialogs and their original runtime transitions are **not yet connected**; users must not infer functionality from image presence.
- The original `logo.swf` 25-frame held-picture sequence is embedded as verified, offline-decoded RGBA, displayed at `(776,2,248,78)`, and loops at the documented 30 fps. Runtime playback has no decoder or file I/O, and GPU textures have deterministic OpenGL-context ownership.
- All 43 embedded resources are exact PNG-derived RGBA with a fixed length and decoded SHA-256 validation. `ASSET-PROVENANCE.json` maps each source symbol/file to its input SHA.

## Remaining within STARTUP-001Z before merge

1. Restore the genuine account history popup (sprite 360, attached `itemBtn` sprite 6), 20-ms list motion, bounded named-account persistence, and confirmed Save/Clear dialogs (symbols 283/285) with only explicit Save→OK recording. Do not implement automatic saving.
2. Reconstruct exact region/server view compositions/transitions from SWF timelines and verify frame-13 selected state, 4/3 grid geometry and existing authenticated catalog icon mapping. External URL actions remain disabled until a secure allowlist is specified.
3. Restore genuine initial opening, 602-frame flying-bird parent, 10-frame error/dialog animations and callback-driven loading only after source-backed runtime behavior is modeled. Top/bottom named dragon and poetry clips are empty; `rsDragon` is never loaded in retail 5517. Do not animate these empty holders.
4. Execute locked restore, formatting, Release build, all tests, original content-set validation and Mac/Windows CI. Visually compare original screenshots at 1024×768 and 800×600; verify scissor, keyboard capture, catalog churn, and audio. Source-derived Flash device-font pixel parity remains an explicit Windows-GDI verification gate.

Enter remains deliberately disabled and no account-server packets or unsafe publisher URLs are sent. Original retail media require a distribution-rights review before shipping externally.

## Account controls and original confirmation graphics (continuation)

- Original `start.dat` DefineButton2 states 335 / 342 / 349 replace the previously inert account icons with pointer-captured actions. There is no invented music toggle, modern button skin, automatic account recording, or unsafe external navigation.
- `CMD_RECORDACCOUNT` is represented by explicit Save → original 10-frame confirmation → OK only. `CMD_CLEARACCOUNT` is represented by original Clear → confirmation → OK, and only when the current account occurs in saved history. Cancel does not mutate local storage. Popup uses native sprite 6 two-frame skin at x≈452 and original `10+2*N` pixel steps every 20 ms for up to ten rows; selection sets the account and clears the password when the identity changes.
- Confirmation frames are exact source-derived composed RGBA snapshots for frames 2–9 of `saveMC` sprite 283 and `clearMC` sprite 285; frame 6 holds for click, frame 7 begins closing, frame 10 hides. Native buttons 272 and 282 are drawn with their actual Up/Over/Down art. Clear's original dynamically assembled text is ` "<account>" ` in field 284. Native device-text rasterization (Arial 14) remains host-dependent and is not claimed pixel-identical on macOS.
- History is stored privately as account names only (UTF-8, schema-validated JSON, ten entries, 4 KiB maximum). Values are not tokens or login authority. Unsupported/corrupt histories are preserved untouched; failed writes disable further persistence for the session. New writes use private temporary files, durable flush, and an atomic same-directory replacement. No password or selected endpoint is persisted.
- Credential entry remains independent from authenticated catalog selection. Only network submission, which is still intentionally disabled, can eventually use an explicitly validated signed realm. No 5517 server endpoint is trusted from historical accounts.
- Content resources carry exact 5517 PNG-derived decoded-RGBA hashes, validated on load. GPU textures are created once per OpenGL context and disposed deterministically. No disk or JSON I/O is performed while drawing frames.

The retail movie's remaining opening, 602-frame flying-bird parent, region/server transition effects, and callback-driven loading must still be audited against live 5517 playback before declaring the entire startup page pixel/timeline complete. This continuation does not declare those unimplemented elements finished. All builds, unit tests, Windows/macOS CI and image comparisons are required before merging #108.
