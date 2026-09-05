# Client Architecture

This document describes the high-level architecture of OpenConquer Client for contributors and
developers interested in the project.

It is intentionally focused on product boundaries, subsystem boundaries, dependencies, ownership,
and lifetime.

Compatibility-specific native graphics requirements are documented separately. Historical
`Server.dat` evidence is documented separately because that format is retained by offline tooling,
not by the modern client runtime.

## Products and Projects

The repository now contains two independent executable products:

- `OpenConquer.Launcher`
- `OpenConquer.Client`

They are separate process and composition boundaries. The launcher is not a wrapper assembly around
the game client, and the game client is not a reusable library for the launcher.

```mermaid
flowchart TD
    Launcher["OpenConquer.Launcher"]

    Client["OpenConquer.Client"]

    Platform["OpenConquer.Platform"]
    Gameplay["OpenConquer.Gameplay"]
    Rendering["OpenConquer.Rendering"]
    Content["OpenConquer.Content"]
    Networking["OpenConquer.Networking"]

    Client --> Platform
    Client --> Gameplay
    Client --> Rendering
    Client --> Content
    Client --> Networking
```

`OpenConquer.Launcher` is its own executable composition root.

`OpenConquer.Client` is the sole game-runtime composition root. Game subsystem projects remain
independent unless a real ownership or behavioral requirement justifies a dependency between them.

There is deliberately no project-reference edge from `OpenConquer.Launcher` to `OpenConquer.Client`,
`OpenConquer.Platform`, `OpenConquer.Rendering`, `OpenConquer.Content`, `OpenConquer.Gameplay`, or
`OpenConquer.Networking`.

In particular, `OpenConquer.Platform` and `OpenConquer.Rendering` are sibling game-runtime
subsystems and do not reference one another.

Offline development and compatibility tooling is not part of either product's runtime dependency
graph.

## Responsibilities

| Project                  | Owns                                                                                                                                                                                                                      |
| ------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `OpenConquer.Launcher`   | launcher process entry point, Avalonia application lifetime, launcher-window shell, process failure policy, bounded diagnostics, future account/update/repair composition, and future authorized game-start orchestration |
| `OpenConquer.Client`     | game process entry point, startup-option validation, compatibility-derived runtime policy, game-subsystem composition, application lifetime, and shutdown coordination                                                    |
| `OpenConquer.Platform`   | desktop windowing, native graphics-context lifetime, physical framebuffer state, desktop frame-loop orchestration and pacing mechanics, native buffer swapping, and future desktop input                                  |
| `OpenConquer.Gameplay`   | game state, entities, movement, combat, interactions, and gameplay rules                                                                                                                                                  |
| `OpenConquer.Rendering`  | OpenGL integration, logical rendering, logical-to-host framebuffer composition, cameras, shaders, GPU resources, and render targets                                                                                       |
| `OpenConquer.Content`    | runtime client-root filesystem semantics, required legacy configuration and formats, decoding, loading, and content lookup                                                                                                |
| `OpenConquer.Networking` | game connections, transport, encryption, packet framing, protocol encoding, and decoding                                                                                                                                  |

Platform-specific window and context types remain inside `OpenConquer.Platform`. Rendering owns
graphics behavior and GPU resources without depending on the windowing subsystem.

Compatibility-derived game policy remains in `OpenConquer.Client`. Platform implements the desktop
mechanism required to apply that policy without knowing why a particular value was chosen.

Launcher UI and launcher process lifetime belong to `OpenConquer.Launcher`. They do not move into
the game's Platform project merely because both products have desktop windows.

Historical formats that have no production runtime consumer do not remain in runtime assemblies
merely because they are useful reconstruction evidence.

## Launcher Product Boundary

`OpenConquer.Launcher` is a .NET 10 desktop executable using Avalonia.

The launcher is an independent executable composition and security boundary:

```text
Program
    │
    ├── LauncherDiagnostics
    │
    ├── LauncherHostExceptionObserver
    │
    └── Avalonia AppBuilder
            │
            ▼
           App
            │
            ▼
        MainWindow
```

`Program` owns only executable-host composition and process lifetime policy.

It:

- creates the process-owned diagnostics boundary;
- creates and starts process-wide exception observation;
- configures the runtime Avalonia application;
- explicitly uses `ShutdownMode.OnMainWindowClose`;
- translates managed faults escaping the launcher host into a nonzero process exit code;
- disposes exception observation before diagnostics through deterministic scope ownership.

Filesystem policy, log formatting, exception projection, redaction, and global-event implementation
details remain outside `Program`.

The main-window lifetime is therefore the current launcher-process lifetime. Closing the primary
launcher window terminates the launcher rather than allowing an unrelated auxiliary window to keep
the process alive accidentally.

If a future product requirement introduces tray behavior, background patching, or another
long-running launcher mode, that lifetime policy must change explicitly rather than emerging from
additional windows.

### Launcher Process Privilege Policy

The Windows launcher has an explicit application manifest connected through the launcher project
file.

The launcher requests:

```text
requestedExecutionLevel = asInvoker
uiAccess = false
```

The account-bearing launcher therefore runs with the privileges of the process that invoked it
rather than requesting administrative elevation.

Administrative privilege must not become an implicit launcher-wide capability merely because a
future updater, repair operation, or installation action may require privileged work.

If future functionality genuinely requires elevation, that capability belongs behind a separately
designed and audited privilege boundary rather than changing the primary launcher process to
`requireAdministrator`.

The application manifest also declares the supported modern Windows compatibility identifier.

Avalonia remains responsible for DPI behavior. The launcher manifest deliberately does not introduce
a second independent DPI-awareness policy.

### Launcher Diagnostics Boundary

Launcher diagnostics are a process-host concern owned entirely by `OpenConquer.Launcher`.

The boundary is deliberately local, bounded, structured, and best-effort.

```text
launcher host event
        │
        ▼
LauncherDiagnostics
        │
        ├── lifecycle event
        │
        └── redacted exception diagnostic
                │
                ▼
        Serilog file sink
                │
                ▼
        bounded per-user JSONL
```

Persistent diagnostics are not an availability dependency.

Failure to establish the expected diagnostic storage boundary because of an ordinary I/O or access
failure, or a malformed storage path, causes diagnostics to degrade to a no-sink logger rather than
preventing launcher startup.

Path-format failures are caught only around directory creation, before logger configuration.
The expected-storage fallback is intentionally narrow. Programming and logging-configuration defects
are not converted broadly into silent persistence fallback.

Diagnostic writes and diagnostic disposal are likewise best-effort. Failure of the diagnostic sink
must not:

- convert a successful launcher run into a launcher failure;
- replace an application exception with a logging exception;
- obscure the original terminal failure;
- prevent normal process teardown.

The launcher uses platform-native per-user diagnostic locations:

```text
Windows
%LOCALAPPDATA%\OpenConquer\Launcher\Logs

macOS
~/Library/Logs/OpenConquer/Launcher

Linux
$XDG_STATE_HOME/OpenConquer/Launcher/Logs
```

When Linux does not provide a usable absolute `XDG_STATE_HOME`, the fallback is:

```text
~/.local/state/OpenConquer/Launcher/Logs
```

Relative XDG base-directory values are not treated as valid state roots.

Persistent launcher logs are newline-delimited JSON:

```text
launcher-*.jsonl
```

The current sink policy is:

- daily rolling;
- 5 MiB rolling threshold per file (the final event may cross the threshold);
- rolling on the size boundary;
- at most 14 retained log files;
- unbuffered writes;
- exclusive ownership rather than shared file writing.

The retention and size policy keeps normal diagnostic storage bounded rather than allowing launcher
logs to grow indefinitely.

The launcher currently records:

- launcher host start;
- launcher host stop and exit code;
- redacted unhandled-exception diagnostics.

Serilog is used directly at this host boundary. The launcher does not currently have a Generic Host
or dependency-injection composition model that would justify adding `Microsoft.Extensions.Logging`
plus an adapter solely to wrap this one logging boundary.

### Exception Diagnostic Projection

Raw exceptions are never supplied to the persistent logging sink.

Before an exception reaches Serilog it is projected into `LauncherExceptionDiagnostic`.

The projection contains only:

```text
ExceptionType
ExceptionTypeTruncated
HResult
StackTrace
StackTraceTruncated
InnerExceptions
InnerExceptionsTruncated
```

The stack trace is captured without source-file information. Only declaring-type and method names
are formatted; method signatures, parameter names, and virtual exception text are excluded. Stack text uses LF separators on every platform. Overloads may therefore share the
same diagnostic name; this is an intentional limit of the redacted representation.

The diagnostic projection deliberately excludes potentially secret-bearing or unnecessary exception
state such as:

```text
Exception.Message
Exception.Data
Exception.Source
Exception.TargetSite
source-file paths
arbitrary application parameters
request URLs
authorization headers
cookies
passwords
authorization codes
access tokens
refresh tokens
session tokens
```

This is particularly important because authentication and network-facing launcher services have not
yet been implemented. The security default is established before those future systems introduce
credential-bearing values.

Nested exception traversal is bounded.

The projection permits at most:

- eight nested exception levels;
- sixteen exception objects in total;
- 512 UTF-16 code units per exception type name;
- 32 stack frames and 4,096 UTF-16 code units of stack text per exception.

The logger permits a destructuring depth of 32 so it preserves every projected exception and child
collection, including the deepest truncation flag; its default depth discarded permitted children.
Truncation never splits a surrogate pair. Type-name, stack, and inner-exception truncation have
separate flags. Stack text is appended within its budget rather than formatting an unbounded trace
and then slicing it. Leaves use the shared empty child collection; populated collections are exposed
read-only without copying their backing list.

These limits bound the projected payload and its formatting work, not all CLR stack-capture or
reflection allocations. The runtime still materializes the captured stack and metadata names;
process-fatal resource exhaustion is not recoverable through this logger.

`AggregateException` children are traversed directly rather than flattened into an unbounded
intermediate representation.

When the diagnostic limit is reached, the representation records that inner exceptions were
truncated.

### Launcher Host Exception Policy

The launcher distinguishes handled application failures from faults escaping normal application
error handling.

The host observer covers:

```text
Avalonia Dispatcher.UnhandledException
AppDomain.CurrentDomain.UnhandledException
TaskScheduler.UnobservedTaskException
```

The top-level `Program.Main` catch remains a separate process boundary rather than another global
event subscription.

The high-level terminal path is:

```text
managed exception escapes launcher operation
        │
        ▼
host exception boundary
        │
        ├── classify source
        │
        ▼
redacted diagnostic projection
        │
        ▼
best-effort durable fatal event
        │
        ▼
nonzero launcher exit
```

Unknown UI-thread failures are not globally treated as recoverable.

The Avalonia dispatcher callback intentionally does not set `Handled = true`.

It performs only lightweight classification bookkeeping and lets the unknown fault escape toward the
top-level executable boundary. Durable projection and logging occur after the fault leaves the
dispatcher callback.

This avoids turning a global UI-exception hook into an implicit recovery mechanism and avoids doing
resource-intensive diagnostic work in the dispatcher exception event itself.

When the same UI exception reaches the top-level boundary, the observer classifies the failure as
`UiDispatcher`. Otherwise an exception escaping directly through the executable lifetime is
classified as `TopLevel`.

UI classification is consumed once so a stale prior dispatcher exception cannot classify a later
unrelated top-level failure.

`AppDomain.CurrentDomain.UnhandledException` records the runtime-provided terminating state.

`TaskScheduler.UnobservedTaskException` is treated as a non-terminating diagnostic boundary. The
exception is recorded through the same redacted projection and then marked observed.

Global exception callbacks must not throw secondary diagnostic failures. A diagnostics failure while
an unhandled exception is already being processed is therefore swallowed at that boundary so the
original failure remains primary.

`LauncherHostExceptionObserver` owns global event subscription and deterministic unsubscription.

Its lifecycle is explicit:

```text
construct observer
        │
        ▼
Start
        │
        ├── AppDomain subscription
        └── TaskScheduler subscription
        │
        ▼
Avalonia setup completes
        │
        ▼
attach UI Dispatcher subscription
        │
        ▼
desktop lifetime
        │
        ▼
Dispose
        │
        ├── remove Dispatcher subscription
        ├── remove AppDomain subscription
        └── remove TaskScheduler subscription
```

Subscription state is not published internally until the corresponding subscription operation has
succeeded.

The observer is not reusable after disposal, and duplicate start or duplicate dispatcher attachment
is rejected explicitly.

### Launcher Package Boundary

The launcher currently uses:

```text
Avalonia
Avalonia.Desktop
Avalonia.Themes.Fluent
Serilog
Serilog.Sinks.File
```

It deliberately does not currently depend on:

```text
Silk.NET
OpenConquer.Client
OpenConquer.Platform
OpenConquer.Rendering
OpenConquer.Content
OpenConquer.Gameplay
OpenConquer.Networking
Microsoft.Extensions.Hosting
Microsoft.Extensions.Logging
```

The launcher also deliberately introduces no speculative:

- account-authentication implementation;
- OAuth/OIDC client;
- registration or recovery UI;
- token cache;
- credential store;
- HTTP service layer;
- updater;
- repair system;
- launcher-to-game IPC;
- game-process launcher;
- launch grant;
- realm model;
- realm discovery;
- realm routing;
- legacy server connection profile.

Those concerns require separately audited boundaries when their actual contracts are introduced.

The launcher currently has a minimal presentation shell rather than placeholder feature screens.
Feature UI should follow implemented product state and service contracts rather than inventing fake
login, update, or realm workflows ahead of their architecture.

## Product Publish Boundary

Building an executable and publishing an executable are treated as different product guarantees.

CI publishes both executable products independently.

The game-client publish must contain the exact verified retail runtime content closure:

```text
data/main/Logo1.bmp
data/main/Logo2.bmp
ini/GameSetUp.ini
ini/info.ini
ini/package.ini
```

The published client is additionally checked for any `Server.dat` anywhere beneath the publish root.

The launcher publish is separately checked to ensure it does not acquire:

```text
content/retail-5517
```

The intended product relationship is therefore not:

```text
launcher package
└── game runtime internals and retail content
```

Instead each executable has its own publish boundary:

```text
OpenConquer.Launcher publish
        │
        └── launcher runtime only

OpenConquer.Client publish
        │
        └── exact game runtime content closure
```

Runtime-identifier-specific packages, installers, code signing, notarization, self-contained
deployment, and operating-system-native bundles remain future release-engineering concerns.

## Game Runtime Flow

The game runtime flow is directional, with `OpenConquer.Client` coordinating independent game
subsystems.

```mermaid
flowchart LR
    Server["Game Server"]
    Networking["Networking"]
    Client["Client"]
    Platform["Platform"]
    Gameplay["Gameplay"]
    Content["Content"]
    Files["Runtime Client Files"]
    Rendering["Rendering"]
    GPU["GPU"]

    Server <--> Networking
    Networking <--> Client
    Platform <--> Client
    Client <--> Gameplay
    Client --> Content
    Content --> Files
    Client --> Rendering
    Rendering --> GPU
```

The game process begins and ends in `OpenConquer.Client`. Platform provides the game desktop
runtime, Content provides access to required legacy client data, Networking communicates with game
servers, Gameplay owns simulation state, and Rendering consumes the state required to produce a
frame.

`OpenConquer.Launcher` is not part of this steady-state game-subsystem graph.

The future launcher-to-game authorization transition will cross a process boundary. It must not be
implemented as a direct launcher reference to game runtime internals.

A historical retail file is not automatically part of this runtime flow. Compatibility evidence may
instead terminate at an offline tool or parity test.

## Startup and Content Boundary

Game startup configuration belongs to the game executable composition boundary rather than to
Content, Platform, or Rendering.

`ClientStartupOptions` interprets the currently supported game-process arguments before the
application is created.

The supported startup form is:

```text
OpenConquer.Client [--content-root <path>] [--presentation <fit|integer|stretch>]
```

With no explicit content root, startup uses the versioned `content/retail-5517/payload` set staged
under `AppContext.BaseDirectory`, making content discovery deterministic relative to the executable
rather than dependent on the process working directory.

An explicit `--content-root` may be absolute or relative. Relative overrides are resolved against
the process working directory at startup and normalized to an absolute path.

Malformed startup input is rejected before application construction. Unknown arguments, duplicate
content-root declarations, and missing content-root values are not silently ignored.

The resulting content-root path and presentation policy are passed into `ClientApplication`. The
application constructs the composite `PackagedClientContentSource`; Content does not depend on or
receive the executable's complete startup-options object.

```text
process arguments
        │
        ▼
ClientStartupOptions
        │
        ├── absolute ContentRootPath
        └── PresentationPolicy
                │
                ▼
        ClientApplication
                │
                ▼
    PackagedClientContentSource
        │
        ├── loose files through ClientContentRoot
        └── package entries through WdfArchive
```

Direct game-process startup remains available for development and reconstruction work.

It is not the intended final production account-authentication mechanism. Future production startup
will introduce an explicit authorized launcher-to-game transition without using ordinary process
arguments as a transport for credentials or native session secrets.

`ClientContentRoot` establishes the legacy client filesystem boundary.

It:

- requires an existing, inspectable root directory
- rejects a content root that is a symbolic link or reparse point
- accepts legacy `/` and `\` separators
- resolves path segments case-insensitively to preserve Windows-era client lookup semantics on
  case-sensitive filesystems
- rejects rooted content paths
- rejects `.` and `..` traversal segments
- rejects symbolic links and reparse points at every resolved content-path segment
- rejects ambiguous case-insensitive matches instead of selecting one nondeterministically
- distinguishes optional lookup from required-file lookup

The content boundary does not permit filesystem indirection through symbolic links, junctions, or
other reparse points. A resolved content path therefore cannot escape the configured client root
through a linked child entry.

Filesystem metadata, enumeration, and I/O failures are not converted into false "not found" results.
In particular, inaccessible or otherwise unreadable content roots preserve their underlying
filesystem failure instead of being misreported as missing directories.

The first production consumer of this boundary is the retail screen-mode configuration.

At startup, `GameSetupConfiguration` resolves:

```text
ini/GameSetUp.ini
```

and reads:

```ini
[ScreenMode]
ScreenModeRecord=<value>
```

Legacy section and key matching are case-insensitive. The file is read using Latin-1-compatible text
decoding appropriate for the legacy client data.

The verified retail screen-mode mapping is:

| ScreenMode | Logical resolution |
| ---------: | -----------------: |
|          0 |            800×600 |
|          1 |            800×600 |
|          2 |           1024×768 |
|          3 |           1024×768 |

Missing configuration, a missing screen-mode value, or a value outside the verified range fails
startup explicitly rather than inventing unsupported fallback behavior.

The Content project interprets the legacy configuration. It does not construct Rendering types.
`ClientApplication` bridges the resulting dimensions into `LogicalRenderSize`, preserving the
dependency boundary between Content and Rendering.

### Retail Content Closure

The versioned retail runtime payload is consumer-led rather than a bulk client-tree mirror.

`ClientContentClosure` defines the exact paths required by implemented runtime consumers, and the
import and verification tooling requires that code closure, manifest path set, and physical payload
path set remain identical.

The current retail-5517 runtime closure contains exactly:

```text
data/main/Logo1.bmp
data/main/Logo2.bmp
ini/GameSetUp.ini
ini/info.ini
ini/package.ini
```

A retail file enters that closure only with an implemented, reviewed runtime consumer.

Large WDF archives remain outside it because no implemented runtime consumer requires an archive
entry. `ini/package.ini` remains inside the closure because package declaration and routing behavior
are implemented and observable even when the declared archives themselves are absent.

Historical compatibility fixtures are separate from this equality.

Retail `Server.dat`, for example, is preserved under the content-tool test tree but is deliberately
absent from:

```text
ClientContentClosure
content/retail-5517/payload
content/retail-5517/manifest.json
published OpenConquer.Client runtime content
```

### Legacy Server.dat Tooling Boundary

Native 5517 used a loose-root `Server.dat` file for the historical login/server-selection bootstrap.

That format remains valuable reconstruction and protocol evidence, but it is not the production
configuration model for the modern client.

The retained ownership is:

```text
tests/OpenConquer.Content.Tool.Tests/TestData/retail-5517/Server.dat
        │
        ▼
OpenConquer.Content.Tool.Legacy.ServerDat
        │
        ├── ServerDatFileReader
        ├── ServerDatEnvelopeDecoder
        ├── ServerDatNativePublicKey
        └── ServerDatXmlCatalogReader
                │
                ▼
        historical ServerDatCatalog
```

This boundary is intentionally outside `OpenConquer.Content`.

The file reader accepts one explicit filesystem path. It does not use:

- `IClientContentSource`;
- `ContentLookupMode`;
- loose/package runtime fallback;
- WDF routing;
- a runtime server directory abstraction.

The hardened decoder preserves the verified native RSA/PKCS#1/gzip envelope and the verified
`outenserver` XML structure while applying explicit modern resource-safety limits.

The historical model preserves source semantics including:

```text
FlashName
FlashIcon
FlashHint
ServerName
ServerIP
ServerPort
```

It deliberately does not normalize those fields into modern concepts such as `Realm`, `Host`, or
`Endpoint`.

The exact retail data proves that this distinction matters: multiple rows have different `FlashName`
and `ServerName` values.

The retained exact fixture resolves to 14 groups and 94 server rows.

Detailed evidence and security interpretation are documented in
[`docs/compatibility/server-dat.md`](../compatibility/server-dat.md).

### Native-Parity Authentication and Launcher Lifecycle

`OpenConquer.Launcher` is the modern implementation of the retail `Play.exe` product role. It is the
supported production entry-point target, with its own composition root and standard-user process
policy. No second user-facing `Play.exe` is introduced in front of it.

The current implementation provides the host, diagnostics, and window shell only. The remaining
production lifecycle is:

```text
installation discovery and readiness
        ↓
update / repair as required
        ↓
launcher and supported pre-launch game settings
        ↓
native 5517 account login and AccountServer authentication/handoff
        ↓
Play eligibility
        ↓
controlled launcher → OpenConquer.Client startup/handoff
        ↓
native-compatible game bootstrap
```

This reconstruction preserves the native account protocol, packets, credential transformation,
AccountServer result semantics, and login-to-game handoff. OAuth, OIDC, PKCE, bearer-token login,
and replacement account or game authentication systems are outside reconstruction scope.
The previous proposed authenticated realm-routing architecture is superseded by this parity rule.

Native/deob evidence is authoritative, followed by retail artifacts, legacy reconstruction, and the
current rewrite. Before implementation, authentication must be checked against both native evidence
and the current server contract. No protocol details are established by this host-only slice.

Removing `Server.dat` from runtime remains intentional. A replacement source for server selection
must preserve the native login contract, including any protocol server-name field, while keeping
endpoints out of UI code. Its trust boundary and position in the flow require an evidence audit;
server discovery is not assumed to occur after authentication.

The launcher owns the login operation in the target architecture. Credentials are not persisted in
plaintext or unnecessarily duplicated in the client. Passwords, keys, and sensitive session material
must not be logged or passed through arguments, environment variables, or plaintext temporary files.
Local launcher-to-client IPC is a separate process-lifecycle concern from the native protocol on the
wire. Its implementation must transfer the required native state without creating another game
login system.

Product state, installation integrity, settings, authentication, and launch eligibility will be
application-owned responsibilities consumed by thin UI code. They are not yet implemented. Direct
game execution currently remains a development path; a future audited handoff slice must enforce
the supported production boundary without expanding into gameplay.

See [the launcher roadmap](launcher-roadmap.md) for slice ordering and completion criteria.

## Startup Logo Lifetime

The retail logo is an optional initialization surface, not content in the main game framebuffer.

Startup reads `ini/info.ini[DlgLogo]BgFormat` through the loose-filesystem path used by the native
INI loader, selects variant 1 or 2 from the monotonic tick parity, and attempts to decode the
selected 24-bit bitmap. Package lookup is not used for the startup logo.

The logo boundary deliberately preserves the native non-fatal behavior. A missing configuration file
falls back to the verified retail format, while an unusable format, unsafe resolved path, missing
bitmap, inaccessible bitmap, or bitmap-decoding failure makes the visual splash unavailable without
aborting client startup.

When no image is available, `OpenGLStartupSplash` creates no native window or OpenGL context.

When an image is available, `OpenConquer.Client` composes a dedicated borderless `StartupWindow`
with a dedicated Rendering device and startup renderer. The window's logical dimensions are the
bitmap's natural dimensions. On scaled displays the physical framebuffer may be larger, and the
startup renderer accounts for that framebuffer scaling while preserving the image's natural logical
size.

The lifetime order is a tested invariant:

```text
load startup configuration and optional bitmap
        ↓
if no usable bitmap: continue without a startup window
        ↓
if usable: create borderless startup window and OpenGL context
        ↓
present the selected logo once
        ↓
perform synchronous runtime initialization
        ↓
destroy startup renderer, context, and window
        ↓
construct main DesktopWindow
        ↓
create the main logical renderer
```

There is no artificial minimum splash duration. Native evidence shows no sleep, wait, timer, or
minimum-presentation interval between initialization and destruction of the startup logo. The modern
client therefore presents the surface and lets real synchronous initialization determine its
lifetime rather than introducing an invented delay.

The main `OpenGLRenderer` has no startup-logo resource or draw path. This keeps startup resources
out of steady-state rendering and preserves the native separation between the one-shot
initialization surface and the main client window.

## Platform Boundary

`OpenConquer.Platform` owns behavior whose semantics come from the game desktop environment:

- native startup and main-window creation and destruction
- OpenGL context creation and lifetime
- physical framebuffer dimensions
- desktop frame-loop orchestration
- frame-pacing mechanics
- native host-buffer swapping
- focus and window state
- desktop input when introduced

Silk.NET windowing and input types must not leak into Gameplay, Rendering, Content, or Networking.

`OpenConquer.Client` depends on Platform as a consumer and game composition root rather than
implementing platform behavior itself.

`OpenConquer.Launcher` does not depend on this game Platform project. Avalonia owns the launcher's
desktop UI mechanism.

Framebuffer dimensions are represented by `PixelSize`. Zero-sized dimensions are valid because a
desktop framebuffer may temporarily have no drawable area while minimized. Negative dimensions are
rejected at the type boundary.

## Desktop Frame Cadence

The outer client frame cadence is an application compatibility policy rather than an intrinsic
Platform or Rendering constant.

Verified retail behavior gates the outer client frame pipeline on a 25 ms interval. The modern
client therefore defines that interval in `ClientApplication` and supplies it when constructing
`DesktopWindow`.

```text
ClientApplication
        │
        │ retail frame interval: 25 ms
        ▼
DesktopWindow
        │
        │ frame-loop orchestration
        ▼
DesktopFramePacer
        │
        │ monotonic elapsed-time measurement
        │ remaining-interval wait
        ▼
next desktop frame
```

`DesktopFramePacer` is an internal Platform mechanism. It has no Conquer-specific timing constant
and operates only on the interval supplied by its caller.

The pacer uses a monotonic `TimeProvider` timestamp source. A frame that completes early waits for
the remainder of its interval. If work overruns the interval, the next frame proceeds without an
additional wait.

Missed intervals are not replayed. After an overrun, the actual next frame becomes the new cadence
anchor instead of running multiple catch-up frames.

Sleep completion is not assumed to be exact. The pacer rechecks elapsed monotonic time after every
wait, allowing it to handle early wakeups and scheduler oversleep without relying on nominal sleep
duration.

Silk.NET's own render and update frequency limiters remain uncapped because OpenConquer owns the
outer loop cadence explicitly through the lower-level custom view loop. Applying both pacing layers
would create competing timing policies.

Desktop events are processed before frame pacing so native window state is serviced before update
and rendering work for the next frame.

This cadence is not a gameplay simulation clock and must not become one implicitly. Future gameplay
simulation, startup-host timers, networking deadlines, animation clocks, and other timing domains
remain separate unless native behavior proves they share this contract.

## Rendering Boundary

```mermaid
flowchart TD
    Platform["OpenConquer.Platform"]
    Client["OpenConquer.Client"]
    Gameplay["Gameplay State"]
    Rendering["OpenConquer.Rendering"]
    Silk["Silk.NET.OpenGL"]
    OpenGL["OpenGL"]
    GPU["GPU"]

    Platform --> Client
    Gameplay --> Client
    Client --> Rendering
    Rendering --> Silk
    Silk --> OpenGL
    OpenGL --> GPU
```

Platform owns the native window and OpenGL context. Rendering owns the OpenGL API binding and GPU
resources. Client composes those lifetimes without creating a direct dependency between Platform and
Rendering.

Rendering also owns the logical-to-host framebuffer composition step. Platform owns the subsequent
native buffer swap.

### OpenGL Bootstrap

The native OpenGL context is owned by `OpenConquer.Platform`.

Once the desktop window has initialized its graphics context, Platform exposes a narrow borrowed
`IOpenGLContext` capability to `OpenConquer.Client`. Silk.NET window and context types remain
internal to Platform.

Client bridges the Platform-owned context into Rendering through an OpenGL procedure-address
resolver:

```text
OpenConquer.Platform
        │
        │ IOpenGLContext.GetProcAddress
        ▼
OpenConquer.Client
        │
        │ OpenGLProcAddressResolver
        ▼
OpenConquer.Rendering
        │
        │ GL.GetApi(...)
        ▼
Silk.NET.OpenGL
```

`OpenConquer.Rendering` therefore does not depend on `OpenConquer.Platform`, `Silk.NET.Windowing`,
or Silk.NET context types.

`OpenGLGraphicsDevice` owns the Silk.NET OpenGL API binding but does not own the native OpenGL
context.

Graphics-device initialization verifies that the active context satisfies the renderer's OpenGL 3.3
Core requirement. The device also captures the runtime OpenGL, GLSL, vendor, and renderer identity
for diagnostics and compatibility reporting.

Platform guarantees that the OpenGL context exists and is current when graphics initialization,
frame rendering, and graphics shutdown callbacks execute.

## Logical Rendering and Host Composition

The logical game surface is independent of the physical desktop framebuffer.

`OpenConquer.Rendering` receives an explicit `LogicalRenderSize` selected by the application.
Logical dimensions must be positive.

The application obtains the logical size from the verified retail `ScreenMode` value in
`ini/GameSetUp.ini`. Modes 0 and 1 select 800×600; modes 2 and 3 select 1024×768.

Rendering does not read legacy configuration and does not derive the logical size from the desktop
window.

`OpenConquer.Platform` separately reports the physical framebuffer through `PixelSize`. Its
dimensions change as the resizable host window changes.

```text
GameSetUp.ini
        │
        │ ScreenMode
        ▼
GameSetupConfiguration
        │
        │ 800×600 or 1024×768
        ▼
LogicalRenderSize
        │
        ▼
OpenGLRenderTarget
        │
        │ render logical frame
        ▼
fixed logical framebuffer
        │
        │ PresentationViewport places the frame
        │ OpenGLRenderer color blit
        ▼
physical host framebuffer
        │
        │ Platform native buffer swap
        ▼
desktop window
```

The distinction is intentional:

- logical dimensions define the game rendering coordinate space
- physical framebuffer dimensions define only the host composition destination
- resizing the desktop window does not change the logical game resolution
- minimizing the host may temporarily produce a zero-sized physical framebuffer without changing the
  logical render target

The original 5517 client supports four screen modes spanning 800×600 and 1024×768 logical
resolutions. The modern client preserves the verified logical-resolution selection while
intentionally retaining its resizable desktop-host policy.

The shell behavior associated with retail modes 1 and 3 is not inferred by Rendering from the mode
integer. Desktop-window behavior remains a Platform/application policy separate from logical
rendering resolution.

The desktop host is intentionally resizable. This is a modernization over the original fixed-window
behavior while preserving the game's fixed logical rendering coordinate system.

### Presentation transform

`PresentationViewport` is the single definition of where the logical frame lands inside the host
framebuffer. `OpenGLRenderer` blits into the rectangle it describes, and input maps pointer
positions back through `PresentationViewport.TryMapToLogical`. Deriving those two independently is
how a client draws in one place and resolves clicks in another, so neither side computes its own.

The transform holds no graphics-API type and is unit tested without a device.

`PresentationPolicy` selects how the frame is fitted:

| Policy          | Placement                                                                                                            | Filter                                                               |
| --------------- | -------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------- |
| `Fit` (default) | largest uniform scale that fits, centred                                                                             | point when the result is an exact whole multiple, otherwise bilinear |
| `IntegerScale`  | largest whole-number scale that fits, centred; falls back to `Fit` when the window is smaller than one logical frame | point                                                                |
| `Stretch`       | fills the host framebuffer                                                                                           | bilinear unless the result happens to be exact                       |

`Fit` and `IntegerScale` preserve the logical aspect ratio and leave pillarbox or letterbox bars.
`Stretch` is retained only as an explicit opt-in: a 4:3 logical frame across a 16:9 host is
stretched about 1.33x horizontally, so it is never the default.

Point sampling is chosen from the resulting rectangle rather than from the policy, so a `Fit` window
that lands on an exact whole multiple gets the sharper result too.

The policy is selected with the `--presentation fit|integer|stretch` startup option.

Because an aspect-preserving rectangle does not cover the whole host framebuffer, `OpenGLRenderer`
clears the host framebuffer before blitting whenever bars are present. The bars are never written by
the blit and would otherwise show whatever the swap chain left behind.

Positions inside a bar are not positions in the game world. `TryMapToLogical` reports them as
unmapped rather than clamping them to the nearest edge.

### Pointer mapping

Two conversions sit between a pointer and a logical pixel, and both are owned rather than left to
call sites.

```text
pointer position (window coordinates, top-down)
        │
        │ DesktopWindow.PointToFramebuffer
        ▼
host framebuffer pixels (top-down)
        │
        │ PresentationViewport.TryMapPointerToLogical
        ▼
logical pixel (top-down), or unmapped
```

`DesktopWindow.PointToFramebuffer` delegates to the windowing layer rather than deriving a size
ratio. Window coordinates and framebuffer pixels are the same only at a scale factor of one, and a
transform built from `FramebufferSize` without this conversion resolves clicks at a fraction of
their true position. That defect is invisible on an unscaled display.

The coordinate space the input layer reports was measured rather than assumed. On a 2x display, a
640x480 window reports a 1280x960 framebuffer, and with the operating-system cursor at global
position (965.2, 700.7):

```text
window.PointToClient(cursor)     = (915, 639)
IMouse.Position                  = (915.2422, 638.7461)   <- matches, within rounding
PointToFramebuffer(915, 639)     = (1830, 1278)           <- does not match
```

Pointer positions therefore arrive in window coordinates, and the conversion is required rather than
optional. `PointToFramebuffer` was also confirmed to be exactly linear and origin-preserving on that
display: `(0,0)` maps to `(0,0)`, `(1,1)` to `(2,2)`, and `(640,480)` to `(1280,960)`.

Reproduce by creating a window, reading `Size` and `FramebufferSize`, then comparing
`IMouse.Position` against `PointToClient` of the operating-system cursor position. A machine at a
scale factor of one cannot distinguish the two spaces and will not detect a regression here.

Pointer positions are fractional and the windowing conversion accepts whole window coordinates only,
so `PointToFramebuffer` rounds to the nearest rather than truncating, which would bias every pointer
up and to the left by half a window coordinate. Pointer precision is bounded at one window
coordinate, finer than the logical pixels it maps to.

`TryMapPointerToLogical` flips vertically at both ends: once to reach the bottom-up framebuffer row
the destination rectangle is expressed in, and once to return a top-down logical row. Performing
only the first mirrors every position about the horizontal centre — every position still maps
successfully, so only an ordering check detects it.

`TryMapFramebufferPointToLogical` is the bottom-up form, for framebuffer-space work. The two are
named apart so neither origin can be assumed from a signature.

A position inside a letterbox bar is not a position in the game world. Both methods report it as
unmapped rather than clamping it to the nearest edge.

Under minification some logical pixels have no pointer position that resolves to them: a window
smaller than the logical frame has fewer destination pixels than logical ones. This is inherent to
downscaling rather than an off-by-one, and it bounds pointer precision at small window sizes.

## Logical Render Target

`OpenGLRenderTarget` owns the logical framebuffer and its GPU attachments:

```text
logical framebuffer
├── RGB565 preferred / RGB5 fallback color texture
└── 16-bit depth renderbuffer
```

The logical target intentionally has no stencil attachment.

The render target preserves the verified retail 5517 16-bit color and depth contracts. Rendering
prefers an actual 5/6/5/0 color allocation and falls back to an actual 5/5/5/0 allocation. Requested
color formats are accepted only after OpenGL reports the expected component sizes, and the depth
renderbuffer is accepted only when OpenGL reports exactly 16 depth bits.

Frame initialization establishes deterministic full-target clear state. Scissoring is disabled,
color and depth writes are enabled, the color buffer is cleared to opaque black, and the depth
buffer is cleared to `1.0`.

OpenGL dithering is explicitly disabled for logical framebuffer rendering to preserve the verified
retail 16-bit rendering behavior rather than inheriting OpenGL's default dithering state.

The native evidence and compatibility requirements are documented in
[`docs/compatibility/native-graphics.md`](../compatibility/native-graphics.md).

The desktop/default framebuffer is host-composition-only. Rendering blits only color into it, so
Platform does not request depth or stencil storage for the host framebuffer.

## Graphics Ownership and Lifetime

Graphics resources follow strict deterministic ownership.

```text
Create:

window / OpenGL context
        ↓
OpenGL API binding
        ↓
renderer / render target


Destroy:

renderer / render target
        ↓
OpenGL API binding
        ↓
OpenGL context
        ↓
window
```

GPU resources must be destroyed while the OpenGL context required to destroy them is still valid and
current.

`OpenGLRenderTarget` owns its framebuffer, color texture, and depth renderbuffer. `OpenGLRenderer`
owns the render target. `OpenGLGraphicsDevice` owns the Silk.NET OpenGL API binding. `DesktopWindow`
owns the native window and native OpenGL context.

`OpenConquer.Client` coordinates the ordering between Rendering and the Platform-owned context
without taking ownership of either subsystem's native resources.

The desktop runtime deliberately owns the lower-level Silk.NET view lifecycle instead of using the
convenience window loop that resets the view before returning. This keeps the native OpenGL context
alive until `OpenConquer.Client` has released the renderer and OpenGL API binding.

Normal graphics shutdown therefore follows:

```text
desktop loop exits
        ↓
Platform requests graphics release
        ↓
Client disposes OpenGLRenderer
        ↓
OpenGLRenderer disposes OpenGLRenderTarget
        ↓
Client disposes OpenGLGraphicsDevice
        ↓
Platform disposes OpenGL context and window
```

Exceptional loop termination follows the same ownership rule: graphics teardown is attempted while
the native context still exists, and the original loop failure remains the primary exception.

Graphics initialization and shutdown paths are exception-safe. Managed input validation occurs
before native resource acquisition, partially initialized GPU resources are released
deterministically, and graphics teardown is attempted before Platform destroys the context.

Platform and Client transition to their terminal disposed state even if underlying native disposal
throws.

## Frame Lifecycle

The current desktop frame flows through the client as follows:

```text
Platform processes native events
        ↓
DesktopFramePacer enforces remaining frame interval
        ↓
Platform processes update callback
        ↓
Platform render callback
        ↓
OpenConquer.Client
        ↓
OpenGLRenderer.RenderFrame
        ↓
OpenGLRenderTarget.BeginFrame
        ↓
bind fixed logical framebuffer
        ↓
establish deterministic frame state
        ↓
clear logical color + depth
        ↓
OpenGLRenderer blits logical color
        ↓
default host framebuffer
        ↓
Platform swaps host buffers
```

Higher-level scene submission has not yet been implemented. The current renderer establishes and
validates the logical render-target and host-composition lifecycle only.

Before the host blit, Rendering disables state that could unintentionally affect the composition
operation, including scissor testing and framebuffer sRGB conversion.

The renderer restores the default framebuffer after host composition, including when composition is
skipped because the host framebuffer has zero area while minimized.

Host framebuffer resizing changes only the destination dimensions. It does not recreate or resize
the logical render target.

## Host Presentation Policy

Desktop-host behavior is explicit rather than inherited accidentally from backend defaults.

The desktop window currently uses:

- a client-supplied 25 ms outer frame interval
- Silk.NET render and update frequency limiters left uncapped because the custom outer loop owns
  cadence
- automatic native buffer swapping
- VSync disabled
- a resizable 1280×720 initial host window
- an OpenGL 3.3 Core forward-compatible context
- no host framebuffer multisampling
- no requested depth buffer on the host framebuffer
- no requested stencil buffer on the host framebuffer

The host framebuffer is explicitly single-sampled. Native multisampling behavior, when implemented,
belongs to the logical rendering path rather than being inherited from a window-system default.

The disabled-VSync and outer frame-cadence policies preserve verified retail 5517 behavior. Detailed
native evidence and compatibility requirements are documented in
[`docs/compatibility/native-graphics.md`](../compatibility/native-graphics.md).

Presentation cadence does not define gameplay simulation timing. Display presentation and game
simulation remain separate concerns.

Rendering produces the completed host framebuffer. Platform owns the native operation that makes
that framebuffer visible by swapping the host buffers.

## Dependency Rules

Dependencies are introduced only when a product or subsystem genuinely requires another boundary's
behavior or ownership.

The current architecture follows these rules:

- `OpenConquer.Launcher` and `OpenConquer.Client` are separate executable product composition roots.
- `OpenConquer.Launcher` owns its own process-failure and diagnostics policy.
- `OpenConquer.Launcher` runs as a standard-user process rather than requiring launcher-wide
  elevation.
- launcher diagnostic persistence is bounded and best-effort rather than a process-availability
  dependency.
- raw launcher exceptions are not persisted directly.
- `OpenConquer.Launcher` does not reference `OpenConquer.Client`.
- `OpenConquer.Launcher` does not reference `OpenConquer.Platform`, `OpenConquer.Rendering`,
  `OpenConquer.Content`, `OpenConquer.Gameplay`, or `OpenConquer.Networking`.
- `OpenConquer.Launcher` does not depend on Silk.NET.
- the launcher publish does not contain the game retail runtime content closure.
- `OpenConquer.Client` is the sole game-runtime composition root.
- `OpenConquer.Client` owns compatibility-derived application policy such as the retail outer frame
  interval.
- `OpenConquer.Platform` implements frame-pacing mechanics without owning Conquer-specific cadence
  values.
- `OpenConquer.Platform` does not reference `OpenConquer.Rendering`.
- `OpenConquer.Rendering` does not reference `OpenConquer.Platform`.
- `OpenConquer.Rendering` does not depend on Silk.NET Windowing, Maths, or Input.
- `OpenConquer.Client` does not directly depend on Silk.NET.
- `OpenConquer.Platform` directly declares the Silk.NET packages whose APIs it consumes.
- `OpenConquer.Gameplay` remains independent of platform, graphics, and transport infrastructure.
- `OpenConquer.Content` remains independent of graphics and gameplay behavior.
- `OpenConquer.Networking` remains independent of platform and rendering concerns.
- offline compatibility tooling does not become a runtime dependency merely because it preserves
  native format behavior.
- historical `Server.dat` support remains outside `src/` and outside the production runtime content
  closure.
- the published game client must contain no `Server.dat`.
- future launcher-to-game authorization crosses an explicit process/security boundary rather than a
  direct project-reference boundary.

A project is an ownership and dependency boundary, not a replacement for a folder.
