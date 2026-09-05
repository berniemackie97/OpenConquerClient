# Managed launcher installation

## Product boundary

`OpenConquer.Launcher` and `OpenConquer.Client` remain separate executable and publish boundaries,
but they are components of one installed OpenConquer product. The installer or package assembler
owns the product root. A player does not select that root and does not acquire the launcher and game
as unrelated directories.

The managed package layout established by this slice is:

```text
<OpenConquer product root>/
├── OpenConquer.Launcher
├── openconquer.installation.json
└── client/
    ├── OpenConquer.Client
    └── client runtime and content
```

The concrete executable names vary with framework-dependent, self-contained, RID-specific, and
platform-native packaging. The stable boundary is the package context and the managed client
component directory, not a particular .NET publish file set.

`openconquer.installation.json` is a versioned **layout descriptor** created and placed by the
installer/package stage:

```json
{
  "schemaVersion": 1,
  "productId": "OpenConquer",
  "clientRoot": "client"
}
```

It describes ownership and relative component layout only. It is not a release manifest, version
authority, integrity certificate, signature, update source, or launch grant. Those responsibilities
belong to a later trusted release contract.

## Automatic resolution

At startup the launcher uses `AppContext.BaseDirectory` as the package context supplied by the
installed launcher. It reads the descriptor beside the launcher, validates the product identity and
schema, resolves `clientRoot` beneath that package context, and requires the resolved component to
be a real directory. It does not:

- open a folder picker;
- accept a player-entered installation path;
- search parent directories, drives, registries, or arbitrary disks;
- infer release identity from `OpenConquer.Client.dll` metadata;
- parse the current framework-dependent `.deps.json` or runtimeconfig layout;
- follow linked component directories.

Path resolution rejects absolute paths and traversal outside the managed package root. The
descriptor and component directory are read as package-boundary inputs, not as user-selected
content.

When the descriptor is missing or the client component cannot be resolved, the launcher reports an
installation condition such as unavailable, damaged, incomplete, or update-required. It never asks
the player to locate a different game directory. A recovery flow, if product requirements later
justify one, will be a separate explicitly-owned operation and will not redefine normal startup.

## Ownership and lifecycle

`ManagedInstallationResolver` owns descriptor parsing, bounded reads, path containment, and package
component resolution. `LauncherApplication` owns startup evaluation, cancellation, state transitions,
and shutdown draining. `MainWindow` only invokes those operations and renders immutable state; it
does not own the installation state machine, a user path, or a background operation.

The states in this boundary are deliberately narrower than final launch readiness:

```text
Starting
  → EvaluatingInstallation
      → InstallationResolved
      → InstallationUnavailable(issue)
      → Faulted
  → Stopping → Stopped
```

`InstallationResolved` means that the managed product boundary exists. It does not authorize Play.
Trusted release identity, file integrity, update staging, repair, rollback, runtime eligibility,
authentication, and secure game handoff remain separate responsibilities.

## Compatibility and packaging

The launcher must not reference `OpenConquer.Client` or any game runtime subsystem. CI may continue
to publish the two executables independently and verify payload isolation. A release packaging step
composes those independent outputs into the managed product layout above. That composition is the
installer/distribution boundary and must be tested independently from each project publish.

Because the resolver consumes only the installer-owned descriptor and component root, future
framework-dependent, self-contained, RID-specific, and native application-bundle layouts can evolve
inside the client component without making the launcher depend on incidental .NET host files.

`OpenConquer.Product.Tool` is the repository's deterministic composition helper for local and CI
verification. It stages independent launcher and client publish roots into a new managed root and
activates the completed directory without replacing an existing output; it is not a player-facing
installer and does not own release signing, update policy, or rollback.

## Verification

Tests cover successful resolution from the launcher package context, missing and future descriptors,
unknown or traversal paths, missing client components, cancellation, startup state transitions, and
shutdown cancellation. Architecture tests ensure the production window has no manual installation
selection flow and that application composition starts resolution from `AppContext.BaseDirectory`.
