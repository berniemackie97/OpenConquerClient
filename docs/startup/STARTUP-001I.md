# STARTUP-001I — Authenticated realm display labels

## Scope and authority

This slice paints visible region and server labels from the immutable `LoginServerCatalog` view accepted by STARTUP-001G. It never reads retail `Server.dat`, constructs endpoint addresses, or grants authentication. Label text is presentation data; protocol server names and account ingress endpoints remain separate, independently validated identities. An unconfigured, unavailable, expired, or rejected signed catalog still yields no realm labels.

STARTUP-001H's Unicode-scalar text path now has a display-only client consumer. Existing native encoded-text layout, input capture, paging, cell artwork, selection IDs, and the approved visual layout are unchanged.

## Rendering contract

- The OpenGL context owns the label font, glyph atlas, and page-layout cache through `LoginPresentation`. It is disposed before its parent device. The font is constructed lazily: developer builds with an empty authoritative catalog do not require host font discovery just to display empty chrome.
- At most the 16 visible region and 12 visible server labels are prepared on a catalog/page/selected-region transition. Layouts are reused on unchanged frames. Cached page data is cleared when the authoritative view becomes empty, and old layouts are never drawn after a replacement.
- Font height and cell padding derive from the logical 1024-wide reference stage. Glyph **ink** bounds, including negative bearings, must fit wholly inside the configured cell at both 1024×768 and 800×600; no GL scissor or runtime asset changes are introduced.
- Names that do not fit are truncated only at Unicode text-element boundaries and marked with `...`. Text elements requiring multiple scalars (combining or joined glyph sequences) are marked unavailable because the scalar-based renderer cannot shape them. Full candidate coverage is checked first for the remaining names; if the selected host font cannot rasterize every scalar in the complete name, the presentation shows `?` instead of silently dropping glyphs. If even that marker cannot be rendered, the cell remains unlabeled. These markers are visual failure indicators, **not** aliases, server identities, or authorization choices.
- The existing Unicode layout remains scalar-based. This slice does **not** claim HarfBuzz/complex-script shaping, bidirectional reordering, or deterministic glyph appearance across platform fonts. Publication of internationalized catalog names requiring these capabilities remains subject to a separately audited shaping/font-coverage policy and manual conformance verification; scalar acceptance alone does not certify visual fidelity.
- `LoginCatalogText` now validates display-name surrogate pairs as Unicode scalars so valid supplementary-plane display names can flow from signed JSON into the layout path. It continues rejecting malformed UTF-16, controls and formatting code points. The native `ProtocolServerName` field retains its established ban on UTF-16 surrogate code units and therefore is **not** broadened by this display feature.

## Ownership, failure and follow-up gates

One active OpenGL context owns text resources; the render thread owns layout creation and updates. Context release disposes glyph resources with the other login textures. No background thread can mutate the label cache or selection. Missing-font or rendering failures are not reinterpreted as a valid catalog, a route, or a selectable network endpoint.

Acceptance requires unit tests for Unicode catalog-name validation, grapheme-safe truncation, both logical-size ink containment, glyph coverage, old native-layout regressions, full solution Release tests, locked restore, formatting, Windows/macOS checks, and the retail-5517 content-set integrity check. A real driver-backed screenshot at both sizes, including short/long names, a page transition, empty/expired catalog, and unsupported glyphs, is required before representing the full visual behavior as production-ready.

Rendering a supplementary scalar is not equivalent to guaranteeing the selected host font contains that glyph or that full Unicode scripts are correctly shaped. A later internationalization slice must address licensed font coverage and shaping before such publisher content is enabled. Login credentials, registration, native AccountServer transport, packet crypto, and game handoff are explicitly out of scope.
