# Client Architecture

OpenConquer Client is a cross-platform implementation of the Conquer Online 5517 client.

It preserves verified player-visible and wire behavior where compatibility matters, and replaces the earlier
implementation with testable .NET components that have clear responsibilities.

This document describes the product and subsystem architecture. Detailed compatibility research
lives in the compatibility documents.

## Products

The repository contains two executable products:

```text
OpenConquer.Launcher
OpenConquer.Client
```

Each runs in its own process, has its own dependencies, and is published separately.

`OpenConquer.Launcher` is the supported way to start the product.

`OpenConquer.Client` is the game runtime.

The launcher does not reference the game runtime projects. The launcher and client are published
separately and later combined into one installed product.

See [`launcher-managed-installation.md`](launcher-managed-installation.md) for the installed product
format.

## Projects

| Project | Responsibility |
| --- | --- |
| `OpenConquer.Launcher` | Desktop launcher, managed installation and release verification, local maintenance primitives, display preferences and verified client start |
| `OpenConquer.Client` | Game composition root, authenticated session and world lifetime, local hero state, login/role/HUD input and presentation coordination |
| `OpenConquer.Platform` | Desktop window, OpenGL context, framebuffer, input devices and host frame loop |
| `OpenConquer.Rendering` | OpenGL resources, sprite and mesh drawing, text and font resources, logical presentation, and reusable world projection/rendering |
| `OpenConquer.Content` | Client content sources, legacy format readers, asset decoding and world resource preparation |
| `OpenConquer.Networking` | Account and GameServer sessions, packet codecs, legacy crypto, world bootstrap and typed world dispatch |
| `OpenConquer.Content.Tool` | Offline import, content closure verification and map compilation, using Content |
| `OpenConquer.Product.Tool` | Local product composition and release/catalog tooling |

Client references Platform, Rendering, Content and Networking. Rendering references Content for
decoded assets and shared format representations. Platform and Networking have independent
responsibilities. The launcher publishes independently and starts the verified client executable
as a separate process. See the [solution](../../OpenConquer.Client.slnx) and the
[architecture tests](../../tests/OpenConquer.Launcher.Tests/LauncherArchitectureTests.cs).

## Launcher

`OpenConquer.Launcher` is a .NET 10 Avalonia desktop application. Before the desktop starts, the
process takes a per-user process lease. Later invocations activate the existing window instead of
starting a second launcher. The main thread holds the lease until the activation listener and the
desktop have shut down. See [launcher instances](launcher-instances.md) for scope, transport, and
failure behavior.

It is currently composed as follows:

```text
Program
├── private activation namespace and user-scoped process lease
├── activation listener
├── diagnostics
├── host exception observation
└── Avalonia
    └── App
        └── MainWindow (the play.exe dialog)
            ├── PlayScreen
            └── LauncherApplication
                └── ManagedInstallationResolver
```

The launcher currently owns:

- process startup and shutdown
- Avalonia lifetime
- the primary launcher window
- standard-user Windows process policy
- size-limited, best-effort local diagnostics
- fatal host-failure handling
- managed-installation evaluation
- update, repair, and verified rollback transactions for authenticated installed releases
- release selection from an authenticated catalog, and package download within size limits
- application state and cancellation
- non-secret display preferences and the settings dialog
- the verified play screen, start-mode records, and client start

`LauncherApplication` owns installation evaluation and its state. `MainWindow` owns startup
presentation, play, settings and the shared shutdown task. It drains pending work before close.

After an expected installation failure, the check can be retried against the same package root.
Checks run off the UI thread and never overlap. Shutdown runs a single drain operation that all
callers share, and a check that finishes late cannot replace the `Stopping` state. See
[managed installation](launcher-managed-installation.md) for details.

The window reproduces the retail `play.exe` dialog. See [the play screen](launcher-play-screen.md).

Display preferences are stored and edited as described in
[launcher display settings](launcher-settings.md). The view owns the unsaved draft, and the
settings session owns I/O cancellation and draining.

The local managed product can start its authenticated client. Acquisition and transaction code
has test and tool entry points. Production release-origin configuration, the player-facing update
and repair flow, and launcher self-update remain open. Client and Networking own account login,
realm selection and the game session.

### Privileges

The Windows launcher runs as the invoking user:

```text
requestedExecutionLevel = asInvoker
uiAccess = false
```

If installation or repair ever needs elevated privileges, that work must run in a separate, audited
privileged component. The launcher as a whole is never elevated.

### Diagnostics

Launcher diagnostics are local, structured, size-limited, redacted, and best-effort.

A failure in diagnostics must never make the launcher fail.

Raw `Exception` objects are not stored. The launcher writes a reduced diagnostic record that leaves
out application values and any exception text likely to expose credentials, paths, URLs, or other
data the log does not need.

The specific diagnostic size limits and mechanics are implementation details covered by tests. They
are not architectural requirements.

## Game Runtime

`OpenConquer.Client` is the only composition root for the game runtime.

The host delegates application phases to `ClientSession`, which composes `ClientLogin`,
`ClientRoleCreation` and `ClientWorld`. See [session ownership](client-session-ownership.md)
for CP2.1 transitions and shutdown, and [role-creation ownership](client-role-ownership.md)
for the role component's input and resource boundaries.

`ClientSession` advances the application phases and frame clock. `ClientLogin` owns the signed
realm, credential and login presentation flow. `ClientRoleCreation` manages the admitted
character-creation branch. `ClientWorld` owns the admitted session, ingress, heartbeat egress,
content preparation, local hero state and presentation lifetime. Work that can run off the
graphics thread prepares CPU content there. The context thread owns GPU upload, draw and release.

The local hero model applies supported server state and advances gauge followers. Rendering
receives the prepared geometry, assets and draw inputs. The HUD under
`OpenConquer.Client/UI/Hud` owns verified layout and control behavior. Platform supplies physical
pointer coordinates, and Rendering's presentation transform maps them to the logical viewport.
Content owns ANI and image decoding. The client uses the supported attributes and equipment in
the HUD and role presentation. Most action controls still stop at a client-level activation.

### Platform and Rendering

The current renderer uses OpenGL 3.3 Core through Silk.NET.

The game draws to a logical render surface that matches the original client's screen modes,
independent of the physical desktop framebuffer.

The supported compatibility screen modes are:

```text
800 × 600
1024 × 768
```

Desktop window size, window mode, and presentation policy are separate host settings.

This keeps the original logical coordinate system without forcing the desktop window to use the
same physical resolution.

Platform owns:

- desktop window creation
- OpenGL context lifetime
- physical framebuffer state
- frame-loop orchestration
- pacing mechanics
- desktop input-device lifetime
- converting desktop pointer positions from window-client coordinates to top-left
  host-framebuffer pixels
- buffer swapping

Platform reports pointer positions in physical host-framebuffer coordinates. It knows nothing about
the game's logical render size, presentation policy, HUD layout, hit-test regions, or retail UI
behavior.

Rendering owns:

- OpenGL API use
- render targets
- logical rendering
- presentation transforms
- GPU resources
- sprite rendering
- solid-rectangle rendering
- rendering-facing encoded-text behavior
- host font-resource discovery
- deterministic font resolution
- FreeType library, font-file, face, and rasterizer lifetime
- glyph rasterization and rasterizer-facing metrics

`PresentationViewport` converts host-framebuffer positions to logical render coordinates and
rejects positions outside the presented logical surface. Client composition connects Platform
pointer input to this Rendering-owned transform for the UI that is implemented.

For this reason Platform does not clamp pointer positions to the window or the logical render
surface. A position outside those areas stays outside, so the presentation or UI layer that owns it
can reject it instead of turning it into a false hit on the edge.

Font discovery asks the operating system for its registered and configured fonts instead of
searching the filesystem recursively.

The APIs used on each platform are:

```text
Windows → GDI + DirectWrite
macOS   → CoreText + CoreFoundation
Linux   → fontconfig
```

These APIs supply rendering resources and do not manage the desktop host, so they belong to
`OpenConquer.Rendering`.

`OpenConquer.Platform` knows nothing about font discovery, font resolution, FreeType, glyph
metrics, or text rasterization.

Rendering, in turn, does not depend on Platform to discover fonts.

Content owns text configuration. When an implemented runtime UI element that draws text needs it,
the Client composition root will pass the configuration from Content to Rendering.

Text compatibility details are in
[`../compatibility/native-text.md`](../compatibility/native-text.md).

### Content

`OpenConquer.Content` owns runtime content access and the legacy file formats. Format support is
based on documented research into the retail files.

Runtime consumers include the startup and login stage, role creation, authenticated terrain and
local-role presentation, and the HUD. The manifest records the imported payload, including map,
C3, ANI and image dependencies. The content plan documents how consumer requirements select
those files. See [runtime content](../content/retail-5517-content-plan.md).

`ClientContentClosure` defines the content that production code depends on.

These must always match:

```text
ClientContentClosure
        ==
runtime manifest
        ==
runtime payload
```

Supporting a file format does not by itself add anything to the runtime closure. Content is added
only when an implemented consumer needs it and the related compatibility behavior has been
verified.

Runtime ANI frame requirements are resolved with `LooseThenPackage`. When research shows that an
asset exists only as a loose file, or only inside a package, in the audited 5517 source,
conformance tests can require that specific retail source.

Retail `ini/package.ini` is import configuration. It registers the WDF archives used to resolve
package-backed requirements from an authorized retail source, and it is not part of the curated
runtime closure.

Retail WDF archives are only a source. The package entries that are needed are extracted into the
curated content set, and the archives themselves are not shipped.

When a loose file wins the lookup, its retail casing is kept. Logical ANI paths are still matched
case-insensitively, as the original client does.

Retail `Server.dat` is not used in production. It is an offline research and tooling file, it must
not appear in the production client publish, and it is not a runtime server catalog.

See [`../content/retail-5517-content-plan.md`](../content/retail-5517-content-plan.md) for runtime
content policy and [`../compatibility/server-dat.md`](../compatibility/server-dat.md) for the
`Server.dat` compatibility record.

## Networking and authority

A current publisher-signed realm selection supplies the account endpoint and 1060 realm identity.
The credential exchange is limited to literal IPv4 loopback. The AccountServer supplies the
GameServer handoff. Networking owns the DH/CAST5 session, world admission and verified bootstrap
ladder. The client handles the supported live packets through bounded world ingress and keeps
socket state separate from presentation.

Launcher release trust establishes installed product integrity. Signed realm-directory trust
establishes selection authority. Neither turns the legacy credential stream into authenticated
remote transport. OpenConquer registration uses the project's `0xF001` / `0xF002` extension.
See [login networking](../networking/README.md), [registration](../networking/FLOW-001B-account-registration.md)
and [world ingress](../world/WORLD-023-authenticated-world-ingress.md).

## Installed Product

Compiling, publishing, and installing are separate steps.

The launcher and client are published independently:

```text
launcher publish
client publish
```

Product composition creates:

```text
<product root>/
├── launcher files
├── openconquer.installation.json
└── releases/
    └── <release-id>/
        ├── openconquer.release.json
        ├── openconquer.release.sig
        └── client/
            └── client publish
```

The schema-v2 descriptor selects one versioned client generation, and switching it is atomic.
Supported transaction code never changes a generation in place. The descriptor may also name one
verified fallback.

The raw launcher publish must not contain `openconquer.installation.json`, `releases/`, or the
legacy root-level managed `client/` component. Product composition adds those.

`OpenConquer.Product.Tool` composes the product deterministically for local development and CI. It
is not the production installer, updater, signing system, or deployment system.

See [`launcher-managed-installation.md`](launcher-managed-installation.md) for the schema-v2 format
and the read-only support for schema v1.

## Publish Requirements

The client publish must:

- contain the verified runtime content closure
- contain no runtime `Server.dat`

The launcher publish must:

- stay independent of game runtime projects
- contain no retail game-content payload
- contain no installed-product descriptor
- contain no managed client component

The composed product must:

- contain the generated schema-v2 installation descriptor
- contain the client publish in the selected versioned release generation
- be resolvable by the real launcher installation resolver

Linux CI checks publish isolation. It checks the full client payload and composed product when
the private retail manifest is available. See [CI coverage](../development.md#ci).

## Architectural Rules

When adding code:

1. preserve verified behavior wherever interoperability depends on it;
2. keep desktop/window/input mechanisms behind Platform;
3. keep graphics, rendering resources, and rendering-facing text behavior behind Rendering;
4. keep content-format and configuration parsing behavior behind Content;
5. keep protocol behavior behind Networking;
6. keep launcher product behavior inside Launcher;
7. keep client-specific runtime behavior that coordinates subsystems inside Client;
8. keep subsystem projects independent unless a concrete ownership requirement justifies a
   dependency;
9. do not create shared/common utility projects without a concrete ownership need;
10. do not introduce speculative abstractions for future features;
11. make host and application resource ownership and lifetime clear in the code;
12. when platform APIs can provide the information, use the host's resource systems instead of
    guessing the filesystem layout;
13. treat tests and documentation as part of every finished change.

A passing build is required but not enough. Each change must also fit the architecture, be reviewed
and documented, and be justified on its own.
