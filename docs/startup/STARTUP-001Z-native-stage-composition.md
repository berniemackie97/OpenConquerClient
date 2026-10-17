# STARTUP-001Z — Native login stage composition

Replaces the screenshot-calibrated login chrome and grid estimates with the retail `start.dat` display list.
Evidence: `20_notes/Architecture/11_login_ui_assets_and_interactions.md`, section "Stage geometry, runtime
(2026-10-16)", checked against the retail Windows capture `vm-runtime/01-login-first-launch-mode2.png`.
Every baked layer and server movie is listed with its source and decoded SHA-256 in
`STARTUP-001Z-stage-provenance.json`.

## Root display list

| Depth | Retail element | Implementation |
|---:|---|---|
| 1 | shape 11 (bitmap 10) | backdrop at stage y 41 (was 40) |
| 2 | sprite 22 bird | unchanged |
| 4 | sprite 180 `ss` at (685, 373) | twelve holders at `aPosData`; `son` movie drawn at each holder origin, unscaled |
| 138 | sprite 259 `sr` [0.980, 0, 0, 0.980, 150, 381] | panel sprite 183 (alpha 195/256), sixteen holders, paging |
| 216 | shape 400 | single full-stage border layer (replaces the separate header, footer and top-dragon images) |
| 221 | `enterMC` | account panel, credential fields 363/364, Enter/Exit, account controls |
| 274 / 276 | logo / `verTxt` | logo unchanged; version label added |

## Region panel

- Holder slots run down the first column, then the second (`aBtn` order), as the retail capture shows.
- Holder y is the settled value of sprite 259's frame spring (`aPosData`); x keeps the timeline value.
- A region without a picture loads `defaultR.swf`: its text-free tile (shape 3, bitmap 2 at (0, −1)), its
  rollover frames 2–8 / 10–12 (sprite 9) and its selected frame 13 (flame sprite 61 at (3, 1), with the
  five-frame fade-in). The rolled-over holder draws above its neighbours (`swapDepths(999)`). Slots past the
  catalog load `unopenedR.swf` and have no hit area.
- Region captions stay a modern substitute for the text baked into each region movie; the signed catalog
  supplies the name.
- The leftBtn/rightBtn page buttons (192/199, rotated) and the "page/count" text appear only with more than
  one page. Turning a page reloads the holders and keeps the selected region and its servers.

## Server panel

- `son` art is the movie's root shape: `unopenedS`, `defaultS`, or the server movie's shape 3 (170×90 frame
  with the picture clipped in at (6, 3)); Group9/server1 is its 164×85 picture. The earlier raw JPEG3 picture
  assets were premultiplied and aspect-fitted into a guessed cell; both are corrected.
- The name is edit text 35: Arial 14, black, centred in the field at (30.5, 64.8), cut at the field box
  instead of ellipsized.
- Sprite 180 has no paging: only the first twelve realms of a region are shown.

## Input and sound

- Regions and realms select on **press** (`hitMC.onPress`), not release. A press on the selected region does
  nothing; a press on any enabled realm plays `click_server`; a new region plays `dragonMov`;
  `click_region` is empty. Page buttons play `click_*Page` on press and turn on release.
- Rollover sounds follow the same rules: none for the selected region, `Glide` for enabled realms,
  `Button_2` for page buttons.
- The keyboard region/server navigation and its focus rectangles are removed: the login movie has no
  keyboard path into either list (the native bridge polls only Esc, Tab and Enter, and Tab belongs to the
  credential fields).

## Text

- Credential fields use the native edit-text boxes, Arial 14, `#908B37`, with the caret in the text color.
- `verTxt` reads `version.dat` (now part of the content closure) the way `setVer` does: "version: 5517".
- The text stack can request a family's bold face (Arial Bold for `verTxt` and `pageTxt`).

## Not covered here

Staged server-slot fade on region change and the automatic first-realm selection after it, live realm
status lights, `CTxtTip` tooltips, Caps Lock overlay, loading/error/quit dialogs, and Windows GDI device-font
pixel parity remain open.
