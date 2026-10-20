# STARTUP-001X: Original 5517 cursor and login sounds

Adds the retail 5517 animated cursor and the login screen's music and sound effects. The selected server and region artwork, the account panel at Y=620, signed realm trust, the policy that keeps 1060 login disabled, the pointer-capture protections and the 1024x768 and 800x600 display mapping are unchanged. No networking is added, and Enter stays disabled.

## Sources

All media come from `OpenConquer-5517-Startup-Source.zip`, supplied by the operator and collected from a known clean 5517 retail client. The archive includes `data/Cursor/Normal.ani`, `ini/Cursor.ini`, `ini/flashLogin.ini`, `ini/GameSetUp.ini`, `data/main/start.dat`, and twelve MP3s under `sound/`. The ZIP's `SHA256SUMS` was checked when the assets were prepared. `SOURCE-VERIFICATION.json` lists the SHA-256 of each original MP3 and of its decoded PCM, along with dimensions, sample formats and audio lengths.

`Normal.ani` is a Windows RIFF `ACON` animated cursor, unrelated to Conquer's sprite-definition `.ani` files. It has five 32x32 8-bit DIB/CUR frames, an animation rate of 10 jiffies at 60 Hz, and hotspots at (0,0). The RIFF header declares eight bytes more than the file actually holds, while the size of the `LIST/fram` chunk matches its five records. The offline converter reads both the indexed XOR pixels and the 1-bit AND transparency masks. A general-purpose `.cur` decoder that ignores the AND mask produces an opaque black rectangle, which is wrong. The converted frames are stored as Brotli-compressed RGBA resources, and tests check their hashes. The retail `Cursor.ini` maps other context-specific cursors, but only `Normal.ani` was supplied. The NPC, trade and combat cursors are not reconstructed.

`flashLogin.ini` maps events to sounds and sets the background music and effect volumes. The twelve supplied MP3s were decoded offline with FFmpeg to interleaved signed PCM16 at their original sample rate and channel count, then Brotli-compressed into embedded resources. The client does not need or call FFmpeg at startup or at run time. Music is 44100 Hz stereo. Effects are 22050 Hz, mono or stereo depending on the source file. The manifest lists the SHA hashes of both the source files and the decoded PCM. The startup content loader checks each decoded length and rejects trailing bytes. Catalog metadata can never choose paths or audio files.

## Runtime behavior

`DesktopCursorAnimation` keeps its own copies of the frame data (each frame must be the expected size), a size-limited frame count and frame duration, and the original hotspot. `DesktopWindow` sets the RGBA custom cursor through Silk.NET's `ICursor` on the desktop event thread. The animation follows monotonic Stopwatch time rather than the variable render delta. Before switching to the next GLFW custom-cursor frame, the window sets the cursor type back to Standard, which frees the previous native cursor handle. The cursor reverts when the window loses focus and when it is disposed. Because the OS draws the cursor, it appears at physical screen size with the correct hotspot, whatever the render viewport and whether the stage is presented at 800x600 or 1024x768.

`LoginAudioPlayer` runs on the event thread of the window that owns it. It owns one OpenAL Soft device and context, one immutable buffer per packaged sound cue, a looping music source and six reusable effect voices. Source volume is 0.4 for music and 1.0 for effects, as in `flashLogin.ini`. Playback only switches between preloaded buffers. Pointer and render events never decode audio, allocate new buffers or read files. Losing focus pauses the music and stops any effects that are playing. If there is no output device or the OpenAL loader is missing, the client logs it and carries on silently. Login and rendering are not affected. Disposing releases all buffers and voices before the context and device.

| Retail `flashLogin.ini` mapping | Event implemented |
| --- | --- |
| `[bgMusic] music.mp3`, `loopTime=0`, `volume=40` | Music starts with the login window and loops |
| `rollOver_region` and `rollOver_server = Glide.mp3` | First hover over each occupied region or server slot in the signed directory |
| `click_server = click_2.mp3` | Committed server selection |
| `dragonMov` and `serverSelection.show = Dragon.mp3` | Visual change for a new region selection |
| `click_leftPage`, `click_rightPage = page.mp3` | Committed region or server page change |
| `rollOver_leftPage`, `rollOver_rightPage`, account/password, Exit = `Button_2.mp3` | Hover over an enabled hit target |
| `click_exitBtn = Button_1.mp3` | Completed Exit click (the immediate shutdown may cut off the end of the sound) |
| `keyboardDown_0..2 = key_1..3.mp3` | Accepted credential characters only, cycling through the three sounds in a fixed order |

`click_region` is empty in the original ini, so selecting a region plays no click sound. Enter is disabled: it plays no `Confirm.mp3`, starts no network session and is drawn as disabled. The register, help and charge links and the dialog components are not wired up, and no guessed URLs are assigned to them. `Confirm.mp3` and `error.mp3` are packaged and tested, but nothing plays them until the matching retail screen transitions exist.

## Libraries and licensing

Playback uses the Silk.NET libraries that the client already depends on: `Silk.NET.OpenAL` 2.23.0 and the dynamically loaded `Silk.NET.OpenAL.Soft.Native` 1.23.1 package. Both API loaders select the packaged OpenAL Soft implementation, and the device is opened with the default-device sentinel. OpenAL Soft is LGPL-2.0-or-later and must stay dynamically replaceable. Any distribution must include the applicable OpenAL Soft license and notices. Do not link the native library statically or leave out its notices. No external decoders and no shell or player processes are used at run time.

To update the lock files after a package change, run `dotnet restore OpenConquer.Client.slnx --force-evaluate -p:RestoreLockedMode=false`. Review the changes to the regenerated lock files before committing them. CI then uses locked restores.

## Not implemented yet

Each of these needs its own source research: the moving Flash timeline and display-list animations, the footer register, help and charge controls with a safety policy for their URLs, the source-specific logo and poetry, the other cursors in `Cursor.ini`, and the original music on/off preference (`SELECTBGMUSIC`). The supplied `GameSetUp.ini` does not contain that preference. The cursor and audio are presentation only and are no reason to enable account login.

## Verification

1. Check `SOURCE-VERIFICATION.json` and run all resource integrity tests. At least one decoded cursor frame must contain transparency rather than a black box.
2. Update the NuGet lock files as described above, reviewing the changes. Then run the formatting check, a Release build, the targeted and full tests, the retail content-set closure check, macOS and Windows CI, and CodeQL.
3. With the signed local realm, launch at 1024x768 and 800x600. The original five-frame dagger cursor animates without growing resource use or scaling artifacts. Moving focus out and back in, and shutting down, restore the system cursor. With no audio device there are no warnings or crashes.
4. Listen for the music loop, region and server rollover, server selection, page changes, Exit, and key clicks for accepted characters. Hovering empty cells or the disabled Enter button is silent. A rejected character or a stale pointer capture plays no selection sound.
5. Confirm that credentials, the signed catalog and the login protocol are unchanged. Do not report the real-audio or GPU/OS cursor checks as passed unless they were run with real drivers.
