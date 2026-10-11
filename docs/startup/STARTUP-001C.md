# STARTUP-001C — pre-world login visual foundation

## Authority

The client enters a pre-world state following the one-shot native startup logo. It must not load HUD content, create HUD GPU resources, forward HUD input, or permit world entry before a server-authorized session exists. This slice introduces no unverified world-entry API, packet, or account-creation protocol.

## Presentation artifact

`OpenConquer.Content/Assets/Login/login-backdrop.bmp` is an offline-composited first-party resource. It is **not** a file from the original retail filesystem and must not be added to `content/retail-5517/manifest.json` under a fabricated retail path. It is embedded in the signed/published `OpenConquer.Content` assembly. The existing exact-retail closure remains unchanged.

Source: the verified Conquer Online 5517 visual evidence extracted offline from `data/main/start.dat`. Original PNG symbols and SHA-256:

| Symbol | Size | SHA-256 | Placement |
|---|---|---|---|
| `start/symbol-0010.png` | 1024×637 | `88beb17800373e5e1baca522636ca0e99dce26e7b95a7b5fe8f2d9b6b45270f1` | `(0,0)` |
| `start/symbol-0399.png` | 1024×140 | `dfa56ff44b1147c69f8870d6fce40a59b3780b8c6014ef30a914682ea91237aa` | `(0,628)` alpha-composited |
| `start/symbol-0327.png` | 508×98 | `d643ff4d64036e96c70b982d0e9b8249c91110901285cdfea92bf57ba076e723` | `(258,657)` alpha-composited |

Output: 1024×768, uncompressed RGB24 BMP. SHA-256: `f1c8845a6cb01c604d19419920e43f20ff6ba05fb79a2ba1eec6f207241fd1cd`.

Composition coordinates are **first-party reconstructed placements**, not purported exact Flash timeline coordinates. Source artwork identity is preserved; desktop presentation scales the 4:3 composed image to the configured 800×600 or 1024×768 logical viewport. It is intentionally just the background and account-panel backdrop, not a completed functional login UI.

## Runtime ownership

* `ClientApplication` owns main-window setup, GL context lifecycle, and the active pre-world presentation.
* `LoginPresentation` owns the single backdrop GL texture, constructed and disposed within the current context.
* `WorldHudRuntime` retains the previous HUD construction, state, pointer handling, and drawing behavior under a dedicated future authenticated-world lifetime. It is **not instantiated** during pre-world startup.
* No keyboard, pointer, or world input is dispatched to gameplay before authentication.
* Registration and login controls require actual services and verified server-selection semantics; they are not mocked in this slice.

## Verification

Run the established locked restore, format, Release build, test, retail content verification, publish, and actual macOS/Windows client launch. Verify the desktop window renders the login backdrop instead of the HUD at both retail logical sizes (configure `ini/GameSetUp.ini` appropriately in a development content root). Confirm startup shutdown and OpenGL context cleanup; keep the normal renderer conformance suite green.
