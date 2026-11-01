# ROLE-001A: Retail 5517 create-role movies on a Flash stage

The create-role screen is two retail Flash movies: `role.dat` and the class portraits it loads,
`characterInAndOut.swf`. Hand-placing each of their tweens would be neither faithful nor maintainable, so
this change plays them from their own timelines. It bakes both movies into embedded content and adds a
small Flash runtime that replays the timelines, with C# translations of their ActionScript. The screen
that hosts the stage, its native host gate and its sounds, and the wiring after `NEW_ROLE`, are the next
change.

## Sources

| Source (clean 5517 install) | SHA-256 |
|---|---|
| `data/main/role.dat` | `93b937535c74c6ae56bf7cd1352c0a223e99d6569790c40c0f106f8866d4074e` |
| `data/main/flash/characterInAndOut.swf` | `b38d88316f8a40bb3059ad8234d4d5a6663db8123c9cf6e043aee246ee74d9b8` |
| `ini/selectCharacter.ini` (read from the install at run time) | `45192235789d6c5ff43d963ce65b531b61d4b9f9423b669fa5e0496536cda92d` |

- Behavior: `20_notes/Architecture/12_create_role_flow.md`, sections "characterInAndOut.swf",
  "role.dat runtime contract" and "Retail runtime".
- Evidence and tools: `Evidence/nativeflashevidence/work/create-role-research/`. `build_timelines.py` turns
  the JPEXS XML into per-frame display lists, `swf_isolate.py` builds a SWF that shows one character per frame,
  and `bake_flash_movie.py` renders those frames with JPEXS and writes the assets below. The retail
  captures it was checked against are in `vm-runtime/`.

## Baked format

`src/OpenConquer.Content/Assets/Role/<movie>/` holds `<movie>.movie.json.br` and `r<n>.rgba.br`. Everything is
Brotli-compressed, and nothing decodes SWF or JPEG at run time.

- The movie JSON (format 1) holds every timeline's display list per frame (depth, character, name, matrix,
  color transform, clip depth, morph ratio and the frame that placed the object), the frame labels, the
  button records, the edit-text definitions and the raster table. `RoleMovies` checks its length and
  SHA-256, and `FlashMovieReader` rejects any unknown kind, dangling reference, out-of-range value, missing
  morph ratio or descending depth.
- Each shape, static text and morph ratio in use is one straight-alpha raster with its SHA-256. Every
  character is drawn alone by JPEXS, with its bounds minimum on a whole pixel, so pixel (0,0) is the
  character-space point (xmin, ymin). Morph rasters are cropped to the union of the start and end bounds.
  A bitmap-filled shape checked this way (characterInAndOut shape 101) reproduced its source bitmap to within one step per channel.
- Sizes: role.dat 200 rasters (4.5 MB), characterInAndOut 101 rasters (14.2 MB).

## Runtime

| Area | Location |
|---|---|
| Movie model, reader, role movie table | `OpenConquer.Content/Flash/` |
| `selectCharacter.ini` with CIni's rules | `OpenConquer.Content/Role/SelectCharacterIni.cs` |
| Stage, clips, buttons, text fields, hit testing | `OpenConquer.Client/UI/Flash/` |
| Drawing | `UI/Flash/FlashStagePresentation.cs`, `OpenGLRenderer.DrawTransformedSprite` and masks |
| ActionScript translations | `UI/Role/CharacterInAndOutScripts.cs`, `UI/Role/RoleScripts*.cs` |

The runtime follows Flash Player where the movies depend on it:

- A seek rebuilds the display list from the target frame. An object survives while its depth keeps the same
  placed character and placing frame, so `loadMovie` content and script state survive the parent's frames.
- After a script writes `_x`, `_y` or `_alpha`, the timeline stops moving that object. After `swapDepths`,
  the timeline stops removing it.
- Each frame runs `onEnterFrame`, then advances every playing clip that existed when the frame began, then
  the queued frame scripts. A parent's frame script runs before the first-frame scripts of the children it
  placed, as in AVM1.
- The pointer targets the topmost enabled button, or clip with handlers, inside every mask over it. A child
  named `hitArea` is the hit area. Its authored baked bounds remain interactive even when the
  hit-area raster is fully transparent; hit areas with painted pixels, visual shapes, and clipping masks
  still use pixel-alpha coverage.
  The body-selection silhouettes exercise this path. Press, release, release-outside, drag-out and
  drag-over follow Flash.
- Masks clip with nested coverage levels. A mask covers where its raster's alpha is at least one half.
- Input text fields take focus on a click (a click elsewhere clears it) and from `Selection.setFocus`.
- Edit text uses device fonts (Arial, and SimSun for `宋体`) with Flash's 2-pixel gutter.
- An untransformed bitmap draws on whole pixels, with a half pixel rounding down. This matches the login
  backdrop at start.dat y 41.5.

The scripts are translated statement for statement. That includes retail quirks: the other portraits settle at
about 72.7% alpha rather than 70%, every Enter toggles the panel flag, `HIDERETURNBTN` changes nothing, and
`tip.show` draws nothing because no tooltip is ever registered. SWF 6 ActionScript is case-insensitive, so
`headMC.gotoAndstop` and sound handles such as `rollOver_cancel` resolve as in retail.

## Verification

- GL conformance (`OpenConquer.Rendering.Conformance`): affine placement, the color transform and two nested
  masks, exact in the 16-bit target.
- Content tests: both movies load with the documented structure, every raster matches its size and
  digest, the reader rejects malformed movies, and `selectCharacter.ini` parses with CIni's rules.
- Client tests: timeline playback, seek identity, script-owned transforms, script order, pointer events,
  masks, `swapDepths`, `loadMovie`, the portrait carousel and selection, and full create-role flows. The
  flows cover each error panel, the native commands for a Monk (`CMD_NICK`, `CMD_BODYSIZE 1`,
  `CMD_VOCATION 10`, `CMD_ENTERGAME 1`, as retail sent), the 15-byte name limit, a host error, Back and
  the account-ID warning.
- Against the retail captures (Windows 11 VM, 2× DPI), the class stage at rest matches in layout and
  content. Mean absolute difference over the portrait strip is 6–10 per channel, from 16-bit color and the
  capture's scaling. The scroll opening and closing, the body stage, the confirm panel and the loading bar
  match the captures on visual comparison.

## Differences from retail

- Sub-pixel placement follows the measured Flash rules (identity bitmap fills `ceil(x)`, other rasters `ceil(x - 0.5)`),
  but the top chrome, Back and Create still land one pixel from retail; no single rule explains them yet.
- Vector shapes are rasterized at their authored size, so they soften when a timeline scales them up.
- Device text uses FreeType with GDI-style hinted advances and baseline, drawn with grayscale antialiasing rather than
  ClearType.
- Input fields have no range selection by mouse drag and no IME.

## Later changes

- Render parity pass (2026-10-31): `logo.swf` (a Sorenson video) baked and loaded into `logoMC`; edit-text variable binding (the
  body stage's class text); the name box baked as Flash-placed bitmap pieces; per-piece pixel snapping; GDI text
  metrics, whole-line clipping and no caret, as retail; the screens render into an RGBA8 target; script handlers no
  longer throw on clips their frame removed, and a disabled clip gets no rollOut. Evidence:
  `20_notes/Architecture/12_create_role_flow.md`, "Retail render parity".
