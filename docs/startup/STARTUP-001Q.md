# STARTUP-001Q: Retail 5517 Enter and Exit buttons

## Purpose

Builds on the STARTUP-001P credential field alignment. The original clean 5517 login movie has Enter and Exit buttons, but none of the earlier signed-catalog, paging, directory-status or credential-alignment work drew them. Those features are unchanged. This change adds the retail Enter and Exit artwork to the login screen and makes Exit close the window. Enter is drawn but stays disabled: credential entry is not connected to the account transport yet, so pressing `Enter` sends nothing. The type-1060 framing utility is also unchanged and still unused.

## Sources

- Artwork: the clean 5517 `data/main/start.dat` (CWS SWF version 6) in `OpenConquer-5517-Login-Reference.zip`.
- Behavior: `20_notes/Architecture/03_login_flash_startup_command_contract.md` maps `CMD_ENTERGAME` and `CMD_GAMESTART` to `0x480` and `CMD_EXIT` to `0x55`. `05_startup_data_files_and_native_replacement.md` covers the loginFlash presentation and `flashLogin.ini`. In the 5517 client, the exit command closes the client through a host action and does not go through the account server transport.
- Offline extraction: decompress the CWS root, read the JPEG RGB payload of each `DefineBitsJPEG3` (SWF tag 35), apply its decompressed per-pixel alpha mask, convert to straight-alpha RGBA and Brotli-compress each frame. The shipped client contains no SWF interpreter or ActionScript, decodes no JPEG at runtime, and loads no remote images or asset paths chosen at runtime.

| Original SWF bitmap character | State | Source size | SHA-256 decoded RGBA |
| --- | --- | --- | --- |
| 311 | Enter up | 132x40 | `41fdea3a98e653cb2c991ce9b656d41ac97d3e828ce32e1f5362174a51d3e404` |
| 313 | Enter hover | 132x40 | `8ca3dffe0f2f1850ea61872e137003e54ec8c6a16d68606157ad6cbd85946b83` |
| 315 | Enter pressed | 132x40 | `5056f3f7098a2d935af7faf550cd3cff1ce5a4957832935c5fc2cdca5db212a8` |
| 318 | Exit up | 132x40 | `57121511c04031b7cde18efc93a23728adc19bae695280f4d6a9678de6283730` |
| 320 | Exit hover | 132x40 | `fe32a09db879dda1b73e8c5012ca8fab5aa45b9fa845ccf4a03508660d89d794` |
| 322 | Exit pressed | 132x40 | `37442d367079c3f6b1ad3e6c9e8fb7eabc615f10b10491ad631e4b9518a3bd1a` |

Tests decompress the embedded resources and check each frame against these SHA-256 values. The pixels come from the retail files.

## Placement

An earlier placement stacked Enter at `(768,642)` above Exit at `(768,690)`, which was wrong. The buttons now sit in one bottom row on the 1024x768 reference stage: Enter at `(654,726,124,34)` and Exit at `(862,726,124,34)`. This matches the visible button positions in the supplied screenshot of the original client. That screenshot shows version 5631, so these coordinates are a visual calibration for the 5517 reconstruction. They are not the 5517 Flash `PlaceObject` matrices, which have not been verified, and neither position should be read as one. The 132x40 source textures are unchanged and are drawn at the calibrated size. The region and server grids, header, footer, dragon and account panel stay where they were.

Enter overlaps only the fully transparent bottom-right edge of the account panel's sprite rectangle. It covers no pixels of the source art and neither credential interior. Input layout validation therefore rejects overlap with the credential fields themselves instead of any overlap with the image's transparent bounds. A client regression test checks that the overlapped bitmap pixels have zero alpha. At both 800x600 and 1024x768, every placement comes from the same fixed scaling and stays clear of the account fields. The source art is not added to the retail content-set manifest under made-up retail paths.

## Interaction

- Enter is always drawn in a muted, disabled state. A click inside it is consumed and does nothing else: it never connects, formats credentials, grants world entry or queues a speculative command. The Enter hover and pressed frames are packaged now for later, once login is implemented and verified. They are not used yet, so the button never suggests that login works.
- Exit uses the original bitmap frames for each state. A left-button press and release inside the Exit bounds asks the desktop window to close normally. Pointer capture is cancelled when the window loses focus or when the button is released outside the logical viewport. Releasing a stale press does nothing. Clicks in the letterbox area do nothing. No global keyboard shortcut is taken over.
- `DesktopWindow.RequestClose()` calls the normal Silk.NET `IWindow.Close()` on the existing desktop event thread. It does not call `Environment.Exit` or dispose the window inside a callback, so the normal OpenGL shutdown still runs. Application teardown still clears the credential buffers and cancels the catalog refresh.
- All image textures are created once when the OpenGL context initializes and are disposed before the graphics device. If creation fails partway, the textures already created are released in reverse order. Drawing does not decode assets, build fonts or allocate GPU textures.
- There is no registration action. The 5517 `CMD_GETURL` command needs its own design, with an allowlist of HTTPS destinations, instead of launching arbitrary URLs from the original publisher's era.

## Follow-up: server directory and icons

In the clean 5517 data the server list file is `Server.dat`, not `GameServer.dat`. According to `20_notes/Architecture/05_startup_data_files_and_native_replacement.md`, its decoded `outenserver` hierarchy has `Child`, `FlashName`, `FlashIcon`, `FlashHint`, `ServerName`, `ServerIP` and `ServerPort`. This client already takes its endpoints from a signed directory. The next directory and icon work should keep that signature check and add a small, closed set of icon identifiers that map to images converted ahead of time from the retail files and shipped in the content assembly. The catalog must not supply file paths, remote URLs, executable Flash or unverified fallback endpoints. Asset IDs, signature freshness and display metadata each need their own tests before live realm and icon selection is enabled.

## Testing

1. Locked `dotnet restore`, format verification, Release build, the client and content tests, full solution tests, content-set closure and Windows/macOS CI.
2. Run the client at 1024x768 and 800x600 on the empty-catalog login screen. Enter and Exit should appear side by side in the same bottom row, on the right side of the retail footer, matching the supplied reference screenshot. Enter should look muted and do nothing. Exit should close the window cleanly after a complete click.
3. Check Exit press cancellation, release over the letterbox and repeated clicks, and confirm that there is no socket activity and no new login path. Check that resource ownership and the startup splash and HUD behave as before.
4. Frame-exact parity cannot be claimed until the `start.dat` placement matrices have been compared with real-driver screenshots. This change restores the artwork and adds a safe close action. It does not emulate the Flash timeline.

## Credential fields

`LoginCredentialLayout.cs` and `LoginCredentialPresentation.cs` are not replaced, and the bitmap-calibrated credential field coordinates from STARTUP-001P are kept. The six sprite frames come from the clean 5517 retail reference material. They are embedded rather than downloaded, and tests check them by SHA-256 of the decoded RGBA bytes. This module does not implement authentication.
