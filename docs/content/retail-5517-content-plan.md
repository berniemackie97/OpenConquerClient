# Retail 5517 Content Ingestion Plan

## Decision Status

**Implemented foundation.** The complete `ini/`, loose `data/`, and directly indexing `ani/`
families are now preserved under the versioned `content/retail-5517` set with a deterministic
manifest. Runtime catalog expansion remains incremental.

The observed source inventory is recorded in
[`retail-5517-inventory.md`](retail-5517-inventory.md).

## Objective

Reconstruct the 5517 client from verified retail behavior while giving OpenConquer a clean,
cross-platform content architecture. The project must be able to consume an authorized retail
snapshot during development, select the dependency closure required by implemented features, and
produce deterministic runtime content without mixing large opaque assets into source projects.

## Governing Decisions

### 1. Preserve source; organize through catalogs

The retail tree is immutable evidence. Import tooling reads it but never renames, repairs, converts,
or writes back to it. Original case and relative path are recorded as source identity.

Modern organization is represented by typed logical asset IDs and reviewed catalog metadata. It is
not created by manually rearranging retail folders and hoping every embedded path is found and
rewritten.

### 2. Keep three boundaries distinct

```text
authorized retail snapshot (read-only)
              |
              v
inventory + semantic import catalog (reviewable, deterministic)
              |
              v
generated runtime content set (immutable deployment artifact)
```

- **Source** preserves native names, bytes, package membership, and precedence evidence.
- **Catalog** assigns ownership, logical identity, decoder, dependencies, and import status.
- **Runtime content** contains only the closure needed by supported features and is safe for the
  modern client to load.

This separation lets local development continue using `--content-root` while the modern package
provider is built incrementally.

### 3. Keep retail payloads outside `src/` and ordinary build output

Source projects contain readers, validators, catalogs, and runtime interfaces. The source-preserved
payload belongs in the versioned top-level `content/` product, never in assembly directories.
Ordinary builds stage only the implemented bootstrap closure rather than wildcard-copying all 233 MB
of source bytes into every output. Release packaging can later select reviewed entries by manifest
disposition.

### 4. Prefer typed readers over a universal “INI loader”

Retail extensions do not define a single grammar or semantic model. Shared infrastructure may
handle bounded byte reading, line splitting, section/key tokenization, duplicate policy, and source
diagnostics. Each catalog owns its typed validation and native quirks.

### 5. Fail closed at import; load immutable results at runtime

Import is the place for expensive hashing, dependency analysis, decoding, and strict diagnostics.
Runtime packages are immutable and versioned. Runtime does not silently search arbitrary host
paths, reinterpret a malformed file, or fall back to an unrelated asset.

## Repository Layout

The implemented content boundary is:

```text
content/
├── README.md
└── retail-5517/
    ├── manifest.json                 # deterministic identity and integrity catalog
    └── payload/
        ├── ini/                      # complete source-preserved family
        ├── data/                     # complete source-preserved loose family
        └── ani/                      # complete catalogs indexing data assets

src/OpenConquer.Content/
├── Catalogs/                         # typed logical catalogs
├── Formats/                          # bounded legacy decoders
├── Sources/                          # directory, WDF, and modern package providers
└── Validation/                       # path, signature, size, and dependency checks

tools/OpenConquer.Content.Tool/       # validated, deterministic retail-family import

tests/OpenConquer.Content.Tests/
├── Fixtures/                         # minimal synthetic or legally reviewable format fixtures
└── Golden/                           # expected manifests and diagnostics, never full retail data

artifacts/content/retail-5517/        # future generated runtime packages
```

## Logical Runtime Taxonomy

The catalog presents stable domains to consumers:

```text
configuration/
├── bootstrap/
└── defaults/

definitions/
├── actions/
├── effects/
├── items/
├── magic/
├── roles/
└── world/

presentation/
├── animations/
├── cursors/
├── effects/
├── icons/
├── layouts/
├── localization/
├── portraits/
└── textures/

world/
├── maps/
├── minimaps/
├── scenery/
└── terrain/

models/
├── equipment/
├── objects/
└── roles/

audio/
├── music/
└── sound-effects/
```

This is a logical namespace. A logical entry may point to a path-preserved source during
compatibility development and to a content-addressed payload in a generated package. Consumers do
not receive retail paths or know which provider supplied the bytes.

## Content Source Architecture

`ClientContentRoot` is a useful secure directory boundary, but a complete source model needs stream
access independent of physical storage.

The target abstraction should support:

- existence checks and required opens by validated content key;
- original path and provider provenance in diagnostics;
- bounded sequential streams rather than exposing absolute host paths;
- case-insensitive Windows-era lookup with ambiguity rejection;
- cancellation for long inventory/import operations;
- deterministic enumeration for tooling, without requiring runtime enumeration;
- explicit empty-file versus missing-file behavior.

Planned providers:

| Provider | Purpose | Status |
| --- | --- | --- |
| Legacy directory | Read loose files through current containment and link checks | Extend current boundary |
| WDF package | Resolve and stream package entries without extraction | Format and precedence evidence required |
| Composite retail source | Apply verified package/loose ordering and prefix rules | Blocked on native verification |
| Modern content set | Read a validated generated manifest and payload store | Implement after catalog schema |

Do not encode provider order in consumers. The composite source owns it, and tests pin the verified
native result for collisions, missing packages, and loose overrides.

## Manifest Contract

The generated source inventory and reviewed import catalog use versioned schemas. No manifest
contains workstation-specific absolute paths.

Minimum source-entry fields:

| Field | Purpose |
| --- | --- |
| `sourcePath` | Original slash-normalized retail relative path with original case retained |
| `pathKey` | Separately generated case-folded lookup key |
| `provider` | Loose directory or named package |
| `length` | Exact source byte count |
| `sha256` | Source payload fingerprint |
| `signature` | Observed magic/signature classification, independent of extension |
| `encoding` | Verified text encoding or `binary`/`unknown` |
| `sourceSet` | Immutable source-set identifier tied to `version.dat` and root fingerprints |

Minimum reviewed catalog fields:

| Field | Purpose |
| --- | --- |
| `assetId` | Stable, typed logical identity used by code |
| `kind` | Typed format/consumer classification |
| `source` | One exact source inventory entry |
| `decoder` | Versioned decoder/transform identifier |
| `dependencies` | Logical asset IDs discovered by a semantic reader |
| `feature` | Owning runtime feature or bootstrap slice |
| `disposition` | `import`, `source-only`, `replace`, `defer`, or `exclude` |
| `reason` | Reviewable rationale for non-import dispositions |

Minimum runtime-entry fields add the output hash, output length, media/format identity, and package
location. Import fails if two entries claim the same logical ID or case-folded key.

## First Work Slice: INI, Data, and Direct Dependencies

### In scope

- Complete, deterministic source-preserving migration for `ini/`, `data/`, and `ani/`.
- Root source fingerprints and package declarations.
- WDF header/index research sufficient to enumerate and open bounded entries.
- Native evidence for package order, loose override precedence, and path prefix/hash rules.
- Signature classification for the observed data formats.
- Encoding profiles for text catalogs selected in the first vertical slice.
- Typed parsing for retail startup configuration and one animation catalog.
- DDS/TGA signature validation and DXT1/DXT3/DXT5 capability decision.
- One end-to-end presentation asset: catalog ID -> descriptor -> texture -> decoded image -> renderer
  resource.
- A generated manifest and dependency-closure report for that vertical slice.

### Included only as dependency boundaries

- `map/`, because `GameMap.dat` and some animation catalogs refer into world content;
- `c3/`, because rendering definition tables refer to models and textures;
- `sound/`, because action and region catalogs bind audio;
- `data.wdf` and `c3.wdf`, because loose files alone are incomplete.

Their bulk parsing, transformation, and rendering are later slices.

### Explicitly out of scope

- Executables, DLLs, OCX files, anti-cheat, autopatcher behavior, and server endpoints;
- `Help/` executable content;
- Flash runtime embedding;
- bulk C3 model conversion;
- full map-scene composition;
- audio playback;
- committing or publishing retail payloads before provenance review.

## Delivery Sequence and Gates

### Phase 0 — Correct the current retail baseline

1. Replace the inverted `GameSetupConfiguration` fixture with the observed
   `[ScreenMode] ScreenModeRecord=<value>` shape.
2. Update the typed reader and compatibility documentation together.
3. Retain existing invalid/missing value coverage and add a test sourced from the retail byte shape.

**Exit gate:** the current application can start against the supplied retail configuration without
weakening content-root containment or inventing defaults.

### Phase 1 — Deterministic inventory tool

Implement `inventory` as a cross-platform .NET command that:

1. validates `version.dat` and the requested source set;
2. enumerates deterministically with ordinal path ordering;
3. rejects links, traversal, and case-fold collisions;
4. records length, SHA-256, signature, and conservative text/binary classification;
5. inventories WDF entries without extracting them;
6. emits canonical, schema-versioned JSON through an atomic write;
7. reports exclusions, empty files, extension/signature mismatches, and missing declared packages.

**Exit gate:** two runs against unchanged input are byte-identical, and CI can test the tool with a
small synthetic tree and WDF fixture.

### Phase 2 — Retail source composition

Introduce stream-based directory, WDF, and composite providers. Establish package ordering and
loose override behavior from native analysis, then lock it with collision fixtures.

**Exit gate:** a reference set containing loose-only, package-only, override, empty, ambiguous-case,
and missing entries resolves exactly as the verified native policy requires.

### Phase 3 — Typed catalog foundation

Build bounded readers for:

- startup/default configuration;
- the selected top-level text `.ani` descriptor;
- texture headers and validated payload dispatch.

Readers return typed values and source-aware diagnostics. Embedded paths are parsed as format-level
references and resolved through the owning catalog—not assumed to be root-relative filesystem
paths.

**Exit gate:** import produces a dependency-closed catalog for the selected vertical slice with no
unclassified required file.

### Phase 4 — First runtime content set

Package only the selected bootstrap/UI closure. Preserve each logical path while allowing identical
payload bytes to share content-addressed storage. Add a modern provider and wire it alongside the
retail development provider at the composition root.

**Exit gate:** the same logical asset renders from both an authorized retail root and the generated
content set, with matching decoded dimensions and pixel checksum.

### Phase 5 — Expand by feature

Add content in consumer-led slices rather than directory-sized batches:

1. login and server selection presentation;
2. core UI layouts, fonts, localization, cursors, and icons;
3. item and role definition catalogs;
4. map index, base terrain, minimaps, and scenery;
5. C3 models, textures, motion, and effects;
6. sound effects and music;
7. optional legacy features only when their modern consumer exists.

Every slice extends typed consumers and runtime staging from the complete source-family catalog;
files are not discovered and copied ad hoc at the moment a consumer happens to need them.

## Validation and Security Requirements

- Treat every legacy file and package as untrusted input even when its hash is known.
- Bound file length, entry count, string length, image dimensions, decoded bytes, dependency count,
  recursion depth, and decompression expansion before allocating.
- Use checked arithmetic for offsets, sizes, dimensions, and record counts.
- Validate magic bytes before selecting a decoder; extensions remain identity hints only.
- Reject package entry overlap, offset overflow, truncated tables, duplicate ambiguous keys, and
  paths that escape the logical root.
- Never extract archive/package entries to attacker-controlled paths.
- Make import cancellation-safe and write outputs atomically after full validation.
- Keep generated packages immutable; verify manifest and payload hashes before publication/use.
- Exclude host artifacts such as `Thumbs.db`, logs, screenshots, and updater state by explicit rule
  with an audit reason.
- Sanitize diagnostics so malformed binary data cannot inject terminal control sequences.

## Test Strategy

### CI-safe tests

- Synthetic fixtures cover every structural branch, limit, and corruption case.
- Small golden fixtures capture proven retail quirks without requiring the full proprietary corpus.
- Manifest output is snapshot-tested for deterministic ordering and schema compatibility.
- Provider contract tests run identically against directory, WDF, and modern package sources.
- Property/fuzz tests target binary readers, path normalization, and image header parsing.

### Authorized retail validation

A separate opt-in command accepts an external source path and validates known fingerprints, observed
counts, package declarations, catalog parse totals, dependency closure, and selected decoded output
hashes. It is never a default CI prerequisite and never uploads source assets.

## Operational and Review Policy

- Source-set and schema versions advance independently.
- A decoder change records its version and forces deterministic regeneration of affected outputs.
- Catalog changes are code-reviewed because they alter feature behavior and redistribution scope.
- Generated artifacts publish with their manifest, source-set ID, tool version, and checksums.
- Runtime telemetry and errors refer to logical asset IDs plus safe provenance, not absolute local
  paths.
- No silent fallback is allowed for a required asset. Optional assets are explicitly marked optional
  in the typed catalog.

## Open Evidence Questions

These block portions of implementation and must not be answered by convention:

1. What exact hash algorithm and normalization does WDF lookup use for this client build?
2. What are the native order and failure rules for `data.wdf`, `c3.wdf`, and the absent
   `data3.wdf`?
3. Do loose files override packages globally, per package, or per loader?
4. Which same-stem `.dbc`/`.ini` representation does each native subsystem prefer?
5. Which Windows code page does each high-byte text family use, and are there field-level
   exceptions?
6. Which catalogs prepend implicit namespaces such as `data/`, rather than treating references as
   root-relative paths?
7. Which SWF-driven flows are required for a faithful 5517 experience and which are obsolete
   web/launcher integrations?
8. Which `data/main/*.dat` files are required by the game process versus the legacy login host?

## Definition of Done for This Planning Slice

- The supplied source is identified and quantified without modification.
- `ini/`, `data/`, and direct `ani/`/WDF dependencies have an explicit classification.
- Format traps, current compatibility drift, and unknown native precedence are visible.
- Repository, source, catalog, and generated-artifact ownership are separated.
- The first implementation slice has ordered phases and objective exit gates.
- Deferred map, C3, audio, Flash, launcher, and help work cannot accidentally enter through a bulk
  copy.
