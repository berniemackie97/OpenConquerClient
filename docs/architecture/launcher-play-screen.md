# Launcher play screen

The launcher window is the retail 5517 `play.exe` dialog. Behaviour and layout come from the 5517
binary (`CPlayDlg`, `CSkinButton`; see `20_notes/Architecture/01_play_launcher_startup_and_handoff.md`)
and its configuration files `ini/PlayGui.ini` and `ini/Play.dat`.

## Artwork

`src/OpenConquer.Launcher/Assets/Play` holds the `Play/*.bmp` art, converted offline by
`Evidence/nativeflashevidence/work/login-ui-research/bake_launcher_assets.py`. Each image is
straight-alpha RGBA, Brotli-compressed, and checked against its SHA-256 when loaded. The skin colour
key `RGB(0, 255, 0)` is transparent. `Main.bmp`'s transparent pixels are exactly the area outside
`Play/Main.rgn`. The cursor frames are those of `Normal.ani`, identical to the game's own.

## Layout

| Element | Position | Notes |
| --- | --- | --- |
| Window | 800x600 | `CenterWindow` centred a 900x650 dialog, so the art sits up and left of centre |
| `MainS.bmp` | 594,0 | |
| `Version.bmp` | 35,29 | |
| Version text | 110,33 | red, 15px cell height, 6px average width; the verified release version |
| Logo | 385,-1 | disabled control |
| Web panel | 40,106, 546x405 | the launcher home page |
| High / Standard / Low | 580,392 / 585,443 / 580,520 | |
| Exit / Site / Register | 160,541 / 273,541 / 385,541 | |
| Hint popup | 160,200 | text at +20,+20, dark red, 20px lines; Continue 220,350, Cancel 380,350 |

Buttons take the size of their art. A button is hit only on its opaque pixels. It shows Over while
hovered, Down while a press stays on it, and clicks when the release lands on it. Enter or Space
clicks the focused button; Escape closes the popup, then the window. Dragging the background moves
the window. On Windows the window region is the opaque area of the main art. On other platforms,
clicks pass through the transparent pixels.

Tooltips on the start buttons use the classic system look (`play.exe` has no common-controls v6
manifest), appear after 500 ms, and show the `Play.dat [Str]` texts.

## Start modes

| Button | Mode | Retail `GameModeRecord` |
| --- | --- | --- |
| High Definition | High | 1 |
| Standard Definition | Standard | 0 |
| Low Definition | Low | 2 |

The most-used mode shows its normal art and the other two their faded art. With counts H, S and L, the
target count is `max(H, S)` when that exceeds L, otherwise L; the first of Standard, High and Low with
that count is shown normally. A fresh installation shows Standard. Low asks for confirmation until it
has been used once.

The counters and last mode live in `play.json`, beside `display.json`:

```json
{"schemaVersion":1,"highTimes":0,"standardTimes":1,"lowTimes":0,"lastMode":"standard"}
```

The file carries no authority. A missing or damaged file reads as unused and is replaced on the next
start. A newer schema or an oversized file is never overwritten. A failed write never blocks a start.

The game client distinguishes only Low (`GameModeRecord == 2`); it does not read the mode yet.

## Starting the game

A start button verifies the installation again, records the mode, starts the release's client
executable with the display preferences (`--window-size`, `--window-mode`, `--presentation`), and
closes the launcher. `play.exe` closed the same way after starting AutoPatch. If the installation is
not usable or the client cannot start, the hint popup shows the reason. Continue retries the same mode.

## Web panel

`play.exe` showed `ini/Play.dat [Web] web1`, a publisher page, in a WebBrowser control. The launcher
shows `OpenConquerLauncherHomePage`, an HTTPS address set at build time (a placeholder until a product
page exists), in the platform web view (`Avalonia.Controls.WebView`). Browsing data is not persisted
and developer tools are off. Pages on the home page's site stay in the panel. Other HTTPS links open in
the system browser, and any other scheme is blocked. WebView2 keeps its working files under the launcher
configuration directory.

## Not reproduced

- Site, Register and the Exit sign-out page opened publisher URLs; they do nothing until OpenConquer
  pages exist.
- Click sounds name `Sound/Enter.wav` and similar files that the clean client does not ship.
  `PlaySoundA` then played the system default sound; the launcher stays silent.
- The logo animation (`Play/logo/logo0100%02d.bmp`) is never started and its frames are not shipped.
- The system-menu About box.
- `SimSun` is used where installed; other systems use their closest serif.

The launcher's own context menu (right-click the background) opens the display settings and exits.
