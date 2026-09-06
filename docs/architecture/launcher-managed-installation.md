# Managed launcher installation

The launcher and client publish independently. `OpenConquer.Product.Tool` composes them into one
local/CI product; it is not an installer, updater, or release-signing authority.

## Package contract

```text
<product root>/
├── launcher publish files
├── openconquer.installation.json
└── client/                       # client publish files
```

Composition generates the descriptor; the raw launcher publish must not contain it or `client/`.

```json
{"schemaVersion": 1, "productId": "OpenConquer", "clientRoot": "client"}
```

Schema v1 requires exactly these properties and values. Duplicate/unknown fields, malformed JSON,
nonpositive versions, and arbitrary client paths are rejected. A structurally recognized higher
version reports that a launcher update is required. The descriptor is bounded to 32 KiB.

## Resolution and recovery

`ManagedInstallationResolver` starts at `AppContext.BaseDirectory`. It requires a valid descriptor
and a direct, real `client/` directory; linked/reparse descriptors and client directories are rejected.
It never searches the machine or asks the player to choose a folder.

**Resolved means layout found.** It does not verify executable files, release hashes, signatures,
version compatibility, or authorization to launch.

`LauncherApplication` owns the lifecycle:

```text
Starting → EvaluatingInstallation → InstallationResolved
                                ↘ InstallationUnavailable → Check again → EvaluatingInstallation
                                ↘ Faulted
Any active state → Stopping → Stopped
```

- Checks run off the UI thread. Only one can be active; overlapping retry requests share its task.
- `Check again` is available after an expected installation failure. It rechecks the same package
  root after files or permissions are restored; it does not claim to repair anything.
- Unexpected failures remain fatal and reach the existing redacted host-diagnostics boundary.
- Shutdown immediately owns state and signals cancellation. Every caller shares one task that drains
  cancellation callbacks and the check before disposing the token source and publishing `Stopped`.
- Callback failures cannot skip cleanup. Unrelated cancellation and unexpected failures remain
  observable to the host. Resolver implementations must honor lifetime cancellation.
- The fixed launcher window exposes keyboard-focusable `Check again` and `Close` buttons. Status
  changes use polite accessibility live regions. Closing drains work and closes the window.

## Composition guarantees

The product tool rejects overlapping roots, filesystem aliases, linked roots/entries, an empty
client publish, and launcher input that already owns the descriptor or `client/`. It stages in a
temporary sibling directory, preserves Unix executable modes, and activates by rename without
replacing existing output. Failed-stage cleanup is best-effort and preserves the primary error.

## Verification

`ManagedInstallationResolverTests` cover schema and layout rejection. `LauncherApplicationTests`
cover retry, cancellation ownership, concurrent/reentrant shutdown, callback failures, and terminal
state. `ManagedProductIntegrationTests` exercise the real stager/resolver, including recovery without
restarting the application. Product Tool tests cover staging and filesystem identity.

CI checks raw-publish isolation and managed-product composition. See
[development commands](../development.md#launcher) to stage and run a product.

For a desktop smoke check, temporarily move the descriptor aside in a disposable staged product,
open the launcher, and verify repeated checks remain unavailable. Restore the descriptor and activate
`Check again` with the keyboard; the status must become resolved. Close through both the in-window
button and the native window control on separate runs; each process must exit successfully.
