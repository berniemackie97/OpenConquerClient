# Managed launcher installation

The launcher and client publish independently. `OpenConquer.Product.Tool` composes them into one
local/CI product; it is not an installer, updater, or production release-signing authority.

## Package contract

```text
<product root>/
├── launcher publish files
├── openconquer.installation.json
├── openconquer.release.json
├── openconquer.release.sig
└── client/                       # client publish files
```

Composition generates the installation descriptor and stages the authenticated release metadata; the
raw launcher publish must not contain the descriptor, release manifest, release signature, or
`client/`.

```json
{ "schemaVersion": 1, "productId": "OpenConquer", "clientRoot": "client" }
```

Installation schema v1 requires exactly these properties and values. Duplicate/unknown fields,
malformed JSON, nonpositive versions, and arbitrary client paths are rejected. A structurally
recognized higher version reports that a launcher update is required. The descriptor is bounded to
32 KiB.

`openconquer.release.json` identifies the product, monotonically positive release sequence, release
version, minimum compatible launcher version, target runtime, client executable, and the complete
ordered client file set with lengths and SHA-256 hashes. Release metadata is bounded and rejects
duplicate, ambiguous, nonportable, linked, unsupported, or excessive filesystem entries.

`openconquer.release.sig` carries the ECDSA P-256/SHA-256 signature envelope for the exact manifest
bytes. Publisher trust roots are immutable public keys embedded into the launcher package. Private
release-signing keys are never part of the launcher or managed product.

## Resolution and recovery

`ManagedInstallationResolver` starts at `AppContext.BaseDirectory`. It requires a valid descriptor
and a direct, real `client/` directory; linked/reparse descriptors and client directories are
rejected. It never searches the machine or asks the player to choose a folder.

Resolution also requires:

- supported release metadata and launcher-version compatibility;
- configured embedded publisher trust;
- a valid ECDSA P-256 signature from a trusted publisher over the exact manifest bytes;
- a release target runtime matching the current launcher platform;
- the exact manifest-declared client file set;
- matching file lengths and SHA-256 hashes;
- valid, non-linked, non-device, portable package paths; and
- the declared platform-specific client executable.

The client tree is enumerated again after hashing to catch ordinary update/repair races that add,
remove, rename, or link entries during verification. A hostile process executing as the same OS user
is outside this filesystem-integrity trust boundary.

**Resolved means the installed managed client is structurally valid, platform-compatible,
publisher-authenticated, and integrity-verified for the release described by the manifest.**

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
client publish, and launcher input that already owns the descriptor, release metadata, or `client/`.

Release-manifest creation hashes the exact independently published client tree. Release-signature
creation accepts an externally produced signature and public key, verifies that the ECDSA P-256
signature authenticates the exact manifest bytes, and writes the bounded signature envelope.
Production private-key custody remains outside `OpenConquer.Product.Tool`.

Staging requires the release manifest and signature envelope as explicit inputs. It validates their
structure, copies the launcher and client into a temporary sibling directory, copies the release
metadata to the product root, verifies the staged client against the staged manifest, writes the
installation descriptor, preserves Unix executable modes, and activates by rename without replacing
existing output. Failed-stage cleanup is best-effort and preserves the primary error.

The stager does not establish publisher trust. The launcher independently verifies the staged
signature against public trust roots compiled into its package before reporting the installation as
resolved.

## Verification

`ManagedInstallationResolverTests` cover schema/layout rejection, missing release metadata,
unavailable release authority, untrusted signatures, client mutation, and trusted resolution.
`LauncherApplicationTests` cover retry, cancellation ownership, concurrent/reentrant shutdown,
callback failures, and terminal state. `ManagedProductIntegrationTests` exercise real manifest
creation, signing, staging, trusted resolution, untrusted publishers, and recovery without
restarting the application. Product Tool tests cover release metadata, staging, client integrity,
and filesystem identity.

CI generates an ephemeral P-256 publisher solely to exercise the complete release pipeline. It
creates the client manifest, signs it externally, creates the verified signature envelope, embeds
the corresponding public trust root into the CI launcher publish, and composes the managed product.
The ephemeral private key is not packaged or committed. Production release signing must use a
separately controlled production publisher identity.

See [development commands](../development.md#launcher) to stage and run a product.

For a desktop smoke check, use a correctly signed staged product. Temporarily move the installation
descriptor aside, open the launcher, and verify repeated checks remain unavailable. Restore the
descriptor and activate `Check again` with the keyboard; the status must become resolved. Close
through both the in-window button and the native window control on separate runs; each process must
exit successfully.
