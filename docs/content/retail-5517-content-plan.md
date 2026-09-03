# Retail 5517 Content Ingestion Plan

## Decision Status

**Active and implemented incrementally.**

OpenConquer does not bulk-import retail directories.

The checked-in content set is the exact dependency closure of implemented consumers:

```text
ClientContentClosure
        ==
manifest
        ==
payload
```

The closure expands only when a reviewed implementation slice introduces a real consumer.

The full retail inventory remains documented in
[`retail-5517-inventory.md`](retail-5517-inventory.md) as compatibility evidence and planning input.
Inventory does not imply that every surveyed file belongs in the repository.

## Objective

Reconstruct the 5517 client from verified retail behavior while maintaining a secure,
cross-platform, production-grade content boundary.

The system must:

- preserve compatibility-sensitive retail path identities;
- consume authorized retail sources without mutating them;
- validate legacy data as untrusted input;
- keep source-format behavior separate from modern architecture;
- stage deterministic content for implemented consumers;
- prevent unsupported assets from entering releases accidentally;
- allow the checked-in closure to expand predictably as reconstruction proceeds.

## Governing Decisions

### Consumer-led migration

Content enters the repository because executable code consumes it, not because it shares a directory
with something already supported.

Every expansion slice must establish:

1. the consumer;
2. the native or retail behavior being preserved;
3. the content lookup mode;
4. the parser or decoder boundary;
5. malformed-input behavior;
6. tests;
7. the resulting `ClientContentClosure` change;
8. manifest and payload verification.

Directory-sized migration is explicitly rejected.

### Retail paths are compatibility identities

Retail paths remain meaningful compatibility identifiers.

The modern content boundary may validate and normalize paths for safe lookup, but consumers do not
casually rename retail resources or reinterpret path structure.

Host filesystem containment is enforced separately from native virtual-path normalization.

### Native evidence determines compatibility behavior

The legacy reconstruction is useful for locating concepts and understanding historical flows, but it
is not authoritative architecture.

For parity-sensitive behavior, evidence priority is:

1. verified native 5517 behavior and resources;
2. retail payload evidence;
3. legacy reconstruction as supporting reference.

Unsafe native undefined behavior does not need to be reproduced.

### Legacy data is untrusted input

Known hashes establish identity, not safety.

Readers and importers must still validate:

- lengths and counts before allocation;
- checked offset arithmetic;
- signatures and structural invariants;
- filesystem containment;
- symbolic links and reparse points;
- case-insensitive ambiguity;
- archive boundaries;
- decoded dimensions and expansion;
- malformed text and binary inputs.

## Current Implemented Closure

The checked-in `content/retail-5517` payload currently contains:

```text
data/main/Logo1.bmp
data/main/Logo2.bmp
ini/GameSetUp.ini
ini/info.ini
ini/package.ini
```

These files support the implemented startup consumers:

- screen-mode configuration;
- package declaration registration;
- startup-logo path configuration;
- the two verified retail startup-logo variants.

No `ani/`, map, C3, audio, login, or general UI families are checked in merely for future use.

## Current Runtime Content Boundary

`OpenConquer.Content` currently provides:

```text
ClientContentPath
        │
        ├── structural virtual-path validation
        └── package-path normalization

ClientContentRoot
        │
        └── contained case-insensitive loose-file lookup

WdfArchive
        │
        └── bounded package-entry lookup

PackagedClientContentSource
        │
        ├── LooseOnly
        ├── PackageOnly
        └── LooseThenPackage
```

Typed startup consumers sit above that boundary:

```text
GameSetupConfiguration
StartupLogoConfiguration
StartupLogo
WindowsBitmapReader
```

The application composes the content source; consumers do not know whether bytes came from a host
filesystem or WDF archive.

## Current Tooling Boundary

`OpenConquer.Content.Tool` imports and verifies deterministic content sets.

Import resolves `ClientContentClosure` against an authorized source tree and stages only those
files.

Verification requires:

```text
resolved code closure
        ==
manifest path keys
        ==
observed payload path keys
```

It additionally verifies expected length, format signature, and SHA-256 identity.

This makes both of the following invalid:

- adding a payload file and manifest entry that no implemented consumer requires;
- removing a required file from both payload and manifest.

## WDF Policy

Native analysis has established the package-registration and routing model required for current
compatibility:

- `package.ini` is whitespace-token parsed;
- missing declarations are non-fatal;
- prefixes are derived from declarations and compared first-wins;
- a missing first WDF still owns its prefix;
- routing uses the first virtual-path segment as package key;
- hashing uses the full normalized virtual path;
- loose and packaged lookup modes remain explicit.

The next WDF-specific hardening slice must finish the untrusted-archive boundary without changing
those verified compatibility semantics.

That slice should cover, where supported by format/native evidence:

- practical entry-count bounds;
- strict table validation;
- reserved fields;
- entry payload bounds relative to the index;
- payload overlap;
- deterministic malformed/truncated failure;
- checked offset/length arithmetic;
- existing-but-unopenable or malformed registered-package behavior.

## Expansion Rule

A future slice that needs another retail asset follows this sequence:

```text
audit consumer behavior
        ↓
verify native/retail evidence
        ↓
implement typed reader/decoder/provider behavior
        ↓
add focused tests
        ↓
extend ClientContentClosure
        ↓
import exact new dependency
        ↓
verify manifest == payload == closure
        ↓
run full release gate
```

The content set is never expanded speculatively.

## Planned Consumer-Led Expansion

Likely future areas, subject to actual reconstruction order, include:

1. server-selection/bootstrap data;
2. first-party login and core UI resources;
3. fonts, localization, cursors, icons, and layout definitions;
4. item and role definitions;
5. map indexes, terrain, minimaps, and scenery;
6. C3 models, textures, motion, and effects;
7. sound effects and music.

Each is its own audited slice or sequence of coherent slices.

The existence of these families in retail does not authorize importing them early.

## Validation Requirements

Every legacy format boundary should apply the requirements relevant to its structure:

- bounded file size before reading;
- bounded collection counts before allocating;
- checked offset and length arithmetic;
- signature validation;
- structural range validation;
- deterministic duplicate handling;
- path containment and link rejection for host files;
- no extraction to attacker-controlled paths;
- bounded decoded image/data dimensions;
- deterministic diagnostics;
- no silent fallback outside verified compatibility behavior.

Cleanup failures must not replace an existing primary failure.

## Test Strategy

Content tests should remain synthetic wherever practical.

They should cover:

- path validation;
- case-insensitive loose lookup;
- containment and symlink/reparse rejection;
- package-prefix and duplicate semantics;
- missing-package behavior;
- WDF parsing and bounds;
- configuration grammar;
- bitmap decoding;
- content-closure resolution;
- import determinism;
- manifest/payload/closure verification;
- malformed and adversarial inputs.

Tests must not require redistribution of large retail payload families.

## Commit and Release Gate

A content slice is not complete until:

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
  -- verify-content-set \
  --content-set content/retail-5517

git diff --check
```

Published client output must contain the same manifest-approved closure as the checked-in content
set.

## Non-Goals

The content system is not:

- a bulk retail-file mirror;
- a general-purpose game-engine asset pipeline;
- an excuse to pre-import unsupported content;
- an architecture copied from the legacy reconstruction;
- a compatibility layer that preserves unsafe native undefined behavior.

Its job is to provide the smallest correct, deterministic, auditable content boundary required by
the reconstructed 5517 client.
