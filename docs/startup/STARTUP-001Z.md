# STARTUP-001Z — Credential viewport and pointer fidelity

## Baseline and scope

Based on merged PR #106, `main` commit `954cd921fffcb9147ab885ab5358bde273fffc15`. PR #108 initially added provisional account-history and music controls, but those were not retail-supported visuals and were removed before acceptance. **No original Flash control artwork or behavior is claimed by this slice.**

The approved changes complete the bounded STARTUP-001Y credential editor presentation: text/caret/selection scissoring within the original account-panel field geometry, deterministic framebuffer-to-logical pointer mapping when dragging into letterboxing, and repeated whole-character scrolling while a captured drag is held outside the visible field. The native input whitelist, capacity, password protections, catalog trust, disabled Enter button, cursor and audio remain unchanged.

## Implementation

- `OpenGLRenderer.Clip` constrains rendering within an exact logical top-left rectangle, translated to render-target OpenGL scissor coordinates. The scope cannot be nested, checks dimensions, and is disabled on exit and frame finalization. No render-time texture creation or content decoding.
- `LoginCredentialPresentation` applies the clip to each original credential field; selection backgrounds, masked characters and caret cannot draw over panel borders or unrelated controls.
- `PresentationViewport.MapPointerXClamped` maps the host framebuffer x-coordinate into the logical stage, including side letterboxes and outside-window positions. Only an already-captured drag uses this mapping; initial pointer hit tests continue to reject letterboxing.
- `ClientApplication` stores the latest pointer position, resolves the captured caret through the renderer's text measurement, and advances selection while the pointer remains held beyond the visible text at normal frame cadence. Focus loss, release and server/catalog invalidation cancel selection through existing input ownership.
- The original signed realm catalog, server display, retail-derived action assets, managed credential buffers and password zeroing are preserved. No local account-history persistence, BGM toggle, speculative account save/clear/list controls or outbound URLs are added.

## Native evidence and remaining work

The 5517 login Flash command evidence in `20_notes/Architecture/03_login_flash_startup_command_contract.md` includes account-history commands and `SELECTBGMUSIC`. Those native command names do **not** establish GUI bitmaps, click rectangles, timeline placement or standalone button behavior. The remaining original startup artwork and controls need direct investigation of the clean client's `data/main/start.dat` and nearby startup resources before implementing them. Do not infer a Save/Clear/List/BGM switch from commands or design modern substitutes.

## Acceptance

1. Locked restore, format check, Release build, full solution tests, content-set verification, `git diff --check`, cross-platform CI and security analysis.
2. Compare 1024x768 and 800x600 screenshots with merged #106: no extra account-panel buttons, dialogs, banners or music widgets; no regression to authentic 5517 sprites and sounds.
3. Enter 32 characters and drag across either field in both directions; masked password and account text remain clipped inside the original image. Holding the pointer past either field edge scrolls the selection without leaking pixels. Verify letterboxed and HiDPI layouts, and dragging beyond the window followed by release/focus loss.
4. Verify disabled Enter does not submit or connect; account/password boundaries and catalog invalidation remain intact.
5. Run driver-backed GL checks. CI passing does not prove image-perfect parity. Investigate real SWF timeline and control graphics before claiming page completion.
