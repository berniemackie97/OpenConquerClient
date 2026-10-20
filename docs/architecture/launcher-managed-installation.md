# Managed launcher installation

The launcher and client are published separately. `OpenConquer.Product.Tool` combines them into one
authenticated product for local development and CI. It is not an installer, an updater, or a
production release-signing system.

## Package layout

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

Composition generates the installation descriptor and stages the authenticated release metadata.
The raw launcher publish must not contain the descriptor, `releases/`, a release manifest, a release
signature, mutable release trust, a legacy root-level managed `client/`, or game runtime content.

```json
{
    "schemaVersion": 2,
    "productId": "OpenConquer",
    "activeRelease": "00000000000000000042-<manifest-sha256>",
    "fallbackRelease": null
}
```

Installation schema v2 requires these properties and no others. The active release is required. The
fallback is either null or a different release identity. A release identity is made of a
zero-padded 20-digit positive sequence number, the lowercase SHA-256 digest of the signed manifest
bytes, and an optional 32-digit lowercase nonce. The nonce is used for same-release repair and keeps
a new generation from colliding with an existing or corrupted one. As a result, every selected
directory name can be checked on its own, without trusting the descriptor to say which release is
valid.

The descriptor is rejected if it has duplicate or unknown fields, malformed JSON, nonpositive or
nonportable identities, identical active and fallback identities, or arbitrary paths. A descriptor
with a recognizable structure but a higher schema version reports that a launcher update is
required. The descriptor may be at most 32 KiB.

Schema v1 is still read, so existing flat products can be migrated one way to v2:

```json
{ "schemaVersion": 1, "productId": "OpenConquer", "clientRoot": "client" }
```

New composition never writes schema v1. A successful update copies a healthy legacy client into a
versioned generation and selects that generation, without changing the legacy client in place
first. The old flat files are left unused. Removing them is a packaging or lifecycle decision and is
not part of activation.

`openconquer.release.json` records the product, the positive release sequence (which only
increases), the release version, the minimum compatible launcher version, the target runtime, the
client executable, and the complete ordered list of client files with their lengths and SHA-256
hashes. Release metadata has a size limit, and duplicate, ambiguous, nonportable, linked,
unsupported, or excessive filesystem entries are rejected.

`openconquer.release.sig` holds the ECDSA P-256/SHA-256 signature envelope over the manifest bytes
as stored. Publisher trust roots are fixed public keys embedded in the launcher package. Private
release-signing keys are never part of the launcher or the managed product.

## Resolution and recovery

`ManagedInstallationResolver` starts at `AppContext.BaseDirectory`. It requires a valid descriptor,
a `releases/` directory directly under the product root, a generation directory whose name matches
the selected identity, and a `client/` directory directly inside it. Linked and reparse-point paths
are rejected. The selected generation may contain only its manifest, signature, and client
directory. The resolver never searches the machine and never asks the player to choose a folder.

Resolution also requires:

- supported release metadata and a compatible launcher version
- configured embedded publisher trust
- a valid ECDSA P-256 signature from a trusted publisher over the manifest bytes
- a release target runtime that matches the current launcher platform
- a generation identity that matches the signed manifest's sequence and the manifest digest
- the client file set declared in the manifest, with no files missing or added
- matching file lengths and SHA-256 hashes
- valid, portable package paths that are neither links nor devices
- the declared platform-specific client executable
- portable executable permissions for that entry point on Unix

After hashing, the resolver lists the client tree again. This catches ordinary update or repair
races that add, remove, rename, or link entries during verification. It does not protect against a
hostile process running as the same OS user.

An installation counts as resolved when the managed client is structurally valid, compatible with
the platform, signed by a trusted publisher, and passes the integrity check for the release the
manifest describes.

`LauncherApplication` owns the lifecycle:

```text
Starting → EvaluatingInstallation → InstallationResolved
                                ↘ InstallationUnavailable → Check again → EvaluatingInstallation
                                ↘ Faulted
Any active state → Stopping → Stopped
```

- Checks run off the UI thread. Only one runs at a time, and retry requests made during a check
  share its task.
- `Check again` is offered after an expected installation failure. It checks the same package root
  again, for example after files or permissions have been restored. It does not repair anything.
- Unexpected failures are still fatal and go to the existing redacted host diagnostics.
- Shutdown takes over the state at once and signals cancellation. All callers share one task. That
  task waits for the cancellation callbacks and the running check to finish, then disposes the
  token source and publishes `Stopped`.
- A failing callback cannot skip cleanup. Unrelated cancellation and unexpected failures stay
  visible to the host. Resolver implementations must honor lifetime cancellation.
- The fixed-size launcher window has keyboard-focusable `Check again` and `Close` buttons. Status
  changes are announced through polite accessibility live regions. Closing waits for pending work to
  drain and then closes the window.

## Composition checks

The Product Tool rejects overlapping roots, filesystem aliases, linked roots or entries, an empty
client publish, and launcher input that already contains the descriptor, release metadata, or
`client/`.

Release-manifest creation hashes the separately published client tree as it is.

Release-signature creation takes a signature and public key produced outside the tool, checks that
the ECDSA P-256/SHA-256 signature is valid for the manifest bytes, and writes the size-limited
signature envelope.

Release-trust creation takes validated public publisher keys and creates the trust document that
the launcher build embeds as a resource.

Production private keys are kept outside `OpenConquer.Product.Tool`.

Staging requires the release manifest and signature envelope as inputs. It validates their
structure, takes a snapshot of their bytes, and derives the release identity. It copies the
launcher and client into a temporary sibling directory and the release metadata into the versioned
generation, and rejects the metadata if it changes during the copy. It then checks the staged
client against the staged manifest and writes the schema-v2 descriptor. Copied Unix file modes are
sanitized but keep their executable bits where intended, and the declared Unix client entry point is
set to the portable mode `0755`. Finally, the staging root is activated by a rename that never
replaces an existing output.

If staging fails, cleanup is best-effort and the original error is reported.

Staging does not decide whether the publisher is trusted. Before reporting the installation as
resolved, the launcher checks the staged signature itself against the public trust roots embedded
in its package.

## Release discovery and acquisition

The release acquisition code reads two configured HTTPS resources:

```text
openconquer.catalog.json
openconquer.catalog.sig
```

The detached catalog signature is checked against the same embedded ECDSA P-256 publisher trust as
installed release manifests. The catalog names the product `OpenConquer`, carries a UTC expiry
time, and lists at most 128 releases. The launcher rejects catalogs that have expired or that expire
more than 31 days in the future. This limits how long a frozen cache or origin can keep serving an
old catalog. Each entry ties together a positive release sequence, a display version, the minimum
launcher version, the supported runtime, an absolute HTTPS package URI, a positive compressed
length, and the lowercase SHA-256 digest of the package. The launcher rejects duplicate
runtime/sequence pairs, unknown or duplicate fields, an unsupported schema, invalid compatibility
data, unsafe URIs, invalid signatures, and catalogs larger than 256 KiB. For the current runtime it
always selects the release with the highest sequence, and only then checks launcher compatibility.
If that release needs a newer launcher, the catalog reports `LauncherUpdateRequired` instead of
quietly picking an older release.

The download code accepts only an HTTP 200 response from the signed URI. It rejects redirects,
credentials or query strings in URLs, fragments, content encoding, content larger than declared or
allowed, length or digest mismatches, default credentials, cookies, automatic decompression, and
stalled network operations. Each metadata operation must finish within two minutes. Package
transfers have a 12-hour limit on top of the 30-second network idle limit. Catalog documents are
held in memory with a size limit. Packages stream through SHA-256 directly into a new private file.
After a rejected response, an existing destination is never replaced or removed.

The package is a deterministic ZIP containing only:

```text
openconquer.release.json
openconquer.release.sig
client/<every manifest-declared file>
```

The package is rejected if it has directory entries, link or reparse entries, path traversal,
alternate separators, nonportable or ambiguous names, missing or extra entries, disagreeing
metadata, a file length or hash mismatch, more than 16,384 client files or extracted directories, a
size over 16 GiB, or an expanded payload over 64 GiB. Extraction writes new files under a private
per-attempt directory, checks the resulting tree shape again, and sanitizes Unix modes. It never
selects the candidate. `ManagedReleaseTransaction` separately authenticates the manifest,
signature, client tree, compatibility, sequence policy, and destination generation again before it
changes the descriptor atomically.

Acquisition workspaces must be absolute paths and must not overlap the managed product on the
filesystem, including through resolved aliases. A private cross-process lock lets only one
acquisition run at a time. At startup, while holding that lock, acquisition removes only stale
directories whose names match the launcher's own attempt-name format, and leaves unknown entries
alone. A returned candidate lease keeps the lock through the transaction and does best-effort
cleanup before releasing it. Cancellation is passed on as cancellation and is never turned into a
recoverable transfer error.

`ManagedReleaseMaintenance` handles state that changes while it runs. If the installed release is
healthy and its sequence is at or above the selected catalog sequence, it is kept, so replayed
metadata cannot downgrade a working client. A package with a higher sequence goes through the update
policy. A package with the same sequence can be used for repair only after the update policy has
rejected it, a fresh resolution has failed, and the transaction's own manifest-identity check has
passed. Expected failures keep their classification (catalog, transfer, package, installation, or
transaction) for the player-facing recovery UI that is still to come.

No production URL is hard-coded. A real deployment still needs a publisher-controlled HTTPS origin,
immutable package publishing, environment configuration, key custody and rotation, a way to publish
the catalog and its signature consistently with a matching cache policy, and an end-to-end check
against the live origin. This code does not cover launcher self-update, UI policy, elevated
installation, account login, or game protocol behavior.

## Local development composition

`create-local-product` is the standard way to build and exercise the complete managed product
during development, without making the Product Tool a production signing system.

The command takes no configuration options.

Local composition uses:

- a persistent, per-user ECDSA P-256 publisher used only for development
- PKCS#8 private-key storage outside the repository
- SHA-256 with RFC3279 DER ECDSA signatures
- a per-user build lock, so only one build runs at a time
- development release sequence numbers that are stored durably and only increase
- the active and previous product manifests as a minimum sequence that blocks rollback
- a separate workspace inside the repository for each run
- one active local product at a fixed location
- a rollback slot for the previous product
- activation by rename that can be rolled back safely

The layout inside the repository is:

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

The workspace holds all intermediate files: publish output, the public key, the raw signature,
trust, release metadata, and the staged candidate.

Workspace cleanup is best-effort. A cleanup failure does not replace the result of the composition.

The development publisher and the sequence and lock state live outside the repository, in the
current user's platform configuration area. On Unix, the state directory and sensitive files are
restricted to the current user.

The per-user build lock is shared by all repository clones that use the same development publisher
state. This keeps concurrent local builds from racing to create the publisher or to allocate a
release sequence.

Each release sequence is reserved durably and never reused on purpose. A build that fails after its
release identity is allocated can therefore leave a gap in the sequence.

If the stored sequence state is missing, the next reserved sequence must be greater than the highest
sequence found in the active or previous local product.

Client publishing, verification of the source and published content, and rejection of `Server.dat`
happen before a sequence is reserved. Failures at those steps do not use up a release identity.

After reserving a sequence, local composition:

1. creates the release manifest for the client publish;
2. signs the size-limited manifest bytes with the persistent development publisher;
3. creates the validated release-signature envelope;
4. creates launcher trust from the development public key;
5. publishes the launcher with that trust embedded;
6. verifies that the raw launcher publish is isolated;
7. stages the managed-product candidate;
8. verifies that the candidate contains no loose development or signing state; and
9. passes the candidate to local activation.

The final managed product must not contain:

- the development private key
- the development public-key file
- the raw development signature
- loose `release-trust.json`
- release-sequence state
- sequence locks
- build locks
- activation locks

Retail `Server.dat` must not appear anywhere in the published managed client either.

## Local activation

Local activation validates a completed candidate before it changes the active product.

Activation checks the candidate's installation descriptor, release manifest, signature-envelope
structure, and client integrity. Checking the publisher's signature cryptographically is left to
the launcher, which does it on its own when the product runs.

Activation:

1. validates how the local-product, workspace, and candidate paths relate to each other;
2. acquires the repository-local activation lock;
3. rejects a candidate whose release sequence is not higher than the active product's;
4. removes the older `product.previous` only after the new candidate has passed validation;
5. moves the current active product to `product.previous`;
6. moves the candidate into the active `product` directory; and
7. tries to restore the previous product if this last move fails.

If a candidate fails before activation, the current active product is left untouched.

`product.previous` is a rollback and recovery slot for local development. It does not implement
player-facing update or repair.

## Installed client update, repair, and rollback transaction

`ManagedReleaseTransaction` makes the changes to the installation once a candidate release has been
acquired. A candidate is one release root that contains the signed manifest, the signature
envelope, and the client directory, and nothing else. The transaction does not discover or download
releases, choose a channel, update the running launcher, elevate privileges, or define an account or
game protocol.

The candidate and managed-product roots must also not overlap, compared by filesystem identity.
Links in existing ancestors are resolved before the paths are compared. The comparison is
case-insensitive on every platform, so a path that differs only in case cannot get around the check
on filesystems such as default macOS APFS.

Before changing installed state, the transaction:

1. authenticates the candidate against the embedded publisher trust and hashes all of it;
2. acquires the product-root update lock, so only one update, repair, or rollback runs at a time
   across processes;
3. authenticates the current signed metadata, so the minimum sequence that blocks rollback is known
   even when the current client files are damaged;
4. rejects an update whose sequence is not higher than the current one, and a repair candidate that
   is not the same signed release;
5. copies only the files declared in the manifest, and no more than their authenticated lengths,
   into a staging generation that is not yet selected. It limits traversal, refuses links and
   devices, creates every destination as a new file, flushes files to disk, and uses restrictive
   Unix creation modes until the final modes are set;
6. authenticates and hashes the copied generation again.

Only after those checks does it rename the complete staging generation into `releases/` and replace
the descriptor. Replacing the descriptor is the commit point. It is an atomic replacement within the
same directory: `File.Replace` for an existing descriptor on Windows, and a rename or move
elsewhere. Cancellation is honored up to the last check before the commit. After the synchronous
commit there is no cancellable work left whose failure could wrongly report the installation as
unchanged.

An update records the previous active generation as the fallback only if it still passes full
verification. If the active files are damaged, an existing healthy fallback is kept. Otherwise the
new release is selected without a fallback. Repair always writes a new generation with a nonce
instead of modifying the selected directory in place. A requested rollback fully verifies the
fallback before selecting it, and keeps the replaced active generation as the next fallback only if
it is still healthy.

Expected failures are classified without exposing paths or exception text. The classes are:
current release metadata unavailable, candidate failed integrity checks, update does not advance,
downgrade blocked, wrong release for repair, fallback unavailable, concurrent maintenance, access
denied, linked paths, and filesystem failure. Cancellation is reported as cancellation and never
turned into a repair error.

On supported local filesystems with atomic same-directory replacement, a process crash has a
predictable outcome:

- Before the descriptor is replaced, the previous active release stays selected.
- After the descriptor is replaced, the fully copied and re-verified generation is selected.
- Incomplete staging directories and finished but unreferenced generations have no effect, because
  no descriptor names them.
- This transaction never deletes the selected active or fallback generation.

Copied files and the temporary descriptor are flushed to disk before activation. Staging files that
are still being written, and the transaction lock, are created with private Unix permissions. The
published descriptor is given portable read permissions before activation.

.NET has no portable way to flush a parent directory, so the launcher makes no stronger promise
about sudden power loss than the host filesystem does.

Old generations are not removed automatically. That waits until the launcher controls the client
process lifetime and can prove a generation is no longer running, which matters most on Windows.
Production release-origin integration, the recovery UI, launcher self-update, and signed platform
deployment are future launcher work.

## Verification

`ManagedInstallationResolverTests` cover rejected schemas and layouts, missing release metadata,
unavailable release trust, untrusted signatures, modified client files, and successful trusted
resolution.

`ManagedReleaseTransactionTests` and the related hardening tests cover trusted updates, same-release
repair, recovery from a damaged client, legacy migration, verified rollback, rejection of a corrupt
fallback, sequence policy, binding of the release identity, cancellation, lock contention, linked
and unexpected candidate entries, rejection of filesystem-identity aliases, staging limited to
authenticated lengths, selected-generation shape, Unix lock permissions, and Unix executable
permissions.

`ManagedInstallationPathGuardTests` cover identical, case-insensitive, ancestor-link, and unrelated
filesystem identities. `ManagedInstallationManifestTests` cover how the descriptor is replaced and
its final Unix permissions.

`ReleaseAcquisitionTests` cover selection of an authenticated, compatible release, rejection of
tampered or ambiguous data, unsafe URLs, size-limited transfers that must match the declared length,
rejection of redirects and oversized content, leaving existing files in place on a collision,
archive extraction, containment of path traversal, metadata agreement, Unix executable bits,
one-at-a-time workspace use, ownership of stale attempts, lease cleanup, end-to-end update and
same-release repair, downgrade prevention, and classification of catalog failures.

`LauncherApplicationTests` cover retry, cancellation ownership, concurrent and reentrant shutdown,
callback failures, and the final state.

`ManagedProductIntegrationTests` exercise real manifest creation, signing, staging, trusted
resolution, untrusted publishers, and recovery without restarting the application.

Product Tool tests cover:

- release metadata
- release-signature envelopes
- deterministic release packages and archive validation
- building multi-runtime catalogs authorized by the publisher, and catalog signatures
- publisher trust
- persistent development publisher identity
- development-state permissions
- per-user build serialization
- release sequences that only increase, and recovery of the rollback floor
- repository and runtime discovery
- local-product paths
- isolation of publish and product artifacts
- release-manifest traversal order and rejection of linked entries
- local-product orchestration
- local activation that can be rolled back safely
- managed-product staging

CI generates a throwaway P-256 publisher only to exercise the release composition steps that
production will also use. It creates the client manifest, creates release trust with the Product
Tool, signs the manifest outside the tool, creates the verified signature envelope, packages the
client, builds the catalog and signs it outside the tool, embeds the matching public trust root in
the CI launcher publish, and composes the managed product.

The CI publisher is unrelated to the persistent local-development publisher. Production release
signing must use a separately controlled production publisher identity.

To check the result on a real desktop, run the local managed launcher from
`artifacts/local-product/product`. A valid local product must reach the ready state.

When you change release integrity, product composition, or installation resolution, also copy the
managed product to a separate temporary directory, change one authenticated client file, and run the
copied launcher. The launcher must reject the modified installation, and the active product must
stay unchanged.

See [Local Managed Product](../development.md#local-managed-product) in the development guide for
the commands.
