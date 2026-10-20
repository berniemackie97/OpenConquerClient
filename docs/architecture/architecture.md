# Client Architecture

OpenConquer Client is a cross-platform reconstruction of the Conquer Online 5517 client.

It keeps the retail client's behavior where compatibility matters, and replaces the original
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

```text
OpenConquer.Launcher
OpenConquer.Client
OpenConquer.Platform
OpenConquer.Gameplay
OpenConquer.Rendering
OpenConquer.Content
OpenConquer.Networking
```

What each project owns:

| Project | Responsibility |
| --- | --- |
| `OpenConquer.Launcher` | launcher process, UI, diagnostics, installation/readiness state, display preferences, the trusted installed-release transaction, and future release acquisition and controlled client launch |
| `OpenConquer.Client` | game-runtime composition root, game-process lifetime, and client-specific UI and runtime behavior |
| `OpenConquer.Platform` | desktop window, native graphics-context lifetime, framebuffer state, frame loop, pacing, and desktop input |
| `OpenConquer.Gameplay` | game state and gameplay behavior |
| `OpenConquer.Rendering` | OpenGL integration, logical rendering, presentation, GPU resources, rendering-facing text behavior, host font-resource discovery, and glyph rasterization |
| `OpenConquer.Content` | runtime client filesystem, legacy formats, decoding, loading, WDF/content lookup |
| `OpenConquer.Networking` | 5517-compatible game transport and protocol behavior, once implemented |

The game runtime dependencies point in one direction:

```text
OpenConquer.Client
├── OpenConquer.Platform
├── OpenConquer.Gameplay
├── OpenConquer.Rendering
├── OpenConquer.Content
└── OpenConquer.Networking
```

Subsystem projects do not depend on each other unless an actual ownership need calls for it.

Platform and Rendering, for example, are siblings. Platform owns the native desktop integration.
Rendering owns graphics behavior, GPU resources, and rendering resources such as font discovery and
rasterization.

Using operating-system font APIs does not make Rendering depend on Platform. Those APIs belong to
the Rendering text subsystem because they find resources that rendering consumes directly. They do
not manage the desktop host.

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
- the retail play screen, start-mode records, and client start

`MainWindow` only adapts state for display. Product state belongs to `LauncherApplication`.

After an expected installation failure, the check can be retried against the same package root.
Checks run off the UI thread and never overlap. Shutdown runs a single drain operation that all
callers share, and a check that finishes late cannot replace the `Stopping` state. See
[managed installation](launcher-managed-installation.md) for details.

The window reproduces the retail `play.exe` dialog. See [the play screen](launcher-play-screen.md).

Display preferences are stored and edited as described in
[launcher display settings](launcher-settings.md). The view owns the unsaved draft, and the
settings session owns I/O cancellation and draining.

The release acquisition code is not connected to any service yet, and no stand-in service is
provided. Production release-origin configuration and publishing, the player-facing update and
repair flow, launcher self-update, and controlled client startup are not implemented. Account login,
realm selection, and all game-session networking belong to Client and Networking, not to the
launcher.

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

At a high level:

```text
content/configuration
        │
        ▼
OpenConquer.Client
        │
        ├── Platform
        ├── Rendering
        ├── Gameplay
        ├── Content
        └── Networking
```

The client wires the subsystems together and controls their lifecycle. Each subsystem owns its own
mechanisms.

Runtime behavior that is specific to the client and coordinates several subsystems stays in
`OpenConquer.Client`. The main HUD is an example. HUD layout, state, availability, and interaction
rules live under `OpenConquer.Client/UI/Hud`. Content owns ANI and image decoding, and Rendering
owns the generic texture, sprite, primitive, and presentation code used to draw the HUD.

The main HUD is currently built from:

```text
Progress45 background
Progress40 life
Progress41 mana
Progress46 stamina
Progress47 extended stamina
Dialog4 panels
Progress42 skill / experience
10-button action strip
four main-HUD CMyCheck controls
```

The action strip lives in Client because it coordinates retail control state, logical hit testing,
pointer capture, ANI-backed assets, timing, and rendering.

The neighboring `CMyCheck` controls also live in Client. Client owns their retail control IDs, final
logical geometry, availability, two-state model, the state change that happens as soon as the left
button goes down, ANI-backed assets, and draw order. Their 32x32 artwork is drawn independently of
their 22x22 logical hit rectangles.

Platform reports pointer input in physical coordinates. Client converts it to logical coordinates
through Rendering's presentation transform, then passes it to the HUD input state.

An activated control is reported as a client-level identifier until the feature that owns the
action is implemented. Client does not guess gameplay, dialog, or network effects from the ANI
artwork.

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

The verified retail runtime closure currently contains 3,785 files totaling 21,319,841 bytes.

Current runtime consumers cover:

```text
screen-mode configuration
startup logos

Progress45 HUD background
Dialog4 HUD panels

Progress40 life
Progress41 mana
Progress42 skill
Progress46 stamina
Progress47 extended stamina

10-button main-HUD action strip
Mission button frames
Organise button frames
four PK button skins

four main-HUD CMyCheck controls
eight check-control frames

10-slot quickbar, action, magic, item-icon and glow families
selected-skill image, cover and cooldown text
category-8 status hints, Dialog21 backdrop, StrRes.ini and binary hotspot regions
category-9 Magic hints, retail magic metadata/effects and configured localization
```

The experience bar is drawn with solid rectangles, so it needs no retail image.

`content/retail-5517/manifest.json` is the source of truth for the full list of paths in the
closure. Consumer groups and import policy are described in
[`../content/retail-5517-content-plan.md`](../content/retail-5517-content-plan.md).

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

## Native-Parity Networking Direction

The client keeps the original 5517 network behavior wherever protocol compatibility is required.

The planned product flow is:

```text
OpenConquer.Launcher
    ├── installation/readiness and trusted release integrity
    ├── update/repair and recovery
    ├── pre-launch display preferences
    └── controlled, verified and authorized client startup
            ↓
OpenConquer.Client + OpenConquer.Networking
    ├── realm discovery/selection and login UI
    ├── AccountServer / MsgAccount / MsgConnectEx
    ├── native credential transformations and compatibility crypto
    └── GameServer / MsgConnect / character flow / gameplay
```

Launcher authorization confirms where the installed product came from. It does not authenticate the
game account.

The launcher must not depend on game-protocol packet types. Account registration and recovery need
a real account service that owns the accounts. No temporary desktop protocol is planned.

Packet layout, credential transformations, cryptography, result meanings, and the login-to-game
handoff follow research on the retail and deobfuscated binaries. OAuth/OIDC cannot replace the 5517
protocol.

The capability table in the README tracks what is implemented. This flow defines which product owns
each step.

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

CI checks all of these.

## Architectural Rules

When adding code:

1. match retail behavior wherever research has confirmed it matters for compatibility;
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
11. make native and managed resource ownership and lifetime clear in the code;
12. when platform APIs can provide the information, use the host's resource systems instead of
    guessing the filesystem layout;
13. treat tests and documentation as part of every finished change.

A passing build is required but not enough. Each change must also fit the architecture, be reviewed
and documented, and be justified on its own.
