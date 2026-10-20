# STARTUP-001I: Realm labels from the signed catalog

## Scope

The login screen draws the visible region and server labels from the immutable `LoginServerCatalog` view that STARTUP-001G accepts. Labels never come from retail `Server.dat`, and drawing them never builds an endpoint address or grants authentication. Label text is display data only. Protocol server names and account ingress endpoints remain separate identities, each validated on its own. When the signed catalog is unconfigured, unavailable, expired or rejected, no realm labels are drawn.

This is the first client consumer of the Unicode-scalar text path from STARTUP-001H, and it uses that path for display only. The existing 5517 encoded-text layout, input capture, paging, cell artwork, selection IDs and the approved visual layout do not change.

## Rendering

- `LoginPresentation` owns the label font, glyph atlas and page-layout cache for the OpenGL context and disposes them before the parent device. The font is created lazily, so a developer build with an empty catalog does not need host font discovery just to draw the empty frame.
- Layouts are prepared only when the catalog, page or selected region changes, and only for the visible labels: at most 16 region labels and 12 server labels. Unchanged frames reuse the existing layouts. Cached page data is cleared when the catalog view becomes empty, and layouts from a replaced catalog are never drawn.
- Font height and cell padding are derived from the 1024-wide logical reference stage. The ink bounds of every glyph, including negative bearings, must fit inside the cell at both 1024x768 and 800x600. This needs no GL scissor and no runtime asset changes.
- A name that does not fit is cut at a Unicode text-element boundary and ends with `...`. A text element made of several scalars (a combining or joined glyph sequence) is shown as unavailable, because the renderer works on single scalars and cannot shape it. For the remaining names, the renderer first checks that the host font can rasterize every scalar in the full name. If it cannot, the cell shows `?` instead of silently dropping glyphs. If the font cannot draw that marker either, the cell stays blank. These markers only signal a display failure. They are not aliases or server identities, and they do not affect what the player can select or log in to.
- The layout stays scalar-based. There is no HarfBuzz or complex-script shaping and no bidirectional reordering, and the same name can look different with different platform fonts. Publishing internationalized catalog names that need those features depends on a separately reviewed shaping and font-coverage policy and on manual conformance testing. Accepting a name's scalars does not mean it renders correctly.
- `LoginCatalogText` now accepts surrogate pairs in display names as Unicode scalars, so valid supplementary-plane names can pass from the signed JSON to the layout. It still rejects malformed UTF-16, control characters and formatting code points. The 5517 `ProtocolServerName` field keeps its ban on UTF-16 surrogate code units. This display change does not widen it.

## Threading and failure handling

One active OpenGL context owns the text resources, and the render thread creates and updates layouts. Releasing the context disposes the glyph resources together with the other login textures. No background thread can change the label cache or the selection. A missing font or a rendering failure is never treated as a valid catalog, a route or a selectable network endpoint.

## Testing

Unit tests cover Unicode catalog-name validation, grapheme-safe truncation, ink containment at both logical sizes, glyph coverage and regressions in the existing 5517 text layout. The full solution's Release tests, locked restore, the formatting check, the Windows and macOS checks and the retail-5517 content-set integrity check also run. Before the visual behavior can be called production-ready, it needs real driver-backed screenshots at both sizes that cover short and long names, a page change, an empty or expired catalog and unsupported glyphs.

## Limits

Drawing a supplementary scalar does not mean the host font has that glyph, or that whole scripts are shaped correctly. Licensed font coverage and shaping need separate internationalization work before publisher content of that kind is enabled. Login credentials, registration, the 5517 AccountServer transport, packet encryption and the handoff to the game are out of scope.
