# STARTUP-001X — Original 5517 cursor skin and audible login presentation

## Audited baseline and replacement of superseded proposal

Based on merged PR #103, main `95d71d7a1a851e94b3a073295bd04ba30e3be115`. Replaces the earlier cursor-only STARTUP-001X proposal, which never implemented the game-specific cursor graphic or startup audio. Do not apply that earlier ZIP.

This is a bounded native startup-presentation slice. It retains the original selected-server/region artwork, account panel Y=620, signed realm trust, 1060 login-disabled policy, pointer-capture protections, and 1024×768/800×600 display mapping. It does not resume networking or enable Enter.

## Source and provenance

All media derive from the operator-supplied `OpenConquer-5517-Startup-Source.zip`, collected from the known clean 5517 retail client. The archive includes `data/Cursor/Normal.ani`, `ini/Cursor.ini`, `ini/flashLogin.ini`, `ini/GameSetUp.ini`, `data/main/start.dat`, and twelve MP3s beneath `sound/`. The ZIP's `SHA256SUMS` was checked during preparation. See `SOURCE-VERIFICATION.json` for both original MP3 and decoded PCM SHA-256 digests, dimensions, sample formats and audio lengths.

`Normal.ani` is Windows RIFF `ACON`, **not** a Conquer sprite-definition `.ani`: five 32×32 8-bit DIB/CUR frames with 10-jiffy/60 Hz animation rate and (0,0) hotspots. The original RIFF declares eight bytes beyond the actual file length while the contained `LIST/fram` size exactly matches its five records. Offline conversion reads indexed XOR pixels **and** 1-bit AND transparency masks; general-purpose `.cur` image decoding that discards AND masks produces an opaque black rectangle and is incorrect. Converted frames are Brotli-compressed RGBA resources and hash-checked in tests. Existing native `Cursor.ini` maps other context-sensitive cursor variants, but only `Normal.ani` was supplied; this slice does not claim to reconstruct unsupplied NPC, trade, or combat cursors.

`flashLogin.ini` provides explicit sound mappings and the background music/sound volumes. The twelve supplied MP3s were decoded offline with FFmpeg into original-rate, original-channel interleaved signed PCM16 bytes, then Brotli-compressed into embedded resources. Decoding is **offline**; FFmpeg is neither required nor invoked at client startup or runtime. Music: 44100 Hz stereo; effects: 22050 Hz mono/stereo per source. Original source SHA hashes and decoded PCM SHA hashes are both included in the manifest. The startup content loader enforces fixed decoded lengths and no excess bytes, and catalog metadata can never select paths or audio files.

## Runtime ownership and native mapping

`DesktopCursorAnimation` takes private copies of exact-size frame data, a bounded frame count/duration and the genuine hotspot. `DesktopWindow` installs the native RGBA custom cursor through Silk.NET's `ICursor` on the desktop event thread; animation is driven by monotonic Stopwatch time, not variable render delta. Before swapping a GLFW custom-cursor frame, the implementation changes cursor type back to Standard, which frees the prior native cursor handle. Cursor state reverts on focus loss and disposal. The OS cursor operates at physical screen size and keeps correct hotspot, regardless of render viewport or 800×600/1024×768 presentation scale.

`LoginAudioPlayer` owns one OpenAL Soft device/context, one immutable buffer per curated cue, a looping music source and six reusable effect voices on the owning window event thread. The source-level volume is 0.4 for BGM and 1.0 for effects, matching `flashLogin.ini`. Playback switches preloaded buffers; it never decodes, allocates new buffers or reads files on pointer/render events. Losing focus pauses music and stops in-flight effects. Output-device or OpenAL loader absence causes a **silent, logged fallback**, not an account/renderer failure. Dispose releases all buffers and voices before the context and device.

| Native `flashLogin.ini` mapping | Event implemented |
| --- | --- |
| `[bgMusic] music.mp3`, `loopTime=0`, `volume=40` | Music starts with the login window and loops |
| `rollOver_region` and `rollOver_server = Glide.mp3` | First hover over each occupied authenticated region/server slot |
| `click_server = click_2.mp3` | Committed server selection |
| `dragonMov` and `serverSelection.show = Dragon.mp3` | New region selection visual change |
| `click_leftPage`, `click_rightPage = page.mp3` | Committed region/server page navigation |
| `rollOver_leftPage`, `rollOver_rightPage`, account/password, Exit = Button_2.mp3` | Enabled hit-target hover |
| `click_exitBtn = Button_1.mp3` | Completed Exit gesture (immediate shutdown may truncate its tail) |
| `keyboardDown_0..2 = key_1..3.mp3` | Accepted credential characters only, deterministic bounded rotation |

`click_region` is empty in the original ini; this implementation does **not** invent a click-region sound. Disabled Enter emits no `Confirm.mp3`, starts no network session, and remains visually disabled. Remaining first-party register/help/charge links and dialog components are not wired or assigned speculative URLs. `Confirm.mp3` and `error.mp3` are curated and testable but inactive until their native transitions exist.

## Libraries / licensing

Runtime playback uses already-managed Silk.NET architecture: `Silk.NET.OpenAL` 2.23.0 and the dynamically loaded `Silk.NET.OpenAL.Soft.Native` 1.23.1 package. Both API loaders explicitly select the packaged OpenAL Soft implementation, and the device is opened using the default-device sentinel. The latter is LGPL-2.0-or-later and must remain dynamically replaceable; distribution needs the applicable OpenAL Soft license and notices. Do not statically embed the native library or omit its notices. No external decoder dependencies or shell/player processes are used at runtime. Restore lock-file changes with `dotnet restore OpenConquer.Client.slnx --force-evaluate -p:RestoreLockedMode=false`, then commit regenerated lock files **only after** inspecting their changes; thereafter CI must use locked restores.

## Remaining startup parity (do not pretend complete)

The current stage still needs a separately evidence-backed slice for moving native Flash timeline/display-list animations, footer register/help/charge controls and their URL safety policy, the source-specific logo/poetry, the remaining multi-cursor skins in `Cursor.ini`, and the original music-toggle preference (`SELECTBGMUSIC`). The supplied `GameSetUp.ini` does not contain that preference. Source-only cursor and audio do not justify enabling account login.

## Verification

1. Verify `SOURCE-VERIFICATION.json` and all resource integrity tests. At least one decoded cursor frame must have transparency, not a black box.
2. Update the NuGet lock closure deliberately, then run formatting, Release build, targeted and full tests, retail content-set closure, macOS/Windows CI and CodeQL.
3. With the existing signed local realm, launch at 1024×768 and 800×600: five-frame original dagger cursor animates without resource growth or scaling artifacts; focus-out/in and shutdown restore the system cursor; no warnings/crashes on an absent audio device.
4. Listen for music looping, region/server rollover, server selection, page changes, Exit and bounded accepted keyboard clicks. Hovering empty cells/disabled Enter is silent. A rejected character or stale pointer capture must emit no selection sound.
5. Confirm credentials, signed catalog and login protocol unchanged. Never claim a pass for the real-audio or GPU/OS cursor gates without driver-backed verification.
