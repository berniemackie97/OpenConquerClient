# STARTUP-001K — Pre-authentication credential entry boundary

## Native evidence and scope

The `20_notes/Architecture/03_login_flash_startup_command_contract.md` native research documents distinct `CMD_NAME` (`CLoginFlashDlg_HandleFlashCommandString` at `0x4AA72D`) and `CMD_PASSWORD` (`0x4AA7B3`) inputs. `20_notes/Server/login-credential-envelope.md` documents the verified 5517 AccountServer login envelope and the credential keypad character domain (`0-9 a-z A-Z . - @ > _`). These are the evidence anchors for input separation and conservative ASCII acceptance; the implementation does **not** perform the keypad transform, emit packet 1060, initiate a socket, persist an account, or invoke a native callback.

The verified retail art includes an account panel, but its exact interactive field placements have not been established from the Flash timeline. This slice places two explicitly **first-party reconstructed** input outlines inside the existing panel for usability. They are not asserted to be retail-exact, and manual screenshot validation at 1024×768 and 800×600 remains mandatory.

## Authority and input lifetime

- Until a catalog view has a currently selected server, the fields are invisible and refuse input. The only production catalog source is the previously verified signed authority. There is no default server, development endpoint, or untrusted configuration fallback.
- Changing catalog identity or selected-server identity clears the account and password arrays and removes focus. Catalog revocation/expiry is observed immediately on the render thread after the existing selection reset.
- Changing window focus, leaving the input area, disposing the input, or releasing the OpenGL context clears the password. `Escape` clears focus and password. No keyboard command can submit a login request in this slice.
- Both input buffers have conservative 32-character limits. Characters outside the verified keypad domain are dropped instead of coerced or silently encoded. The eventual networking slice must explicitly revalidate the account and credential semantics against the selected authoritative endpoint.
- Password input is held only in a fixed-length `char[]`; removed bytes are erased and the array is explicitly zeroed with `CryptographicOperations.ZeroMemory`. Neither password strings nor password glyphs are generated; rendering receives only a fixed `*` mask string whose length equals the current password length. Managed and operating-system-level copies of individual key events cannot be guaranteed to be erasable; this design does not claim otherwise.
- Account text is presented on screen only while an authoritative server is selected. Its existing field contains only validated ASCII, and long values show a bounded visible suffix. Password input is never written to files, clipboard, diagnostics, or catalog metadata.
- `DesktopWindow.TextEntered` is the only printable-character input path. `KeyPressed` handles editing commands; Ctrl/Alt/Super keyboard shortcuts do not alter the field. Pointer hit tests use the already established host-framebuffer-to-logical mapping, keeping letterboxing inert. No clipboard, paste API, IME, or hidden surrogate acceptance is introduced.
- Text resources belong to `LoginPresentation`, are created lazily for an enabled form, and are disposed before the graphics device. UI changes update cached layouts, never reconstruct them per unchanged frame. There is no status indicator or enabled Login/Register button before a verified AccountServer protocol implementation exists.

## Acceptance

Run focused model/layout tests, all existing login/catalog/rendering tests, locked restore, formatting, full Release build and tests, and retail content closure. Validate on real Windows/macOS drivers at both 4:3 logical resolutions: untrusted empty catalog shows no form, signed selected server reveals the account fields, keyboard focus and masking behave as specified, changing region/server or expiry clears input, losing desktop focus clears the secret, no login socket opens, and GL resources are released cleanly.

Native credential transform, seed exchange, packet framing and response handling require a separate audited slice. No assumption about a password's plaintext length beyond the conservative pre-auth input cap is treated as permission to send it over the wire.
