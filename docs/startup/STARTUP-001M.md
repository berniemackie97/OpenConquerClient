# STARTUP-001M — Visible login region/server pagination

## Scope and ordering

Continue the native client startup UI reconstruction after STARTUP-001K credential entry. STARTUP-001L's separate packet-framing utility remains dormant: this slice adds no cryptography, account networking, registration operation, world transition, or startup submission action.

The verified startup/login asset reference contains groups with more than the reconstructed twelve visible server slots. STARTUP-001F already implements bounded region/server pages and PageUp/PageDown keys, but provided no discoverable mouse controls. The current signed catalog admits up to 64 regions and 128 servers per region, so a mouse-only player could not reach off-page identities.

## Layout provenance

The controls use **first-party reconstructed** 24×24 reference-stage hit rectangles placed immediately outside the existing region and server cell grids. Their positions derive from `LoginVisualLayout` and scale with the two approved 4:3 logical stages (1024×768 and 800×600). They do **not** claim byte-for-byte reproduction of a Flash timeline, native button sprites, or previously unverified native mouse behavior. They use existing OpenGL rectangle primitives: no new textures, retail asset changes, Flash runtime, native behavior guesses, or font dependencies. Region/server cell positions, previously verified artwork, account panel and dragon remain unchanged.

## State and security

- Navigation affordances are invisible when a grid has zero or one page. When multiple pages exist, each direction is explicitly enabled/disabled by the current page bound. No controls appear in an empty, unconfigured, expired, or rejected catalog.
- Mouse navigation requires press and release within the **same enabled** control, the same immutable catalog object, the same page, and (for server paging) the same selected region object. A released pointer outside logical viewport, lost focus, stale catalog, or changed region cannot activate a page.
- Navigation uses the existing `LoginServerSelection.ChangeRegionPage` and `.ChangeServerPage` methods, which revoke stale selected identities; the existing `LoginCredentialInput.Synchronize` consequently erases credentials when a previously selected server is invalidated. Changing region pages clears server focus. No page arrow selects a server, authorizes login, or opens a socket.
- Keyboard PageUp/PageDown continue to use the same selection transition rules, and pointer input remains routed through the host-framebuffer-to-logical viewport transform. The control layout and draw calls are stable, with no per-frame layout construction or GPU resource allocation.

## Acceptance and follow-up

Verify unit tests for geometry, non-overlap, both resolutions, visibility, bounds, press/release, disabled controls, off-screen cancellation, focus loss, stale snapshot/region capture, credential revocation, and keyboard parity. Run locked restore, formatting, Release build and full tests, retail content-set verification, Windows/macOS CI, and real-driver visual conformance at 800×600 and 1024×768 using an explicitly authorized signed test catalog with 17 regions/13 servers.

The remaining startup UI work includes visual calibration against the original `start.dat` movie, login/register button chrome and appropriate **disabled/pending** states, catalog availability/error feedback, and native-equivalent command/status transitions after separate protocol verification. Production release authority and actual account credentials remain explicit external prerequisites; do not fabricate a live server to make the screen appear populated.
