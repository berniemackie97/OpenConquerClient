# Retail 5517 Content Ingestion Plan

## Decision Status

**Active and implemented incrementally.**

OpenConquer does not bulk-import retail directories.

The checked-in content set is the exact dependency closure of implemented consumers:

```text
ClientContentClosure.Resolve(payload)
        ==
manifest path keys
        ==
observed payload path keys
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

Modern safety limits may be stricter than the native implementation when they do not reject valid
retail 5517 data or change verified compatibility behavior.

## Current Implemented Closure

The checked-in `content/retail-5517` payload currently contains:

```text
Server.dat
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
- the two verified retail startup-logo variants;
- the typed retail `Server.dat` server-catalog boundary.

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
        ├── contained case-insensitive loose-file lookup
        └── host link/reparse rejection

WdfArchive
        │
        ├── bounded header/index validation
        ├── sorted UID lookup
        └── bounded WdfEntryStream creation

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
ServerDatCatalogLoader
        │
        ├── ServerDatEnvelopeDecoder
        ├── ServerDatNativePublicKey
        └── ServerDatXmlCatalogReader
                │
                ▼
            ServerCatalog
```

The application composes the content source; consumers do not know whether bytes came from a host
filesystem or WDF archive. `ServerDatCatalogLoader` deliberately requests `LooseOnly`, preserving
the verified native rule that `Server.dat` is read directly from the client root and never falls
back to a package. The typed catalog is implemented and parity-tested but is not yet consumed by
first-party server-selection UI or networking.

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

## Server.dat Policy

The retail `Server.dat` boundary is implemented as a narrow startup-content pipeline:

```text
loose Server.dat
        ↓
bounded encrypted read
        ↓
verified 5517 RSA public key
        ↓
PKCS#1 type-1 extraction
        ↓
bounded gzip inflate
        ↓
outenserver XML
        ↓
typed immutable ServerCatalog
```

Verified retail identity:

```text
length: 2816 bytes
RSA blocks: 11 × 256 bytes
SHA-256: 0b4d366786aa4498c7e470f10fd8bca716bc1d6cbda1eb3894666183f8327a90
```

The independently recovered native public key uses exponent `65537` and a 2048-bit modulus whose raw
256-byte SHA-256 is:

```text
76acb04b08190b129985f8dee2b466efcd686eb1662cb598bd1a8154cb9196f1
```

The modern implementation stores the final verified modulus directly rather than reproducing the
native constructor's seed/schedule/BIGNUM assembly. The native derivation remains locked by parity
tests.

Envelope validation includes:

- whole 256-byte RSA blocks;
- a modern maximum of 64 encrypted blocks;
- RSA representatives strictly below the modulus;
- PKCS#1 type-1 prefix `00 01`;
- at least eight `FF` padding bytes;
- a required zero separator and non-empty extracted chunk;
- gzip signature validation;
- a 1 MiB modern inflate ceiling;
- deterministic malformed-data failures.

The exact retail fixture decodes through the production key to 38,819 XML bytes with SHA-256:

```text
5d6b00ff722a8b37aa2981affecd478aee73bdc22cdc498a25b700242b55c35a
```

The XML reader prohibits DTDs, requires exactly one `table_data[name=outenserver]` table, rejects
duplicate row IDs and duplicate field names, bounds group/server counts, and projects the verified
row scheme into immutable `ServerGroup` and `ServerDefinition` objects. Host and port values remain
source text at the Content boundary; endpoint validation belongs to the future networking handoff.

The current retail root row declares 14 server groups. The content tests lock the authentic retail
file, encrypted and inflated hashes, 11-block shape, and 14-group catalog result.

## WDF Policy

The WDF boundary now implements the verified retail registration, routing, index, and lookup
contracts required by current consumers.

### Package declaration registration

Native `GraphicData.dll` consumes `ini/package.ini` as whitespace-delimited package-name tokens, not
as an INI section/key document.

The modern boundary preserves the verified non-gating behavior:

- a missing declaration file registers zero packages and startup continues;
- an unavailable declaration file registers zero packages and startup continues;
- a declaration file rejected by the modern bounded-read safety policy registers zero packages and
  startup continues;
- an individual missing package does not abort registration;
- an individual unavailable or structurally invalid WDF does not abort registration;
- later independent package declarations continue to be processed.

The declaration file is bounded to 64 KiB by modern policy. This is not a native format limit; it
prevents an untrusted optional startup file from driving unbounded allocation while preserving the
native non-fatal package-registration boundary.

### Prefix derivation and package identity

For each declared package:

1. ASCII `A-Z` is folded to lowercase;
2. `\` is converted to `/`;
3. everything from the final `.` onward is removed;
4. no basename extraction occurs.

For example:

```text
data.wdf        -> data
folder/data.wdf -> folder/data
data.v2.wdf     -> data.v2
```

The normalized prefix string is useful for diagnostics, but it is **not** the actual native routing
identity.

Native `TqPackagesOpen` hashes the prefix with `WdfHash_Core` and compares only that 32-bit hash
against registered package objects.

Therefore:

- package registration is first-wins by **prefix hash**;
- the first declaration owns that hash before its WDF is opened;
- a missing or unavailable first WDF still owns the hash;
- a later declaration with the same prefix is a duplicate;
- two different prefix strings whose 32-bit hashes collide are also duplicates;
- the first registration wins in either case.

`WdfPackageRegistration` retains the human-readable prefix and the observable registration outcome,
while the runtime routing table is keyed by the native 32-bit prefix hash.

### Virtual-path routing

Package lookup:

1. validates the modern virtual-path structure;
2. normalizes separators;
3. derives the first normalized virtual-path segment;
4. hashes that segment with the native WDF hash;
5. selects the first registered package with the matching prefix hash;
6. hashes the complete normalized virtual path to obtain the WDF entry UID.

The native hash implementation uses a 256-byte zero-padded buffer and silently truncates longer
inputs. `WdfPathHash` preserves that verified behavior.

The lookup modes remain explicit:

- `LooseOnly`;
- `PackageOnly`;
- `LooseThenPackage`.

There is no universal loose/package precedence outside the entry point that requested the lookup.

### Archive validation

A WDF archive is treated as untrusted binary data.

The implemented reader validates:

- the `PFDW` magic;
- the 12-byte archive header;
- a modern maximum of 100,000 entries before index allocation;
- checked index-table size and end arithmetic;
- an index offset at or after the header;
- an index table fully contained by the physical archive;
- complete 16-byte index records;
- a zero reserved DWORD in every index record;
- strictly ascending entry UIDs;
- duplicate UID rejection;
- payload offsets at or after the header;
- payload ends at or before the index-table offset;
- deterministic rejection of malformed or truncated structures.

The 100,000-entry ceiling is a modern resource-safety policy, not a native format limit. The
surveyed retail archives contain:

```text
c3.wdf    10,274 entries
data.wdf  14,739 entries
```

so the limit leaves substantial compatibility headroom while preventing an attacker-controlled
header from requesting effectively unbounded index allocation.

The sorted on-disk UID table is retained in memory and queried with binary search rather than being
flattened into an unordered dictionary. That preserves the verified format invariant and native
lookup model.

`WdfEntryStream` owns one physical archive stream and exposes only the selected entry range. Reads
and seeks cannot escape the entry's declared payload bounds.

### Unavailable archives

Package routing ownership is established before archive resolution/opening.

The modern registration outcomes are therefore:

- `Registered` — the archive was opened and indexed;
- `FileNotFound` — the declared package does not exist;
- `ArchiveUnavailable` — package resolution, file access, or structural archive validation failed;
- `DuplicatePrefix` — the native prefix hash was already owned by an earlier declaration.

`FileNotFound` and `ArchiveUnavailable` retain their routing hash exactly as the native package
object remains registered after `WdfHandler_OpenFile` failure. Lookups route to that unavailable
package identity and miss rather than falling through to a later colliding declaration.

Expected filesystem and archive-validation failures are contained at this boundary. Catastrophic CLR
failures and programming errors are not hidden by a blanket `catch (Exception)`.

### Payload overlap policy

The current reader does **not** reject overlap between two individually valid entry payload ranges.

Retail evidence shows packed contiguous payloads, but native evidence has not established that an
overlapping range is structurally rejected by the native reader.

Each individual entry is already constrained to the archive payload region and exposed through a
bounded entry stream, so overlap does not allow a read to escape the validated archive payload
boundary.

Overlap rejection will be added only if later native or format evidence establishes it as part of
the accepted WDF contract, or if a concrete safety requirement arises that cannot be satisfied by
the existing containment model.

It is therefore an explicit compatibility decision, not deferred cleanup.

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

1. first-party server-selection UI and the networking endpoint handoff;
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
- package prefix-hash registration and duplicate/collision semantics;
- missing and unavailable package behavior;
- native `Server.dat` key derivation and retail fixture identity;
- RSA/PKCS#1/gzip envelope validation;
- typed `outenserver` XML projection and row-index semantics;
- optional package-declaration failure behavior;
- verified WDF hash vectors;
- WDF header/index parsing and bounds;
- WDF entry-stream read and seek containment;
- configuration grammar;
- bitmap decoding;
- content-closure resolution;
- import determinism;
- manifest/payload/closure verification;
- malformed and adversarial inputs.

Tests must not require redistribution of large retail payload families. Small retail fixtures may be
tracked when they are necessary to permanently lock a parity-sensitive decoder and are already part
of the reviewed runtime content closure.

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
