# STARTUP-001K: Credential entry before authentication

## Sources and scope

The research note `20_notes/Architecture/03_login_flash_startup_command_contract.md` documents separate `CMD_NAME` (handled by `CLoginFlashDlg_HandleFlashCommandString` at `0x4AA72D`) and `CMD_PASSWORD` (`0x4AA7B3`) inputs in the 5517 client. `20_notes/Server/login-credential-envelope.md` documents the 5517 AccountServer login envelope and the characters the credential keypad accepts (`0-9 a-z A-Z . - @ > _`). These notes are the basis for keeping the account and password inputs separate and for accepting only that ASCII set. The implementation only collects input. It does not apply the keypad transform, send packet 1060, open a socket, store an account or call any of the 5517 command callbacks.

The retail art includes an account panel, but the Flash timeline has not yet shown where its interactive fields sit. To make the fields usable, STARTUP-001K draws two input outlines of its own inside the existing panel. They are a reconstruction and are not claimed to match the retail layout. They still have to be checked by hand against screenshots at 1024x768 and 800x600.

## Input lifetime

- Until the catalog view has a selected server, the fields are hidden and ignore input. In production, the catalog comes only from the signed publisher source that the client already verifies. There is no default server, no development endpoint and no fallback to untrusted configuration.
- Changing the catalog identity or the selected server clears the account and password arrays and removes focus. Catalog revocation or expiry is picked up immediately on the render thread, right after the existing selection reset.
- The password is cleared when the window loses focus, when focus leaves the input area, when the input is disposed and when the OpenGL context is released. `Escape` clears focus and the password. No keyboard command can submit a login request yet.
- Both input buffers are limited to 32 characters. Characters outside the keypad set are dropped. They are never converted or silently encoded. When networking is added, it must validate the account and credential rules again against the selected server's endpoint.
- The password is held only in a fixed-length `char[]`. Removed bytes are erased, and the array is zeroed with `CryptographicOperations.ZeroMemory`. No password string and no password glyphs are ever created. Rendering receives only a fixed `*` mask string whose length matches the current password length. Copies of individual key events held by the managed runtime or the operating system may not be erasable, and this design does not claim otherwise.
- Account text is shown only while a server from the signed catalog is selected. The account field holds only validated ASCII, and a long value shows only its last characters that fit. Password input is never written to files, the clipboard, diagnostics or catalog metadata.
- `DesktopWindow.TextEntered` is the only path for printable characters. `KeyPressed` handles editing commands, and Ctrl, Alt and Super shortcuts do not change the field. Pointer hit tests use the existing mapping from host framebuffer to logical coordinates, so clicks in the letterbox area do nothing. There is no clipboard or paste support, no IME and no hidden acceptance of surrogates.
- Text resources belong to `LoginPresentation`. They are created lazily once the form is enabled and are disposed before the graphics device. UI changes update the cached layouts, and unchanged frames do not rebuild them. There is no status indicator and no enabled Login or Register button until the AccountServer protocol is implemented and verified.

## Testing

Run the model and layout tests, all existing login, catalog and rendering tests, locked restore, formatting, a full Release build and tests, and the retail content closure check. On real Windows and macOS drivers, at both 4:3 logical resolutions, check that:

- an untrusted, empty catalog shows no form
- selecting a server from a signed catalog shows the account fields
- keyboard focus and masking behave as described above
- changing region or server, or catalog expiry, clears the input
- losing desktop focus clears the password
- no login socket opens
- GL resources are released cleanly

## Limits

The 5517 credential transform, seed exchange, packet framing and response handling need separate, reviewed work. The 32-character cap is only an input limit. It is not a statement about what password lengths are safe to send over the wire, and nothing is sent.
