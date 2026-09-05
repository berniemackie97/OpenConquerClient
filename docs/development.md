# Development

OpenConquer Client targets the .NET SDK pinned in [`global.json`](../global.json).

## Build

Restore dependencies:

```bash
dotnet restore OpenConquer.Client.slnx --locked-mode
```

Build the complete solution:

```bash
dotnet build OpenConquer.Client.slnx
```

Use a Release build when reproducing CI locally:

```bash
dotnet build OpenConquer.Client.slnx --configuration Release --no-restore
```

## Running

### Game Client

By default, the game client uses the versioned retail content staged beneath the executable at
`content/retail-5517/payload`.

Run it with:

```bash
dotnet run --project src/OpenConquer.Client/OpenConquer.Client.csproj
```

A different authorized client tree can be supplied explicitly:

```bash
dotnet run \
  --project src/OpenConquer.Client/OpenConquer.Client.csproj \
  -- \
  --content-root /path/to/client
```

Relative `--content-root` values are resolved against the working directory from which the process
is launched.

The runtime content root must contain the legacy files required by implemented consumers. The
current consumer-led retail runtime closure is exactly:

```text
data/main/Logo1.bmp
data/main/Logo2.bmp
ini/GameSetUp.ini
ini/info.ini
ini/package.ini
```

`ini/GameSetUp.ini` must contain a valid:

```ini
[ScreenMode]
ScreenModeRecord=<0-3>
```

Startup also examines `ini/package.ini` when present to register WDF package prefixes and reads
`ini/info.ini` to resolve the optional startup logo. Missing package declarations and verified
non-fatal package-registration outcomes do not prevent startup.

Retail `Server.dat` is deliberately not part of the runtime content closure. It is preserved only as
historical compatibility evidence under the content-tool test tree and consumed by offline
`OpenConquer.Content.Tool.Legacy.ServerDat` tooling.

Inspect an explicit historical `Server.dat` file with:

```bash
dotnet run \
  --project tools/OpenConquer.Content.Tool \
  -- \
  inspect-server-dat \
  --file /path/to/Server.dat
```

The inspection command does not provide runtime realm discovery, endpoint configuration, WDF
fallback, or a production server catalog.

The startup logo is non-fatal. When a usable logo bitmap is available, it is presented once in a
dedicated borderless window at its natural logical size. The startup renderer, OpenGL context, and
native window are destroyed before the main resizable client window is constructed. When the logo is
unavailable, no startup window is created. There is no artificial minimum splash duration.

The screen-mode value remains independent of the physical desktop-host size.

`--presentation fit|integer|stretch` controls how that fixed logical frame is presented within the
resizable host framebuffer.

Unknown startup arguments, duplicate options, unsupported presentation values, and missing option
values are rejected rather than ignored.

### Launcher

`OpenConquer.Launcher` is a separate .NET 10 desktop product using Avalonia.

Run it with:

```bash
dotnet run --project src/OpenConquer.Launcher/OpenConquer.Launcher.csproj
```

The current launcher boundary establishes:

- the launcher executable and process composition root;
- explicit installation selection, bounded metadata inspection, and application-owned result state;
- Avalonia application and primary-window lifetime;
- explicit `ShutdownMode.OnMainWindowClose` process policy;
- an independent dependency and publish boundary;
- explicit standard-user process policy on Windows;
- per-user structured diagnostics;
- redacted unhandled-exception diagnostics;
- observation of UI-dispatcher, AppDomain, and unobserved-task exception boundaries;
- deterministic diagnostics and exception-observer teardown;
- nonzero process termination for managed faults escaping the launcher host boundary.

The launcher intentionally does not yet implement:

- account authentication;
- registration or account recovery;
- native session-state ownership;
- launcher-to-game authorization;
- launcher-to-game IPC;
- game-process launching;
- updating or repair;
- realm discovery;
- realm selection;
- supported pre-launch settings.

Those capabilities require their own audited implementation slices rather than placeholder services
or speculative abstractions. Account login must use the original 5517 AccountServer protocol and
native login-to-game semantics. Moving the login UI does not authorize a replacement identity
protocol. See the [launcher roadmap](architecture/launcher-roadmap.md) for the remaining work.

The launcher does not reference the game client's runtime subsystem projects and does not consume
the retail game-content payload.

For installation inspection, use Browse or enter an absolute path to the directory produced by
`dotnet publish src/OpenConquer.Client`. Choose Check folder or press Enter. Escape cancels an active
check. The selection is session-local, and editing it clears the previous result. Closing the window
cancels and drains inspection before host teardown. **Game files located** establishes local
identity/layout only; it does not authorize Play. See the
[inspection contract](architecture/launcher-installation-inspection.md).

The launcher's MSBuild warning policy includes Avalonia/XAML task warnings as errors in addition to
compiler/analyzer warnings. Launcher integration tests build the client as an isolated fixture and
read its metadata without loading game code or adding a launcher runtime dependency.

#### Windows Process Policy

`OpenConquer.Launcher` uses an explicit Windows application manifest.

The launcher requests:

```text
requestedExecutionLevel = asInvoker
uiAccess = false
```

The account-bearing launcher is therefore designed to execute with the privileges of the user who
started it rather than requiring administrative elevation.

Future functionality that genuinely requires elevation must use a separately designed and audited
privilege boundary rather than elevating the launcher process itself.

The manifest also declares Windows 10-or-later compatibility through the documented Windows
compatibility identifier.

Avalonia remains responsible for the launcher's DPI behavior; the launcher manifest does not
override Avalonia with an independent DPI-awareness declaration.

#### Launcher Diagnostics

Launcher diagnostics are local, bounded, structured, and deliberately non-authoritative.

Diagnostic persistence is **best-effort**. Failure to resolve, create, open, write, or dispose the
persistent log sink must not become an application-availability dependency.

The launcher uses platform-native per-user log locations:

```text
Windows
%LOCALAPPDATA%\OpenConquer\Launcher\Logs

macOS
~/Library/Logs/OpenConquer/Launcher

Linux
$XDG_STATE_HOME/OpenConquer/Launcher/Logs
```

When Linux does not provide a usable absolute `XDG_STATE_HOME`, the launcher uses:

```text
~/.local/state/OpenConquer/Launcher/Logs
```

If no supported per-user diagnostic location can be resolved, or if the expected storage boundary
cannot be established because of an I/O, access, or storage-path format failure, diagnostics degrade
to a no-sink logger rather than preventing launcher startup.

Programming and configuration defects are not intentionally converted into persistence fallback.
Only expected operational storage failures are treated as unavailable diagnostics; path-format
exceptions are caught around directory creation, not logger configuration.

Persistent launcher events are newline-delimited JSON written to:

```text
launcher-*.jsonl
```

The file sink uses:

- daily rolling;
- a 5 MiB per-file rolling threshold (the final event may cross the threshold);
- rolling when the current file reaches that limit;
- retention of at most 14 log files;
- unbuffered writes;
- exclusive process ownership of the file sink.

The sink is intentionally bounded so ordinary launcher diagnostics cannot grow without limit.

Launcher lifecycle records currently include:

- host startup;
- host shutdown and exit code;
- redacted exception diagnostics.

The launcher does **not** pass raw `Exception` objects to Serilog.

Unhandled exceptions are first projected into a deliberately restricted representation containing:

- exception type;
- HRESULT;
- bounded declaring-type/method stack names without signatures or source-file information;
- bounded inner-exception structure;
- separate flags for truncated type names, stack text, and inner-exception traversal.

The projection deliberately excludes:

- `Exception.Message`;
- `Exception.Data`;
- `Exception.Source`;
- `Exception.TargetSite`;
- source-file paths;
- arbitrary application parameters;
- request URLs;
- authorization headers;
- cookies;
- account passwords;
- authorization codes;
- access tokens;
- refresh tokens;
- session tokens.

This establishes the default security posture before account authentication or network-facing
launcher services exist.

Projection visits at most 16 exception objects and eight nested levels. Each exception has at most
512 UTF-16 code units of type name and 32 stack frames / 4,096 code units of stack text. Formatting
stops at the budget instead of allocating an entire formatted trace first; surrogate pairs are not
split. Runtime stack capture and reflection still allocate according to the captured stack and
metadata; the projection does not claim to bound those CLR allocations or recover from resource
exhaustion. Remote stack text and virtual exception text are never used.

#### Launcher Fatal-Fault Policy

The launcher observes the relevant unmanaged-host boundaries without treating unknown failures as
recoverable application state.

The process-level observer covers:

```text
Avalonia Dispatcher.UnhandledException
AppDomain.CurrentDomain.UnhandledException
TaskScheduler.UnobservedTaskException
```

An unknown unhandled Avalonia UI exception is not marked handled. The dispatcher callback remains
lightweight, records the exception reference for classification, and allows the fault to escape to
the top-level launcher boundary.

The top-level process boundary records the resulting redacted fatal diagnostic and returns a nonzero
launcher exit code.

AppDomain unhandled exceptions are recorded with the runtime-provided terminating state.

Unobserved task exceptions are recorded as non-terminating diagnostics and marked observed after the
diagnostic attempt.

Failure of diagnostics while an unhandled exception is already being processed is itself best-effort
and must not replace or obscure the original process failure.

`Program.Main` remains the explicit process composition root. Launcher diagnostics and exception
observation own their detailed mechanisms rather than expanding `Program` into a logging,
filesystem, or exception-handling framework.

## Formatting

Check formatting without modifying files:

```bash
dotnet format OpenConquer.Client.slnx --verify-no-changes --no-restore
```

Apply formatting:

```bash
dotnet format OpenConquer.Client.slnx
```

## Tests

Test projects are added when a subsystem has behavior with meaningful invariants to verify. Empty
test-project scaffolding is not retained.

The current test projects are:

```text
tests/OpenConquer.Client.Tests
tests/OpenConquer.Content.Tests
tests/OpenConquer.Content.Tool.Tests
tests/OpenConquer.Launcher.Tests
tests/OpenConquer.Platform.Tests
tests/OpenConquer.Rendering.Tests
```

`OpenConquer.Client.Tests` verifies executable startup policy, including content-root defaults,
explicit overrides, path normalization, malformed argument handling, startup-presentation policy,
and startup-window-before-main-window lifetime invariants.

`OpenConquer.Content.Tests` verifies runtime content-root lookup semantics, package registration and
lookup behavior, shared content-path validation, native-compatible INI parsing, startup-logo
configuration and decoding, content-closure resolution, WDF archive behavior, and filesystem-safety
invariants.

`OpenConquer.Content.Tool.Tests` verifies deterministic content import, manifest construction,
physical-payload verification, implemented-closure enforcement, integrity validation,
filesystem-safety behavior, and the offline legacy `Server.dat` compatibility boundary. The latter
includes the verified retail RSA key, hardened RSA/PKCS#1/gzip envelope validation, typed
`outenserver` projection, exact retail-fixture parity, and deterministic inspection reporting.

`OpenConquer.Launcher.Tests` verifies launcher host and product-boundary invariants without
requiring a native desktop session.

The launcher tests cover:

- dedicated launcher assembly identity;
- absence of game-runtime subsystem and Silk.NET dependencies;
- project-to-manifest linkage;
- Windows `asInvoker` and `uiAccess=false` process policy;
- Windows compatibility-manifest policy;
- platform-native diagnostic path selection;
- invalid and unavailable diagnostic path behavior;
- Linux XDG state-directory behavior and fallback;
- diagnostic directory and JSONL lifecycle output;
- bounded persistence fallback;
- diagnostic disposal semantics;
- exception redaction;
- bounded aggregate and nested exception projection;
- exception-observer lifecycle;
- UI-dispatcher exception classification.

Avalonia's headless UI test harness is not currently part of the repository test infrastructure.
Launcher UI behavior that eventually requires process-level desktop verification should use a
reliable dedicated end-to-end test boundary rather than weakening the repository's existing xUnit
baseline or introducing a flaky test host.

`OpenConquer.Platform.Tests` verifies desktop frame-pacing mechanics and startup-window policy
independently of a live native window. The tests cover interval validation, start-state enforcement,
remaining-time waits, overruns, scheduler oversleep, no-catch-up behavior, and startup-window
configuration.

`OpenConquer.Rendering.Tests` verifies logical presentation transforms, OpenGL capability policy,
render-target behavior, and other rendering invariants that do not require a live desktop window.

Run the complete test suite while validating a branch:

```bash
dotnet test OpenConquer.Client.slnx --configuration Release --no-build --no-restore
```

Every test project is part of `OpenConquer.Client.slnx`, and CI executes the complete solution test
suite.

A test project must contain real tests. A zero-test test assembly is not retained as placeholder
scaffolding.

## Continuous Integration

GitHub Actions runs CI for pushes to `main`, pull requests, and manual workflow dispatches.

The Linux quality job runs on Ubuntu 24.04 and performs:

1. locked dependency restore;
2. formatting verification;
3. Release build;
4. the complete solution test suite;
5. verification that the implemented runtime content closure, manifest, and physical payload agree;
6. framework-dependent publication of `OpenConquer.Client`;
7. verification that the published client contains the exact five-file runtime content set;
8. explicit rejection of any published `Server.dat`;
9. framework-dependent publication of `OpenConquer.Launcher`;
10. verification that the launcher publish does not contain the game's retail runtime content set.

Publishing is treated as a separate product boundary from compilation. A project that builds
successfully but cannot produce its expected publish layout does not satisfy the repository quality
gate.

The published client content check verifies:

```text
ClientContentClosure
        ==
tracked manifest
        ==
tracked payload
        ==
published client content set
```

An additional explicit `Server.dat` search protects the wider publish directory so a future
project-file change cannot accidentally stage that historical file outside the verified content-set
subtree.

The launcher publish is independently checked to prevent the game's retail content payload from
becoming an implicit launcher dependency merely because both executables live in the same
repository.

The Release build runs with the repository's analyzers and warnings-as-errors configuration.

Additional Windows 2025 and macOS 15 jobs restore, build, and test the complete solution in Release
configuration so cross-platform compilation and test behavior remain continuously verified.

Runtime-identifier-specific packaging, installers, signing, notarization, self-contained deployment,
and platform-native application bundles are intentionally outside the current CI publish check. They
belong to a future audited packaging and release-engineering slice.

CI obtains the .NET SDK selection from `global.json` rather than duplicating the SDK version in the
workflow.

GitHub Actions dependencies are pinned to immutable commit SHAs. Dependabot checks GitHub Actions
weekly so pinned actions can be reviewed and advanced deliberately when new releases are available.

## Dependency Review

Pull requests targeting `main` run a dependency-review workflow. The workflow fails when a
dependency change introduces a package with a known vulnerability according to GitHub's dependency
review.

NuGet dependencies are also checked weekly by Dependabot.

Package versions are centrally managed. Runtime framework families such as Silk.NET and Avalonia
should be advanced deliberately and audited as coherent dependency groups rather than changed
independently without reviewing their transitive closure.

GitHub dependency graph and automatic dependency submission are enabled for the repository. For
.NET, automatic dependency submission resolves build-time and transitive dependencies and submits
them to the dependency graph, giving dependency review and Dependabot more complete dependency data
than static manifest analysis alone.

## Code Scanning

CodeQL uses GitHub default setup so the scanning configuration follows GitHub's supported C#
defaults without adding a hand-maintained CodeQL workflow to the repository.

Code scanning should become a required repository rule only after its baseline has been verified
healthy and stable enough to act as a merge gate.

## Repository Protection

`main` is protected by the repository's active `Main protection` ruleset.

Changes to `main` require a pull request and the repository's configured required status checks.

Required checks use strict branch-update enforcement so the candidate branch must be current with
`main` before merge.

The ruleset also:

- requires linear history;
- restricts deletion of `main`;
- blocks force pushes;
- has no bypass actors.

Repository policy is configured in GitHub rather than duplicated as source-controlled workflow
logic.

Because linear history is required, production work should normally be integrated using squash or
rebase semantics rather than merge commits.

## Packages

NuGet versions are managed centrally in:

```text
Directory.Packages.props
```

Project files should reference packages without specifying their version:

```xml
<PackageReference Include="Silk.NET.OpenGL" />
```

The game runtime currently uses Silk.NET for its platform and OpenGL boundaries.

The launcher currently uses:

```text
Avalonia
Avalonia.Desktop
Avalonia.Themes.Fluent
Serilog
Serilog.Sinks.File
```

The launcher intentionally does not currently depend on an MVVM framework, authentication library,
HTTP client package, updater framework, embedded browser package, Generic Host integration, or game
runtime subsystem.

Serilog is currently used directly at the launcher host boundary rather than through
`Microsoft.Extensions.Logging`. The launcher does not yet have a Generic Host or dependency-
injection composition model that would justify introducing another logging abstraction and adapter
layer.

## Project Settings

Repository-wide compiler and analyzer settings live in:

```text
Directory.Build.props
.editorconfig
```

Repository checkout line-ending normalization is defined by:

```text
.gitattributes
```

Text files use LF line endings across development platforms, while Windows command scripts retain
CRLF line endings.

## Before Committing

Run the repository quality gate from a clean working tree or a deliberate review state:

```bash
dotnet restore OpenConquer.Client.slnx --locked-mode

dotnet format OpenConquer.Client.slnx \
  --verify-no-changes \
  --no-restore

dotnet build OpenConquer.Client.slnx \
  --configuration Release \
  --no-restore

dotnet test OpenConquer.Client.slnx \
  --configuration Release \
  --no-build \
  --no-restore

dotnet run \
  --project tools/OpenConquer.Content.Tool \
  --configuration Release \
  --no-build \
  --no-restore \
  -- verify-content-set \
  --content-set content/retail-5517

rm -rf \
  /tmp/openconquer-client-publish \
  /tmp/openconquer-launcher-publish

dotnet publish \
  src/OpenConquer.Client/OpenConquer.Client.csproj \
  --configuration Release \
  --no-restore \
  --output /tmp/openconquer-client-publish

dotnet run \
  --project tools/OpenConquer.Content.Tool \
  --configuration Release \
  --no-build \
  --no-restore \
  -- verify-content-set \
  --content-set /tmp/openconquer-client-publish/content/retail-5517

find \
  /tmp/openconquer-client-publish \
  -type f \
  -iname 'Server.dat' \
  -print

dotnet publish \
  src/OpenConquer.Launcher/OpenConquer.Launcher.csproj \
  --configuration Release \
  --no-restore \
  --output /tmp/openconquer-launcher-publish

find \
  /tmp/openconquer-launcher-publish \
  -path '*/content/retail-5517*' \
  -print

git diff --check
```

The two final `find` commands must print nothing.
