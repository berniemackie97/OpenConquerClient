# STARTUP-001H — Unicode text layout foundation

## Boundary

The authenticated realm catalog from STARTUP-001G contains display strings, not CP936 or other retail-encoded byte sequences. Text intended for the modern login interface must not be converted into legacy bytes, because such conversion can lose identities and silently replace unsupported characters.

This slice adds a Unicode-scalar layout entry point to the existing Rendering-owned FreeType/OpenGL text context. It does not change the legacy byte-oriented layout contract or draw server labels yet. That presentation work must be completed in its own audited slice, including a font coverage policy and correct fitting within the approved login geometry. A completed string-to-glyph path is not proof of complete Unicode shaping, bidirectional ordering, font fallback, or glyph coverage. Those are separate presentation requirements.

## Contract

- `OpenGLTextContext.LayoutUnicode` accepts UTF-16 text and passes Unicode scalar values directly to the same existing font rasterization and glyph-atlas machinery.
- Supplementary-plane scalar values remain intact. Unpaired surrogates and NUL are rejected before rasterization.
- Unicode layout does not interpret `#NN` as retail data icons and does not depend on any legacy code page. LF retains the existing logical line-advance convention.
- Native encoded layout continues to use encoded 16-bit glyph identities; Unicode glyphs carry an independent scalar identity. Both layout types preserve the originating context and glyph-atlas ownership.
- A Unicode layout is bounded to 4096 UTF-16 code units, and each font context caches at most 4096 distinct direct Unicode scalar values. Exceeding a limit fails explicitly rather than expanding resources without a bound.
- Missing glyphs preserve the current rasterizer miss semantics; this slice does not invent a font-fallback or replacement-glyph policy.
- Existing OpenGL renderers, textures, text passes, content closure, catalog signature validation, and login presentation geometry are unchanged.

## Acceptance

Verify the new Unicode layout tests, existing native encoded-text and glyph-cache tests, full Release tests, and cross-platform build gates. Driver-backed glyph rendering and approved 800×600/1024×768 login-label placement remain requirements for the subsequent UI consumer. An empty or unavailable trusted realm catalog remains empty; no endpoints, server names, or authentication behavior are introduced here.
