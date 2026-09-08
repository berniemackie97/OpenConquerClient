# Managed launcher installation

The launcher and client publish independently. `OpenConquer.Product.Tool` composes them into one
authenticated local/CI product; it is not an installer, updater, or production release-signing
authority.

## Package contract

```text
<product root>/
├── launcher publish files
├── openconquer.installation.json
└── releases/
    └── <release-id>/
        ├── openconquer.release.json
        ├── openconquer.release.sig
        └── client/                       # client publish files
```

Composition generates the installation descriptor and stages the authenticated release metadata. The
raw launcher publish must not contain the descriptor, `releases/`, release manifest, release
signature, mutable release trust, a legacy root-level managed `client/`, or game runtime content.

```json
{
  "schemaVersion": 2,
  "productId": "OpenConquer",
  "activeRelease": "00000000000000000042-<manifest-sha256>",
  "fallbackRelease": null
}
```

Installation schema v2 requires exactly these properties. The active release is mandatory; the
fallback is either null or a distinct release identity. A release identity contains a zero-padded
20-digit positive sequence, the lowercase SHA-256 digest of the exact signed manifest bytes, and,
for same-release repair generations, an optional 32-digit lowercase nonce. This makes every selected
directory name independently checkable without granting the descriptor release authority.

Duplicate/unknown fields, malformed JSON, nonpositive or nonportable identities, equal active and
fallback identities, and arbitrary paths are rejected. A structurally recognized higher version
reports that a launcher update is required. The descriptor is bounded to 32 KiB.

Schema v1 remains a read and one-way migration contract for existing flat products:

```json
{ "schemaVersion": 1, "productId": "OpenConquer", "clientRoot": "client" }
```

New composition never emits schema v1. A successful update migrates a healthy legacy client into an
immutable generation before selecting the new generation. The untouched flat files remain inert;
removing them is a packaging/lifecycle decision, not part of activation.

`openconquer.release.json` identifies the product, monotonically positive release sequence, release
version, minimum compatible launcher version, target runtime, client executable, and the complete
ordered client file set with lengths and SHA-256 hashes. Release metadata is bounded and rejects
duplicate, ambiguous, nonportable, linked, unsupported, or excessive filesystem entries.

`openconquer.release.sig` carries the ECDSA P-256/SHA-256 signature envelope for the exact manifest
bytes. Publisher trust roots are immutable public keys embedded into the launcher package. Private
release-signing keys are never part of the launcher or managed product.

## Resolution and recovery

`ManagedInstallationResolver` starts at `AppContext.BaseDirectory`. It requires a valid descriptor,
a direct `releases/` root, an exact selected-generation root, and a direct `client/` directory;
linked/reparse paths are rejected. The selected generation may contain only its manifest, signature,
and client directory. It never searches the machine or asks the player to choose a folder.

Resolution also requires:

- supported release metadata and launcher-version compatibility;
- configured embedded publisher trust;
- a valid ECDSA P-256 signature from a trusted publisher over the exact manifest bytes;
- a release target runtime matching the current launcher platform;
- a generation identity matching the signed manifest sequence and exact manifest digest;
- the exact manifest-declared client file set;
- matching file lengths and SHA-256 hashes;
- valid, non-linked, non-device, portable package paths; and
- the declared platform-specific client executable; and
- portable executable permissions for that entry point on Unix.

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
structure, snapshots their exact bytes, derives the release identity, copies the launcher and client
into a temporary sibling directory, copies release metadata into the immutable generation, rejects
metadata races, verifies the staged client against the staged manifest, writes the schema-v2
descriptor, sanitizes copied Unix file modes while preserving executable intent, forces the declared
Unix client entry point to portable `0755`, and activates the staging root by rename without
replacing an existing output.

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

## Installed client update, repair, and rollback transaction

`ManagedReleaseTransaction` owns the narrow mutation boundary after a candidate release has been
acquired. A candidate is one release root containing exactly the signed manifest, signature envelope,
and client directory. The transaction does not discover releases, download them, choose a channel,
update the running launcher, elevate privileges, or define an account/game protocol.

Before changing installed state, the transaction:

1. fully authenticates and hashes the candidate against embedded publisher trust;
2. acquires the product-root update lock, serializing update, repair, and rollback across processes;
3. authenticates current signed metadata to retain an anti-rollback floor even when current client
   payload files are damaged;
4. rejects update sequences that do not strictly advance and repair candidates that are not the
   exact same signed release;
5. copies only manifest-declared files into a private staging generation with bounded traversal,
   no links/devices, create-new destinations, durable file flushes, and safe Unix modes; and
6. authenticates and hashes the copied generation again.

Only after those checks does it rename the complete staging generation into `releases/` and replace
the descriptor. The descriptor replacement is the commit point and is one same-directory atomic
rename. Cancellation is honored through the last pre-commit check; after the synchronous commit
there is no cancellable work whose failure could falsely report an unchanged installation.

An update records the former active generation as fallback only if it still verifies completely. If
the active payload is damaged, an already-recorded healthy fallback is retained; otherwise the new
release is selected without a fallback. Repair always writes a new nonce-bearing generation rather
than modifying the selected directory in place. Explicit rollback fully verifies the fallback before
selecting it and retains the displaced active generation as the next fallback only when it remains
healthy.

Expected failures are classified without exposing paths or exception text: unavailable current
authority, rejected candidate integrity, non-advancing update, blocked downgrade, wrong repair
release, unavailable fallback, concurrent maintenance, access denial, linked paths, and filesystem
failure. Cancellation remains cancellation rather than being flattened into a repair error.

On supported local filesystems that provide atomic same-directory rename, process-crash behavior is
deterministic:

- before descriptor replacement, the previous active release remains selected;
- after descriptor replacement, the completely copied and reverified generation is selected;
- incomplete staging directories and completed unreferenced generations are inert because no
  descriptor names them; and
- selected active/fallback generations are never deleted by this transaction.

The implementation durably flushes copied files and the temporary descriptor before activation.
.NET does not expose a portable parent-directory flush, so the launcher does not claim stronger
sudden-power-loss guarantees than the host filesystem provides.

Automatic reclamation is intentionally absent until controlled client-process lifetime can prove a
generation is no longer executing, especially on Windows. Authoritative acquisition, recovery UI,
launcher self-update, and signed platform deployment remain later launcher-owned slices.

## Verification

`ManagedInstallationResolverTests` cover schema/layout rejection, missing release metadata,
unavailable release authority, untrusted signatures, client mutation, and trusted resolution.

`ManagedReleaseTransactionTests` cover trusted update, same-release repair, damaged-client recovery,
legacy migration, verified rollback, corrupt fallback rejection, sequence policy, release-identity
binding, cancellation, lock contention, linked and unexpected candidate entries, selected-generation
shape, and Unix executable permissions.

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
