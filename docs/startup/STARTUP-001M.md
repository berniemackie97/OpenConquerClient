# STARTUP-001M: Region and server page buttons

## Scope

Builds on the STARTUP-001K credential entry and adds mouse paging to the login screen. The STARTUP-001L packet serializer stays unused. Nothing here touches cryptography, account networking, registration, world transition or login submission.

Some groups in the startup/login asset reference have more servers than the twelve server slots the reconstructed screen shows. STARTUP-001F already splits regions and servers into pages and handles the PageUp and PageDown keys, but it had no visible mouse controls. The signed catalog allows up to 64 regions and 128 servers per region, so a player using only the mouse could not reach entries on other pages.

## Layout

Each control is a 24x24 hit rectangle on the reference stage, placed just outside the region or server cell grid. The controls are this project's own reconstruction. They are not taken from the Flash timeline or the retail button sprites, and they do not copy 5517 mouse behavior, which has not been confirmed. Their positions come from `LoginVisualLayout` and scale with the two supported 4:3 logical stages (1024x768 and 800x600). They are drawn with the existing OpenGL rectangle primitives, so they need no new textures, retail asset changes, Flash runtime or fonts. The region and server cell positions, the existing artwork, the account panel and the dragon do not move.

## State and input handling

- The page controls are hidden when a grid has zero or one page. With more pages, each direction is enabled or disabled depending on whether the current page is the first or the last. No controls appear when the catalog is empty, unconfigured, expired or rejected.
- A page change needs the press and the release on the same enabled control, with the same immutable catalog object, the same page and, for server paging, the same selected region object. Releasing outside the logical viewport, losing focus, a replaced catalog or a changed region cancels the click.
- Paging calls the existing `LoginServerSelection.ChangeRegionPage` and `.ChangeServerPage` methods, which revoke selections that are no longer valid. When that invalidates the selected server, the existing `LoginCredentialInput.Synchronize` erases the credentials. Changing the region page also clears server focus. The page arrows never select a server, start a login or open a socket.
- PageUp and PageDown follow the same selection rules as the mouse controls. Pointer input still goes through the transform from host framebuffer to logical viewport. The control layout does not change from frame to frame, and drawing builds no layouts and allocates no GPU resources per frame.

## Testing

Unit tests cover geometry, non-overlap, both resolutions, visibility, first and last page limits, press and release, disabled controls, cancellation on an off-screen release, focus loss, a press captured against a stale catalog snapshot or region, credential revocation and keyboard parity. Also run locked restore, formatting, the Release build and full tests, retail content-set verification and Windows/macOS CI. Visual conformance is checked on a real driver at 800x600 and 1024x768 with a signed test catalog, issued for that purpose, that has 17 regions and 13 servers.

## Remaining work

Startup UI work still open after this change: visual calibration against the original `start.dat` movie, Login and Register button artwork with disabled and pending states, feedback on catalog availability and errors, and command and status transitions that match the 5517 client once the protocol has been verified separately. A production publisher key and real account credentials have to come from outside the project. A fake live server should never be added just to fill the screen.
