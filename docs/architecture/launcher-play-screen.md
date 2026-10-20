# Launcher play screen

The launcher window is the retail 5517 `play.exe` dialog. Its behavior and layout come from the 5517
binary (`CPlayDlg`, `CSkinButton`; see `20_notes/Architecture/01_play_launcher_startup_and_handoff.md`)
and its configuration files `ini/PlayGui.ini` and `ini/Play.dat`.

## Artwork

`src/OpenConquer.Launcher/Assets/Play` holds the `Play/*.bmp` art, converted offline by
`Evidence/nativeflashevidence/work/login-ui-research/bake_launcher_assets.py`. Each image is
straight-alpha RGBA, Brotli-compressed, and checked against its SHA-256 hash when loaded. The skin
color key `RGB(0, 255, 0)` is transparent. The transparent pixels of `Main.bmp` match the area
outside `Play/Main.rgn`. The cursor frames are those of `Normal.ani`, the same cursor the game uses.
`CPlayDlg_OnSetCursor` sets it over the whole dialog, buttons included.

## Layout

| Element | Position | Notes |
| --- | --- | --- |
| Window | 800x600 | `CenterWindow` centered a 900x650 dialog, so the art sits up and left of center |
| `MainS.bmp` | 594,0 | |
| `Version.bmp` | 35,29 | |
| Version text | 110,33 | red, 15px cell height, 6px average width; shows the version of the verified release |
| Logo | 385,-1 | disabled control |
| Web panel | 40,106, 546x405 | the launcher home page |
| High / Standard / Low | 580,392 / 585,443 / 580,520 | |
| Exit / Site / Register | 160,541 / 273,541 / 385,541 | |
| Hint popup | 160,200 | text at +20,+20, dark red, 20px lines; Continue 220,350, Cancel 380,350 |

Buttons take the size of their art. A button is hit only on its opaque pixels. It shows Over while
hovered and Down while a press stays on it, and it clicks when the release lands on it. Enter or
Space clicks the focused button. Escape closes the popup, then the window. Dragging the background
moves the window. On Windows the window region is the opaque area of the main art. On other
platforms, clicks pass through the transparent pixels.

Tooltips on the start buttons use the classic system look (`play.exe` has no common-controls v6
manifest), appear after 500 ms, and show the `Play.dat [Str]` texts.

## Start modes

| Button | Mode | Retail `GameModeRecord` |
| --- | --- | --- |
| High Definition | High | 1 |
| Standard Definition | Standard | 0 |
| Low Definition | Low | 2 |

The most-used mode shows its normal art, and the other two show their faded art. With counts H, S
and L, the target count is `max(H, S)` when that exceeds L, otherwise L. The first of Standard, High
and Low with that count is shown normally. A fresh installation shows Standard. Low asks for
confirmation until it has been used once.

The counters and the last mode are stored in `play.json`, next to `display.json`:

```json
{"schemaVersion":1,"highTimes":0,"standardTimes":1,"lowTimes":0,"lastMode":"standard"}
```

Nothing relies on this file for trust or correctness. A missing or damaged file reads as unused and
is replaced on the next start. A file with a newer schema, or one that is too large, is never
overwritten. A failed write never blocks a start.

The game client only distinguishes Low (`GameModeRecord == 2`), and it does not read the mode yet.

## Starting the game

A start button checks the installation again, records the mode, starts the release's client
executable with the display preferences (`--window-size`, `--window-mode`, `--presentation`), and
closes the launcher. `play.exe` also closed this way after starting AutoPatch. If the installation
is not usable or the client cannot start, the hint popup shows the reason, and Continue retries the
same mode. A start pressed while the launcher's first installation check is still running waits for
that check.

Only a managed installation can start the client. When the launcher project runs on its own (from
an IDE or `dotnet run`), it has no managed installation and reports "Installation incomplete". Run
the local managed product instead (`docs/development.md`, Local Managed Product).

## Web panel

`play.exe` showed `ini/Play.dat [Web] web1`, a publisher page, in a WebBrowser control. The launcher
shows `OpenConquerLauncherHomePage`, an HTTPS address set at build time, in the platform web view
(`Avalonia.Controls.WebView`). The address is a placeholder until a product page exists. Browsing
data is not persisted and developer tools are off. Pages on the home page's site stay in the panel.
Other HTTPS links open in the system browser, and any other scheme is blocked. WebView2 keeps its
working files under the launcher configuration directory.

The web view is a native OS view drawn above every other control in the window. It is hidden while
the hint popup is open, because the popup overlaps it, and shown again when the popup closes.

## Not reproduced

- Site, Register and the Exit sign-out page opened publisher URLs. They do nothing until OpenConquer
  pages exist.
- Click sounds name `Sound/Enter.wav` and similar files that the clean client does not ship.
  `PlaySoundA` then played the system default sound. The launcher stays silent instead.
- The logo animation (`Play/logo/logo0100%02d.bmp`) is never started, and its frames are not
  shipped.
- The system-menu About box.
- `SimSun` is used where installed. Other systems use their closest serif font.

The launcher's own context menu (right-click the background) opens the display settings and exits.
