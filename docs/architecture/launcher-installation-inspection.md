# Launcher installation inspection

## Implemented boundary

The launcher can now inspect an explicitly selected unpacked game directory. Native folder browsing
and manual absolute-path entry reach the same application operation. The application owns checking,
located, rejected, cancelled, invalid-selection, and unexpected-fault states. Changing the path
invalidates the previous inspection result.

This slice establishes **local product identity and layout evidence**, not trusted release integrity
or launch eligibility. The UI calls its successful result **Game files located**, never Ready, and
has no nonfunctional Play, repair, login, or download controls.

No default install root, registry scan, recursive disk search, path persistence, or release endpoint
is invented. The user selects the existing directory each session. Packaging and preferences will
supply durable discovery inputs once their actual contracts exist.

## Evidence and supported layout

The contract comes from the current rewrite's `OpenConquer.Client.csproj`, emitted .NET 10 build and
publish artifacts, and its packaged content-root default. These define the modern unpacked layout;
this slice changes no native packet, crypto, game configuration, or login behavior.

The selected root must contain:

```text
OpenConquer.Client.dll
OpenConquer.Client.runtimeconfig.json
OpenConquer.Client.deps.json
content/retail-5517/manifest.json
content/retail-5517/payload/
```

The inspector reads the managed PE metadata without loading the assembly. It requires assembly
identity `OpenConquer.Client`, assembly metadata, and a nonzero entry-point field. The displayed
four-part assembly version is diagnostic identity, not a signed release version or update ordering
key. Renaming another executable to `OpenConquer.Client.dll` does not satisfy the identity check.

Both .NET descriptors must be JSON objects with unique property names throughout. The runtime
configuration must declare `runtimeOptions.tfm = net10.0`. The dependency descriptor's active target
must contain exactly one game library with an `OpenConquer.Client.dll` runtime asset. These are
recognition checks, not a reimplementation of the .NET host's complete configuration validator.

The content-set directory, payload directory, and manifest file are checked for filesystem kind and
presence only. The inspector neither parses nor duplicates the offline content manifest contract,
and it does not assert that its entries or payload files are valid. Content/release integrity must
come from the authoritative contract in the next installation slice.

The current unpacked framework-dependent and RID-specific .NET output layout is recognizable.
Single-file, Native AOT, native application-bundle packaging, runtime availability, CPU/OS
compatibility, executable permission, and graphics capability are not established by this probe.
Do not translate Located into runnable or authenticated state later.

## Ownership, limits, and failures

`App` composes an `InstallationInspector` and `InstallationSession` and gives the session to the
window. There are no new runtime packages or game-project references in the launcher.

`InstallationRoot` accepts only normalized absolute paths, independently of the working directory.
The session publishes immutable state snapshots and atomically rejects overlapping inspect/reset
requests. It borrows cancellation from its caller; it owns no background lifetime or global events.
A cancellation already requested at the final checkpoint wins over the returned worker result;
later requests do not retract an accepted result. Unexpected
faults retain their exception identity and escape to the existing fatal host policy.

The inspector runs synchronous metadata probes/parsing off the UI thread and reads files
asynchronously. Assembly input is limited to 16 MiB; each JSON file is limited to 1 MiB and nesting
32. Empty or oversized assembly/descriptor files are rejected before allocating file buffers or
opening streams. Exact-length reads and repeated
length checks detect truncation/growth around reads. JSON documents, PE readers, memory streams,
and file handles have explicit bounded scopes. No game assembly is loaded into a runtime context.

The selected root and each inspected child component reject reparse points/symbolic links; device
attributes and empty file inputs are rejected. OS-resolved ancestors of the selected root are
allowed (for example macOS's standard temporary-directory aliases). These checks are best-effort
observations: they do not create an atomic no-follow filesystem sandbox, immutable file generation,
or exclusive installation lease. A concurrent writer can replace same-sized files or directory
components. No trust, execution, or write authority is granted by this result. Update activation and
launch must establish their own ownership and revalidation boundary.

Cancellation is cooperative. It is checked before reads, passed into asynchronous reads, and
checked before publishing success. Synchronous filesystem calls and bounded parsing cannot always
be interrupted immediately; an unavailable remote filesystem may delay completion. The window
remains responsive and waits for its operation to settle on ordinary close rather than disposing
resources underneath it. Hard process termination cannot promise managed cleanup.

Missing files, malformed metadata, unsupported layouts, links, access denial, and I/O errors become
specific non-sensitive UI outcomes. Expected failures never expose raw exception messages, local
paths, JSON contents, or arbitrary metadata in status text or logs. The user-entered path stays in
the path field/session only. Unexpected faults continue through existing redacted host diagnostics.
There is no new per-file telemetry or credential-bearing state.

The window owns its cancellation source and awaits the read task once. Normal close cancels an
active inspection and is retried only after the owning handler settles and releases resources.
The native picker is also allowed to settle and release its returned folder handles before close.
No async-void operation exists below Avalonia event adapters. Escape cancels a check; Enter submits
it; folder selection moves focus to Check after controls are re-enabled. Labels and a polite live
status are exposed to accessibility, and the form scrolls at small sizes.

## Verification

Pure application tests cover transitions, overlap rejection, cancellation races, retry, input reset,
and original unexpected-exception propagation. Filesystem tests inspect a copy of the actual built
game assembly and assert that the game is not loaded into the test process. The test-only build
reference uses `ReferenceOutputAssembly=false`; it does not alter launcher runtime/publish isolation.
Other cases cover renamed products, malformed/duplicate JSON, unsupported framework metadata,
missing assets/files, damaged PE input, input limits, linked files/directories, cancellation, and
file-handle release.

The native macOS window was inspected at normal and minimum sizes. Manual-path/Enter submission
identified the real game build, nonexistent folders produced actionable feedback, editing cleared
old results, and native picker cancellation preserved the prior result. The final build also confirmed picker
selection/focus, the published game layout, and the OpenConquer native application name. No Avalonia headless stack
was introduced. Windows/Linux native dialog and screen-reader behavior require platform validation.

Launcher `MSBuildTreatWarningsAsErrors` also covers XAML task warnings, which were not promoted by
the existing C# `TreatWarningsAsErrors` setting. The obsolete TextBox watermark API was replaced
with `PlaceholderText` after the build exposed that warning.

Implementation references:

- [Avalonia folder picker](https://docs.avaloniaui.net/api/avalonia/platform/storage/folderpickeropenoptions)
- [Avalonia local path access](https://docs.avaloniaui.net/api/avalonia/platform/storage/storageproviderextensions)
- [MSBuild warning policy](https://learn.microsoft.com/en-us/visualstudio/msbuild/common-msbuild-project-properties)

## Next boundary

Release inventory, manifest authenticity/versioning, integrity verification, update staging/recovery,
and installation ownership must precede Ready. They should build on an audited release contract,
not grow this recognition probe into a second content parser or infer trust from its local files.
Settings persistence, native login, secure game handoff, and final launcher-wide UI remain separate
reviewable slices. The overall launcher is not complete.
