# STARTUP-001N — Realm-directory availability presentation

## Rationale and placement in the startup roadmap

PR #94 introduced navigation controls visible only with more than one signed-catalog page. A production build without an embedded publisher authority deliberately starts with zero authorized realms; navigation, credentials, and realm-name controls are therefore absent. This can make the client look unchanged while the implementation is correct. This slice adds an **operational explanation** at startup without fabricating catalog records or authentication capability.

This remains part of native client startup **presentation** work. The standalone standard `1060` request serializer from STARTUP-001L is untouched and remains unconnected. No account transport, registered server, game bootstrap, or world gating is added here.

## Presentation contract

The status strip is placed below the existing server grid and above the existing account panel, deriving its geometry from `LoginVisualLayout` at either supported 1024×768 or 800×600 logical resolution. It uses a first-party, fixed uppercase 5×7 glyph raster and a modest dark-backed rectangle. This glyph atlas is **not claimed to be a native Flash resource**: it is explicitly a project-owned operational message. It introduces no retail manifest entries, host-font dependency, font discovery, per-frame glyph construction, background threads, or per-frame GPU allocation. Seven tiny fixed text textures are generated once per OpenGL context, remain immutable, and are released before the graphics device; partial constructors clean up resources.

The status is noninteractive and cannot select a region, initiate a login, override a signed catalog, or authorize a socket. It never displays URLs, account credentials, server names, signing keys, or network exception text. It does not interpret native `LOADING_TRACE` or `PROGRESS` callbacks: those are specifically reserved for the verified connect/bootstrap phase, not idle pre-login.

| State | Visible copy | Source |
| --- | --- | --- |
| Publisher authority absent | `REALM DIRECTORY NOT CONFIGURED` | No embedded authority / no `LoginCatalogRefresh` |
| Awaiting first verified snapshot | `CHECKING REALM DIRECTORY` | Authority configured, no current snapshot, no completed failure |
| No valid snapshot after rejection/failure/expiry | `REALM DIRECTORY UNAVAILABLE` | Catalog refresh has no `Current` and reports last attempt failed |
| Verified empty snapshot | `NO REALMS PUBLISHED` | Valid signature/expiry and zero published regions |
| Verified regions but no chosen region | `SELECT A REGION` | `LoginServerSelection.SelectedRegion` null |
| Chosen empty region | `NO SERVERS IN REGION` | Selected region has zero server entries |
| Region chosen but no server chosen | `SELECT A SERVER` | `SelectedServer` null |
| Server explicitly chosen | No overlay | Selection is ready for a future separately verified login workflow |

The catalog still starts empty and untrusted. A valid previously accepted catalog remains operational during temporary refresh failures under the existing STARTUP-001J policy. The status follows the authoritative state on the render thread, and cannot override or retain a revoked selected server.

## Verification

Before committing: locked restore, formatting, Release build, focused status tests, existing startup/login/catalog tests, full solution tests, retail content-set verification, and both platform CI workflows. Validate the default **unconfigured** build on a real macOS and Windows graphics driver at 1024×768 and 800×600: the visible `REALM DIRECTORY NOT CONFIGURED` strip should appear beneath the server grid, while realm names, credential fields, and pagination remain absent. Manually verify the other states only with explicitly authorized signed fixtures; do not embed or hard-code fake endpoints or authority in production. Verify shutdown and resource disposal in one OpenGL context. Cross-platform screenshots remain a manual visual-conformance gate.
