# Managed launcher installation

The launcher and client publish independently. `OpenConquer.Product.Tool` composes them into one
authenticated local/CI product; it is not an installer, updater, or production release-signing
authority.

## Package contract

```text
<product root>/
├── launcher publish files
├── openconquer.installation.json
├── openconquer.release.json
├── openconquer.release.sig
└── client/                       # client publish files
```

Composition generates the installation descriptor and stages the authenticated release metadata. The
raw launcher publish must not contain the descriptor, release manifest, release signature, mutable
release trust, managed `client/`, or game runtime content.

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

The Product Tool rejects overlapping roots, filesystem aliases, linked roots/entries, an empty
client publish, and launcher input that already owns the descriptor, release metadata, or `client/`.

Release-manifest creation hashes the exact independently published client tree.

Release-signature creation accepts an externally produced signature and public key, verifies that
the ECDSA P-256/SHA-256 signature authenticates the exact manifest bytes, and writes the bounded
signature envelope.

Release-trust creation accepts validated public publisher keys and creates the trust document used
by the launcher's embedded-resource build boundary.

Production private-key custody remains outside `OpenConquer.Product.Tool`.

Staging requires the release manifest and signature envelope as explicit inputs. It validates their
structure, copies the launcher and client into a temporary sibling directory, copies the release
metadata to the product root, verifies the staged client against the staged manifest, writes the
installation descriptor, preserves Unix executable modes, and activates the staging root by rename
without replacing an existing output.

Failed-stage cleanup is best-effort and preserves the primary error.

The stager does not establish publisher trust. The launcher independently verifies the staged
signature against public trust roots embedded into its package before reporting the installation as
resolved.

## Local development composition

`create-local-product` is the canonical development orchestration for exercising the complete
managed-product boundary without turning the Product Tool into a production signing authority.

The command intentionally accepts no configuration options.

Local composition uses:

- a persistent user-scoped development-only ECDSA P-256 publisher;
- PKCS#8 private-key storage outside the repository;
- SHA-256 with RFC3279 DER ECDSA signatures;
- user-scoped build serialization;
- durable monotonically increasing development release sequences;
- active and previous product manifests as an anti-rollback sequence floor;
- isolated repository-local per-run workspaces;
- a canonical active local product;
- a previous-product rollback slot; and
- rollback-safe rename-based activation.

The repository-local layout is:

```text
artifacts/local-product/
├── work/
│   └── <run-id>/
│       ├── client-publish/
│       ├── launcher-publish/
│       ├── release/
│       └── product/
├── product/
└── product.previous/
```

The workspace owns all intermediate publication, public-key, raw-signature, trust, release-metadata,
and staged-candidate files.

Workspace cleanup is best-effort. Cleanup failure does not replace the primary composition result.

The development publisher and sequence/lock state live outside the repository in the current user's
platform configuration area. On Unix, the state directory and sensitive files are restricted to the
current user.

The user-scoped build lock is shared across repository clones using the same development publisher
state. This prevents concurrent local builds from racing publisher creation or release-sequence
allocation.

The release sequence is durably reserved and never intentionally reused. A failed build may
therefore leave a sequence gap after release identity has been allocated.

If persistent sequence state is missing, the greatest sequence represented by the active or previous
local product becomes the minimum exclusive floor for the next reservation.

Client publication, source/published-content verification, and `Server.dat` rejection occur before
sequence reservation. Early failures therefore do not consume release identity.

After sequence reservation, local composition:

1. creates the release manifest for the exact client publish;
2. signs the exact bounded manifest bytes with the persistent development publisher;
3. creates the validated release-signature envelope;
4. creates launcher trust from the development public key;
5. publishes the launcher with that trust embedded;
6. verifies raw-launcher isolation;
7. stages the managed-product candidate;
8. verifies the candidate contains no loose development/signing state; and
9. passes the candidate to local activation.

The final managed product must not contain:

- the development private key;
- the development public-key file;
- the raw development signature;
- loose `release-trust.json`;
- release-sequence state;
- sequence locks;
- build locks; or
- activation locks.

Retail `Server.dat` is also forbidden anywhere in the published managed client.

## Local activation

Local activation validates a completed candidate before mutating the active product.

The activation boundary verifies the candidate installation descriptor, release manifest,
signature-envelope structure, and client integrity. Cryptographic publisher authorization remains
the launcher's independent responsibility when the product is executed.

Activation:

1. validates canonical local-product, workspace, and candidate path relationships;
2. acquires the repository-local activation lock;
3. rejects a candidate whose release sequence does not advance beyond the active product;
4. removes the older `product.previous` only after the new candidate has already passed validation;
5. moves the current active product to `product.previous`;
6. moves the candidate into the canonical active `product`; and
7. attempts to restore the previous product if final promotion fails.

A failed candidate before activation leaves the current active product untouched.

`product.previous` is a local-development rollback/recovery slot. It is not an implementation of
player-facing update or repair.

## Verification

`ManagedInstallationResolverTests` cover schema/layout rejection, missing release metadata,
unavailable release authority, untrusted signatures, client mutation, and trusted resolution.

`LauncherApplicationTests` cover retry, cancellation ownership, concurrent/reentrant shutdown,
callback failures, and terminal state.

`ManagedProductIntegrationTests` exercise real manifest creation, signing, staging, trusted
resolution, untrusted publishers, and recovery without restarting the application.

Product Tool tests cover:

- release metadata;
- release-signature envelopes;
- publisher trust;
- persistent development publisher identity;
- development-state permissions;
- user-scoped build serialization;
- monotonic release sequencing and anti-rollback recovery;
- repository and runtime discovery;
- local-product paths;
- publish/product artifact isolation;
- local-product orchestration;
- rollback-safe local activation; and
- managed-product staging.

CI generates an ephemeral P-256 publisher solely to exercise the production-capable release
composition primitives. It creates the client manifest, creates release trust through the Product
Tool, signs the manifest externally, creates the verified signature envelope, embeds the
corresponding public trust root into the CI launcher publish, and composes the managed product.

The CI publisher is unrelated to the persistent local-development publisher. Production release
signing must use a separately controlled production publisher identity.

For native desktop verification, run the canonical local managed launcher from
`artifacts/local-product/product`. A valid local product must resolve to the ready state.

When changing release integrity, product composition, or installation resolution, copy the managed
product to an isolated temporary root, alter an authenticated client file, and run the copied
launcher. The modified installation must fail closed while the canonical active product remains
unchanged.

See [development commands](../development.md#launcher) for the exact workflow.
