# Content compilation and distribution

## Ownership and trust

The repository owns import rules, content schemas, runtime decoders, release tooling, and tests.
Source artwork and other third-party resources remain subject to their own distribution rights.
Do not publish source installations, research databases, or signing keys as part of a client release.

The import stage validates source paths and bytes, resolves implemented consumer dependencies,
and records their identities and digests. It does not decide how the installed product stores
resources. The compiler is the only owner of conversions from imported bytes to engine resources.
A conversion must preserve the decoded semantics established by conformance evidence.

The Product Tool owns release construction and deterministic packaging. The publisher signs
release metadata outside the client and launcher. The launcher verifies signed metadata and
package identity before acquiring, extracting, and activating an immutable release generation.
The game process consumes only installed content through the content-source contract; it does
not perform source verification or manage release installation.

## Target layout and access

Imported source revisions, compiled content, published release objects, installed generations,
and local runtime caches are separate artifacts with separate retention policies:

- **Imported source:** validated inputs and an immutable inventory; not a runtime directory.
- **Compiled content:** canonical versioned resources with explicit dependencies and reproducible bytes.
- **Release objects:** immutable, indexed bundles and a signed mapping of the objects required by a release.
- **Installation:** an authenticated active generation and a retained rollback generation.
- **Runtime cache:** disposable decoded CPU data and GPU resources owned by their consumers.

Content identity must be independent of the file or bundle that stores it. The compiler produces
a deterministic dependency index and stores shared resources once per content revision. The runtime
opens a requested resource through one content-source interface. A bundle reader owns index
validation, random access, integrity checking, and bounded decompression; consumers own decoded
object lifetimes. CPU decoding and disk I/O must not block the rendering thread. GPU uploads and
disposal remain under rendering ownership.

Bundles are immutable release objects. An update downloads only missing objects verified against
the authenticated release description; it builds a candidate generation before atomic activation.
A partially downloaded candidate must not alter the active generation. Repair follows the same
integrity checks. Garbage collection may reclaim only objects not referenced by active, retained,
or in-progress generations.

The bundle index schema, compression method, object size and granularity, and caching budgets
remain design gates, not selected implementation constants. They require representative map,
texture, animation, and audio measurements plus evidence of source-to-compiled equivalence.
Do not add a second runtime lookup path or a second package-signing system during migration.

## Current capacity and compatibility

Release manifests retain schema version 1. Launcher compatibility is independently versioned.
Launcher version 2 supports up to 32,768 files, 32,768 directories, and a 32 MiB manifest.
These are separate bounds: exceeding any one of them rejects the release.

Version-1 launchers support at most 16,384 files, 16,384 directories, and an 8 MiB manifest.
The Product Tool must reject a new release exceeding any of those bounds unless its minimum
launcher version is at least 2. Existing compatible version-1 signed releases remain readable.
A version-1 installation cannot consume an expanded release until its launcher has been replaced.
Launcher self-update is not part of the current installation service; publishing must account
for that external upgrade requirement.

The package remains a deterministic, signed-manifest-bound ZIP until a measured bundle design
is implemented. ZIP package and expanded-size limits, path safety, signature trust, sequence
checks, rollback, and atomic activation are unchanged. Increased manifest capacity is not
permission for unbounded memory use or for putting all source assets in one release.

## Migration and acceptance

1. Align producer, launcher, signing, packaging, extraction, and installation capacity, with
   versioned compatibility and boundary tests.
2. Specify and test canonical compiled map semantics before changing the runtime representation.
   Include map IDs 1010, 1929, and 1999 and distinguish confirmed behavior from unresolved data.
3. Benchmark source layouts and candidate indexes and bundles using cold/warm loading, CPU and GPU
   memory, file I/O, update size, repair, and build reproducibility. Select format details from
   those measurements.
4. Add a compiled-content reader behind the existing content-source ownership boundary.
   Demonstrate equivalent map geometry, layering, animation, hit testing, and resource timing.
5. Integrate authenticated immutable bundle acquisition and generation activation, preserving
   rollback and power-loss recovery. Remove the superseded loose-file path in a controlled migration.
6. Move bulk assets out of the source repository only after the licensed publishing source, CI
   acquisition, reproducibility, and migration plan have been verified and approved.

## Content layout assessment

Run `analyze-content-set --content-set <root>` with the same map-coverage flags used
for `verify-content-set`. The tool verifies the payload and declared implemented closure
before reporting file sizes, ordered size percentiles, duplicate digests, per-format totals,
and path-family totals. Each duplicate is counted only after comparing the complete SHA-256
and file length. Reports are deterministic for a given verified inventory.

This is a storage inventory baseline, not a latency or compression benchmark. A later
benchmark must record actual cold/warm access, decoding cost, CPU/GPU memory, bundle/index
size, and differential update behavior before selecting a distribution format.

## Map semantic comparison

Before replacing the map container or DMap decoder, compare two independently staged
content roots using the existing map parsers:

```text
OpenConquer.Content.Tool compare-map-semantics --source <source-root> --candidate <candidate-root> --map-data-id 1010
```

The comparison covers catalog selection, map header and dimensions, every passability,
surface, and elevation cell, exit markers, scenery record order and complete encoded payloads,
positioned overlay headers, and ordered child entries. It fails on the first difference or
invalid map. Raw archive compression is not compared: the requirement is decoded semantic
equivalence. This check does not establish equality of referenced image, ANI, puzzle, or
scene-file resources; those still require their existing validators and rendering evidence.

Maps 1929 and 1999 remain explicit evidence gates; the comparison must not turn an absent
puzzle or an unresolved archive discrepancy into a fabricated successful load.

Every stage must remain runnable and verifiable on Windows, macOS, and Linux. Build success alone
does not establish rendering or gameplay equivalence. Format migration requires captured behavior
and deterministic semantic comparisons; platform-specific GPU differences require documented,
reproducible visual tolerances.
