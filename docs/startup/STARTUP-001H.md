# STARTUP-001H: Unicode text layout

## Scope

The authenticated realm catalog from STARTUP-001G holds display strings, not CP936 or other retail-encoded byte sequences. Text for the new login interface must not be converted to legacy bytes, because that conversion can make distinct names identical and silently replace characters it cannot represent.

STARTUP-001H adds a layout entry point for Unicode scalar values to the existing FreeType/OpenGL text context owned by Rendering. The legacy byte-oriented layout does not change, and server labels are not drawn yet. Drawing them is separate, reviewed work that needs a font coverage policy and must fit within the approved login layout. A path from string to glyph does not mean full Unicode shaping, bidirectional ordering, font fallback or glyph coverage. Those are separate presentation requirements.

## Behavior

- `OpenGLTextContext.LayoutUnicode` takes UTF-16 text and passes its Unicode scalar values straight to the existing font rasterizer and glyph atlas.
- Scalar values from the supplementary planes stay intact. Unpaired surrogates and NUL are rejected before rasterization.
- Unicode layout does not treat `#NN` as a retail data icon and does not use any legacy code page. LF advances to a new line the same way as in the existing layout.
- The retail encoded layout still identifies glyphs by their encoded 16-bit value. Unicode glyphs are identified by their scalar value, kept separate from encoded ones. Both layout types keep track of the context they came from and of glyph-atlas ownership.
- A Unicode layout takes at most 4096 UTF-16 code units, and each font context caches at most 4096 distinct Unicode scalar values. Going over either limit is an error, so resources never grow without a limit.
- Missing glyphs behave as they already do in the rasterizer. There is no font-fallback or replacement-glyph policy.
- The existing OpenGL renderers, textures, text passes, content closure, catalog signature validation and login layout are unchanged.

## Testing

Run the new Unicode layout tests, the existing retail encoded-text and glyph-cache tests, the full Release test suite and the cross-platform build checks. Glyph rendering on a real driver, and approved login-label placement at 800x600 and 1024x768, are still required of the UI code that will use this path. An empty or unavailable trusted realm catalog stays empty, and no endpoints, server names or authentication behavior are added.
