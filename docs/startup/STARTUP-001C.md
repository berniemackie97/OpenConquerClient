# STARTUP-001C: Pre-world login backdrop

## Pre-world state

After the retail startup logo plays once, the client enters a pre-world state. Until the server has authorized a session, the client does not load HUD content, create HUD GPU resources, forward HUD input or allow world entry. No world-entry API, packet or account-creation protocol is guessed at here.

## Backdrop image

`OpenConquer.Content/Assets/Login/login-backdrop.bmp` is a first-party image composited offline. It is not a file from the retail filesystem, so it must not be added to `content/retail-5517/manifest.json` under a made-up retail path. It is embedded in the signed, published `OpenConquer.Content` assembly. The retail content closure is unchanged.

The source artwork was extracted offline from the Conquer Online 5517 `data/main/start.dat`. The original PNG symbols and their SHA-256 hashes:

| Symbol | Size | SHA-256 | Placement |
|---|---|---|---|
| `start/symbol-0010.png` | 1024x637 | `88beb17800373e5e1baca522636ca0e99dce26e7b95a7b5fe8f2d9b6b45270f1` | `(0,0)` |
| `start/symbol-0399.png` | 1024x140 | `dfa56ff44b1147c69f8870d6fce40a59b3780b8c6014ef30a914682ea91237aa` | `(0,628)` alpha-composited |
| `start/symbol-0327.png` | 508x98 | `d643ff4d64036e96c70b982d0e9b8249c91110901285cdfea92bf57ba076e723` | `(258,657)` alpha-composited |

Output: 1024x768 uncompressed RGB24 BMP, SHA-256 `f1c8845a6cb01c604d19419920e43f20ff6ba05fb79a2ba1eec6f207241fd1cd`.

The composition coordinates are a first-party reconstruction. They are not claimed to be the Flash timeline coordinates. The source artwork itself is unchanged. On the desktop, the 4:3 composed image is scaled to the configured logical viewport, 800x600 or 1024x768. The image is only the background and the account-panel backdrop; it is not a working login UI.

## Runtime ownership

* `ClientApplication` owns main-window setup, the GL context lifecycle and the active pre-world presentation.
* `LoginPresentation` owns the single backdrop GL texture, which it creates and disposes within the current context.
* `WorldHudRuntime` holds the existing HUD construction, state, pointer handling and drawing, and will live for the length of a future authenticated world session. It is not created during pre-world startup.
* No keyboard, pointer or world input reaches gameplay before authentication.
* Registration and login controls need real services and confirmed server-selection behavior, so they are not mocked.

## Verification

Run the usual locked restore, format check, Release build, tests, retail content verification and publish, then launch the client on macOS and Windows. Check that the desktop window shows the login backdrop instead of the HUD at both retail logical sizes (set `ini/GameSetUp.ini` in a development content root to switch). Check shutdown during startup and OpenGL context cleanup. The normal renderer conformance suite must still pass.
