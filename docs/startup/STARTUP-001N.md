# STARTUP-001N: Realm directory status strip

## Purpose

The STARTUP-001M page controls appear only when the signed catalog has more than one page. A production build without an embedded publisher configuration starts with zero realms by design, so the page controls, credential fields and realm names are all missing. The screen then looks unchanged even though the client is working correctly. STARTUP-001N adds a short status message at startup that explains the current state. It adds no catalog records and no login capability.

This is display work for the startup screen only. The standard `1060` request serializer from STARTUP-001L is unchanged and still not connected. No account transport, registered server, game bootstrap or world gating is added.

## Presentation

The status strip sits below the server grid and above the account panel. Its geometry comes from `LoginVisualLayout` at either supported logical resolution, 1024x768 or 800x600. The text uses the project's own fixed uppercase 5x7 glyph raster on a plain dark rectangle. These glyphs are not a retail Flash resource, and the message is the project's own. The strip adds no retail manifest entries, needs no host font or font discovery, and does no per-frame glyph building, background work or per-frame GPU allocation. Seven small text textures are generated once per OpenGL context, never change, and are released before the graphics device. If construction fails partway, the resources already created are cleaned up.

The strip does not respond to input. It cannot select a region, start a login, override the signed catalog or open a socket. It never shows URLs, account credentials, server names, signing keys or network exception text. It does not use the 5517 `LOADING_TRACE` or `PROGRESS` callbacks, which belong to the connect and bootstrap phase rather than the idle screen before login.

| State | Visible copy | Source |
| --- | --- | --- |
| No publisher configured | `REALM DIRECTORY NOT CONFIGURED` | No embedded publisher configuration and no `LoginCatalogRefresh` |
| Waiting for the first signed snapshot | `CHECKING REALM DIRECTORY` | Publisher configured, no current snapshot, no failed attempt yet |
| No valid snapshot after a rejection, failure or expiry | `REALM DIRECTORY UNAVAILABLE` | Catalog refresh has no `Current` and reports that the last attempt failed |
| Signed snapshot with no realms | `NO REALMS PUBLISHED` | Valid signature and expiry, zero published regions |
| Regions available, none chosen | `SELECT A REGION` | `LoginServerSelection.SelectedRegion` is null |
| Chosen region is empty | `NO SERVERS IN REGION` | Selected region has zero server entries |
| Region chosen, no server chosen | `SELECT A SERVER` | `SelectedServer` is null |
| Server chosen | No overlay | The selection is ready for a future login flow, which will be verified separately |

The catalog still starts empty and untrusted. Under the existing STARTUP-001J rules, a valid catalog that was already accepted stays in use while refreshes fail temporarily. The status reads the current catalog and selection state on the render thread. It cannot override that state or keep a selected server that has been revoked.

## Testing

Run locked restore, formatting, the Release build, the status tests, the existing startup, login and catalog tests, the full solution tests, retail content-set verification and both platform CI workflows. Check the default unconfigured build on real macOS and Windows graphics drivers at 1024x768 and 800x600. The `REALM DIRECTORY NOT CONFIGURED` strip should appear below the server grid, and realm names, credential fields and page controls should be absent. Check the other states by hand, using only signed fixtures issued for testing. Fake endpoints or publisher keys must never be embedded or hard-coded in production builds. Also check shutdown and resource disposal within one OpenGL context. Visual conformance across platforms is still checked by hand from screenshots.
