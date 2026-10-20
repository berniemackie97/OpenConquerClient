# STARTUP-001P: Retail account panel field alignment

## Source and purpose

The clean 5517 `data/main/start.dat` contains a 508x98 account panel image (`DefineBitsJPEG3` symbol 327). The embedded `account-panel.rgba.br` already holds that retail image, including the gold-framed Account and Password captions and the blue and dark input interiors. The earlier reconstruction drew extra captions and rectangular outlines over the bitmap. Its hit rectangles started at `(122,12)` and `(122,44)` relative to the panel, away from the painted fields. Those overlays made the screen look less like the original and put live text outside the input artwork.

STARTUP-001P moves the client's hit regions onto the two usable interiors of the source image and lets the original bitmap supply its own labels, backgrounds and borders. No image resources, colors, generated gradients, Flash code or networking are added.

| Input | Panel-local usable interior (x,y,w,h) | Logical 1024x768 | Logical 800x600 |
| --- | --- | --- | --- |
| Account | `(214,38,120,21)` | `(472,675,120,21)` | `(369,528,94,17)` |
| Password | `(214,69,142,19)` | `(472,706,142,19)` | `(369,552,111,15)` |

These rectangles were measured offline from the bitmap and serve as both the hit area and the text area of each field. They are not taken from the Flash click geometry. The account interior is shorter on purpose: it stops before the separate inset at the right edge of the top field in the original art. The account panel position `(258,637)` and the reference-stage scaling do not change.

## Rendering and input

- `LoginCredentialPresentation` now draws only the account text, a password mask made of `*` characters and a narrow focus caret, all kept inside the fields of the source image. It no longer draws duplicate captions or generated field outlines. The caret is clamped to the interior even for long or unsupported text.
- Text layouts are still rebuilt only when `LoginCredentialInput.Revision` changes, not every frame. Font resources are still created lazily and owned by the OpenGL context of `LoginPresentation`.
- The rest of credential handling is unchanged: the fields require a trusted server selection, input is limited to the restricted ASCII set, each buffer holds at most 32 characters, the password is zeroed and is cleared on focus loss, selection or catalog revocation clears the input, the letterbox area ignores clicks, and nothing is submitted.
- As in STARTUP-001K, the credential controls stay hidden while the catalog is empty or untrusted. The corrected fields therefore appear only once a trusted realm is selected. The default screenshot, taken with no publisher configured, does not change.
- The retail manifest, signed catalog, AccountServer transport, role creation, startup timing and world entry are unchanged.

## Testing

Run locked restore, format verification, the Release build, the credential tests, the full solution tests and retail content-set verification, then Windows/macOS CI. With a test catalog signed by the publisher, select a server and inspect 1024x768 and 800x600 by eye:

- text and mask stay inside the original blue field interiors
- the Account and Password labels appear once, from the retail image
- the focus caret stays within the field
- changing realm wipes both buffers, and focus loss wipes the password

Also confirm that the idle default startup screen is unchanged, check desktop resizing and letterboxing, and make sure no authentication socket is created.

## Follow-up

The next retail UI work should check the startup display-list transforms, imagery and the server icon mapping against the existing `20_notes` research and the `Server.dat`/`flashLogin.ini` data. Endpoints still come only from the signed catalog. The historical `Server.dat` must never quietly return as a trusted production source.
