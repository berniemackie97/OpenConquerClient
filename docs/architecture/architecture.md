# Client Architecture

OpenConquer Client is a modern cross-platform reconstruction of the Conquer Online 5517 client.

The architecture preserves native behavior where compatibility matters while replacing the original
implementation with explicit, testable .NET boundaries.

Detailed compatibility evidence belongs in dedicated compatibility documents. This document
describes only stable product and subsystem architecture.

## Product Boundaries

The repository contains two executable products:

```text
OpenConquer.Launcher
OpenConquer.Client
```

They are separate process, dependency, and publish boundaries.

`OpenConquer.Launcher` is the supported product entry point.

`OpenConquer.Client` is the game runtime.

The launcher does not reference the game runtime projects. The independently published launcher and
client are composed later into one installed product.

See [`launcher-managed-installation.md`](launcher-managed-installation.md) for that
installed-product contract.

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

High-level ownership:

| Project                  | Responsibility                                                                                                                                                                              |
| ------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `OpenConquer.Launcher`   | launcher process, UI, diagnostics, installation/readiness state, display preferences, trusted installed-release transaction, future release acquisition and controlled launch orchestration |
| `OpenConquer.Client`     | game-runtime composition root and game-process lifetime                                                                                                                                     |
| `OpenConquer.Platform`   | desktop window, native graphics-context lifetime, framebuffer state, frame loop, pacing, future desktop input                                                                               |
| `OpenConquer.Gameplay`   | game state and gameplay behavior                                                                                                                                                            |
| `OpenConquer.Rendering`  | OpenGL integration, logical rendering, presentation, GPU resources                                                                                                                          |
| `OpenConquer.Content`    | runtime client filesystem, legacy formats, decoding, loading, WDF/content lookup                                                                                                            |
| `OpenConquer.Networking` | native-compatible game transport and protocol behavior when implemented                                                                                                                     |

The game runtime dependency direction is:

```text
OpenConquer.Client
├── OpenConquer.Platform
├── OpenConquer.Gameplay
├── OpenConquer.Rendering
├── OpenConquer.Content
└── OpenConquer.Networking
```

Subsystem projects remain independent unless an actual ownership requirement justifies a dependency.

In particular, Platform and Rendering are siblings. Platform owns the native desktop mechanism;
Rendering owns graphics behavior and GPU resources.

## Launcher

`OpenConquer.Launcher` is a .NET 10 Avalonia desktop application. A user-scoped process lease
precedes desktop startup; subsequent invocations activate the existing window. The main thread owns
the lease until the activation listener and desktop have drained. See
[launcher instances](launcher-instances.md) for scope, transport, and failure behavior.

Its current composition is:

```text
Program
├── private activation namespace and user-scoped process lease
├── activation listener
├── diagnostics
├── host exception observation
└── Avalonia
    └── App
        └── MainWindow
            └── LauncherApplication
                └── ManagedInstallationResolver
```

The launcher currently owns:

- process startup and shutdown;
- Avalonia lifetime;
- the primary launcher window;
- standard-user Windows process policy;
- bounded best-effort local diagnostics;
- fatal host-failure handling;
- managed-installation evaluation;
- authenticated installed-release update, repair, and verified rollback transactions;
- application state and cancellation;
- non-secret display preferences and the owned settings dialog.

`MainWindow` is a presentation adapter. Product state belongs to `LauncherApplication`, not the UI.

Expected installation failures can be retried against the same package root. Checks run off the UI
thread and cannot overlap. Shutdown owns one shared drain operation; late check results cannot
replace `Stopping`. See the [installation contract](launcher-managed-installation.md) for details.

Display preferences have a separate [storage and dialog contract](launcher-settings.md). The view
owns its draft; the settings session owns I/O cancellation and draining.

Authoritative release acquisition, player-facing update/repair orchestration, launcher self-update,
and controlled client startup remain unimplemented. Native account login, realm selection and all
game-session networking belong to Client/Networking, not the launcher.

### Privilege Boundary

The Windows launcher runs as the invoking user:

```text
requestedExecutionLevel = asInvoker
uiAccess = false
```

Future privileged installation or repair work must use a separate audited privilege boundary rather
than elevating the entire launcher.

### Diagnostics Boundary

Launcher diagnostics are local, structured, bounded, redacted, and best-effort.

Diagnostic failure must not become launcher failure.

Raw `Exception` objects are not persisted. The launcher writes a restricted diagnostic projection
that excludes application values and exception text likely to expose credentials, paths, URLs, or
other unnecessary data.

Exact diagnostic budgets and mechanics are implementation details protected by tests rather than
architecture requirements.

## Game Runtime

`OpenConquer.Client` is the sole game-runtime composition root.

Its high-level flow is:

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

The client owns composition and lifecycle. Individual subsystems own their own mechanisms.

### Platform and Rendering

The current renderer uses OpenGL 3.3 Core through Silk.NET.

Rendering occurs against a legacy-compatible logical render surface independently of the physical
desktop framebuffer.

Current compatibility screen modes provide:

```text
800 × 600
1024 × 768
```

Desktop window size, window mode, and presentation policy are separate host concerns.

This distinction preserves the original logical coordinate model without forcing the modern desktop
window to use the same physical resolution.

Platform owns:

- desktop window creation;
- OpenGL context lifetime;
- physical framebuffer state;
- frame-loop orchestration;
- pacing mechanics;
- buffer swapping.

Rendering owns:

- OpenGL API use;
- render targets;
- logical rendering;
- presentation transforms;
- GPU resources.

### Content

`OpenConquer.Content` owns runtime content access and required legacy formats.

The current verified retail runtime closure is:

```text
data/main/Logo1.bmp
data/main/Logo2.bmp
ini/GameSetUp.ini
ini/info.ini
ini/package.ini
```

Historical formats are not kept in runtime assemblies without a production consumer.

Retail `Server.dat` is therefore offline evidence/tooling only. It must not appear in the production
client publish and is not a runtime server catalog.

See [`../compatibility/server-dat.md`](../compatibility/server-dat.md) for the compatibility record.

## Native-Parity Networking Direction

The reconstruction branch preserves the original 5517 network behavior where protocol compatibility
is required.

The intended product flow is:

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

Launcher authorization establishes product provenance; it does not authenticate the game account.
The launcher must not depend on game-protocol packet types. Registration/recovery entry points
require a real authoritative account service; no temporary desktop protocol is planned.

Native/deob evidence is authoritative for packet layout, credential transformations, cryptography,
result semantics and login-to-game handoff. OAuth/OIDC is not a substitute for the native protocol.
The capability table in the README tracks implementation status; this flow defines ownership.

## Installed Product

Compilation, publication, and installation are distinct boundaries.

The launcher and client publish independently:

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

The schema-v2 descriptor atomically selects one versioned client generation that supported
transaction code never mutates in place and may name one verified fallback. The raw launcher publish
must not contain `openconquer.installation.json`, `releases/`, or the legacy root-level managed
`client/` component. Those belong to product composition.

`OpenConquer.Product.Tool` provides deterministic local and CI composition. It is not the production
installer, updater, signing system, or deployment authority.

See [`launcher-managed-installation.md`](launcher-managed-installation.md) for the schema-v2
contract and its read-only schema-v1 compatibility path.

## Publish Invariants

The client publish must:

- contain the verified runtime content closure;
- contain no runtime `Server.dat`.

The launcher publish must:

- remain independent of game runtime projects;
- contain no retail game-content payload;
- contain no installed-product descriptor;
- contain no managed client component.

The composed product must:

- contain the generated schema-v2 installation descriptor;
- contain the client publish in the selected versioned release generation;
- be resolvable by the real launcher installation resolver.

CI enforces these boundaries.

## Architectural Rules

When adding code:

1. preserve native behavior where compatibility is evidence-backed;
2. keep platform mechanisms behind Platform;
3. keep graphics behavior behind Rendering;
4. keep content-format behavior behind Content;
5. keep protocol behavior behind Networking;
6. keep launcher product behavior inside Launcher;
7. do not create shared/common utility projects without a concrete ownership need;
8. do not introduce speculative abstractions for future features;
9. keep resource ownership and lifetime explicit;
10. treat tests and documentation as part of each completed work slice.

A green build is necessary but not sufficient. Each slice must also be architecturally coherent,
audited, documented, and independently justifiable.
