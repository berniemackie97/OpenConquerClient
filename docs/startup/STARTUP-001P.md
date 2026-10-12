# STARTUP-001P — Retail account-panel field alignment

## Evidence and purpose

The original clean-5517 `data/main/start.dat` contains a 508×98 account-panel image (`DefineBitsJPEG3` symbol 327). The existing embedded `account-panel.rgba.br` already preserves that retail image, including the gold-framed **Account** and **Password** captions and the blue/dark input interiors. The earlier first-party reconstruction added extra captions and rectangular outlines over the bitmap, and its hit rectangles began at `(122,12)` and `(122,44)` relative to the panel rather than over the painted fields. Those overlays reduced visual parity and placed live text outside the input artwork.

This slice aligns native-managed hit regions to the source image's two usable interiors and lets the original bitmap provide its own labels, backgrounds, and borders. It adds no new image resources, colors, synthesized gradients, Flash executable, or native networking.

| Input | Panel-local usable interior (x,y,w,h) | Logical 1024×768 | Logical 800×600 |
| --- | --- | --- | --- |
| Account | `(214,38,120,21)` | `(472,675,120,21)` | `(369,528,94,17)` |
| Password | `(214,69,142,19)` | `(472,706,142,19)` | `(369,552,111,15)` |

These are **offline bitmap-calibrated first-party input hit/text interiors**, not claimed as native Flash click geometry. The shorter account interior intentionally avoids the separate native right-edge inset of the top field. The established account panel location `(258,637)` and reference-stage scaling are unchanged.

## Rendering and security

- `LoginCredentialPresentation` now draws **only** bounded account text, a `*`-only password mask, and a narrow, clamped focus caret inside the source-image field. It no longer creates duplicate text captions or procedural field outlines. Caret placement is clamped to the interior on long or unsupported text.
- Text layouts continue to rebuild only when `LoginCredentialInput.Revision` changes, not every frame. Font resources remain lazy and owned by `LoginPresentation`'s OpenGL context.
- Existing authority gates, conservative ASCII validation, maximum 32-character buffers, password zeroing, focus-loss secret clearing, selection/catalog revocation, letterbox exclusion, and no-submit behavior are unchanged.
- Credential controls remain hidden with an empty/untrusted catalog, as established by STARTUP-001K. Thus this visual correction appears when a trusted realm is selected, **not** in the default authority-unconfigured screenshot.
- No retail manifest, signed catalog, AccountServer transport, role-creation, startup timing, or world-entry behavior changes.

## Verification

Run locked restore, format verification, Release build, focused credential tests, full solution tests, and retail content-set verification, then Windows/macOS CI. With an explicitly publisher-signed test catalog, select a server and visually inspect 1024×768 and 800×600: text and mask stay inside the original blue field interiors, Account/Password labels appear exactly once from the retail image, focus caret stays within the field, changing realm wipes both buffers, and focus loss wipes the password. Confirm idle default startup is unchanged. Check desktop-resize/letterbox behavior and ensure no authentication socket is created.

Next retail UI work should audit startup display-list transforms, imagery and server selection icon mapping against the existing `20_notes` and `Server.dat`/`flashLogin.ini` evidence. Signed endpoints remain authoritative; historical `Server.dat` is never silently reintroduced as a production trust source.
