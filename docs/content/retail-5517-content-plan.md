# Retail 5517 Content Ingestion Plan

## Decision Status

**Active and implemented incrementally.**

OpenConquer does not bulk-import retail directories.

The checked-in runtime content set is the exact dependency closure of implemented runtime consumers:

```text
ClientContentClosure.Resolve(payload)
        ==
manifest path keys
        ==
observed payload path keys
```

The runtime closure expands only when a reviewed implementation slice introduces a real runtime
consumer.

Historical retail artifacts may also be retained outside that closure when they are necessary for
compatibility research, parity testing, or offline tooling. Such fixtures do not become runtime
content merely because OpenConquer can decode them.

The full retail inventory remains documented in [retail-5517-inventory.md](retail-5517-inventory.md)
as compatibility evidence and planning input. Inventory does not imply that every surveyed file
belongs in the repository or the shipped client.

## Objective

Reconstruct the 5517 client from verified retail behavior while maintaining a secure,
cross-platform, production-grade content boundary.

The system must:

- preserve compatibility-sensitive retail path identities;
- consume authorized retail sources without mutating them;
- validate legacy data as untrusted input;
- keep source-format behavior separate from modern architecture;
- stage deterministic content for implemented runtime consumers;
- preserve selected historical artifacts separately when required for compatibility evidence;
- prevent unsupported assets from entering releases accidentally;
- allow the checked-in runtime closure to expand predictably as reconstruction proceeds.

## Governing Decisions

### Consumer-led migration

Runtime content enters the shipped content set because executable client code consumes it, not
because it shares a directory with something already supported.

Every runtime expansion slice must establish:

1. the consumer;
2. the native or retail behavior being preserved;
3. the content lookup mode;
4. the parser or decoder boundary;
5. malformed-input behavior;
6. tests;
7. the resulting `ClientContentClosure` change;
8. manifest and payload verification.

Directory-sized migration is explicitly rejected.

Compatibility fixtures follow a separate rule: they may be retained outside the runtime content set
when their exact bytes are necessary to lock parity-sensitive behavior or support explicit offline
inspection tooling.

### Retail paths are compatibility identities

Retail paths remain meaningful compatibility identifiers.

The modern content boundary may validate and normalize paths for safe lookup, but consumers do not
casually rename retail resources or reinterpret path structure.

Host filesystem containment is enforced separately from native virtual-path normalization.

Historical tooling may accept an explicit filesystem path when the artifact is not part of the
runtime content system.

### Native evidence determines compatibility behavior

The legacy reconstruction is useful for locating concepts and understanding historical flows, but it
is not authoritative architecture.

For parity-sensitive behavior, evidence priority is:

1. verified native 5517 behavior and resources;
2. retail payload evidence;
3. legacy reconstruction as supporting reference.

Unsafe native undefined behavior does not need to be reproduced.

Verified native behavior also does not require retaining an obsolete native deployment mechanism
when a modern architecture preserves the relevant compatibility intent more safely and cleanly.

### Legacy data is untrusted input

Known hashes establish identity, not safety.

Readers, importers, and compatibility tooling must still validate the requirements appropriate to
their inputs, including:

- lengths and counts before allocation;
- checked offset arithmetic;
- signatures and structural invariants;
- filesystem containment;
- symbolic links and reparse points where a rooted content boundary is involved;
- case-insensitive ambiguity where legacy path lookup is involved;
- archive boundaries;
- decoded dimensions and expansion;
- malformed text and binary inputs.

Modern safety limits may be stricter than the native implementation when they do not reject valid
retail 5517 data or change verified compatibility behavior.

## Current Implemented Closure

The checked-in `content/retail-5517` runtime payload currently contains:

```text
data/main/Logo1.bmp
data/main/Logo2.bmp
ini/GameSetUp.ini
ini/info.ini
ini/package.ini
```

These five files support the implemented runtime consumers:

- screen-mode configuration;
- package declaration registration;
- startup-logo path configuration;
- the two verified retail startup-logo variants.

Historical compatibility fixtures are not runtime content merely because the modern tooling can
decode them.

Retail `Server.dat` is therefore intentionally outside:

- `ClientContentClosure`;
- `content/retail-5517/payload`;
- the runtime manifest;
- published client runtime content.

Its exact audited fixture is preserved separately at:

```text
tests/OpenConquer.Content.Tool.Tests/TestData/retail-5517/Server.dat
```

That fixture exists for compatibility testing and offline inspection.

No `ani/`, map, C3, audio, login, realm-selection, or general UI families are checked in merely for
future use.

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

Typed runtime consumers sit above that boundary:

```text
GameSetupConfiguration
StartupLogoConfiguration
StartupLogo
WindowsBitmapReader
```

The application composes the content source; consumers do not know whether bytes came from a host
filesystem or WDF archive unless verified compatibility behavior requires a specific lookup mode.

Retail `Server.dat` is no longer a runtime Content consumer.

Its native loose-file lookup behavior remains documented compatibility evidence, while the preserved
decoder and historical schema live exclusively in offline tooling.

There is no runtime `Server.dat` loader, runtime fallback, or generic runtime server-catalog model.

## Current Tooling Boundary

`OpenConquer.Content.Tool` owns two distinct classes of tooling responsibility:

```text
runtime content-set tooling
        ├── import-retail-5517
        ├── validate-startup
        └── verify-content-set

legacy compatibility tooling
        └── inspect-server-dat
```

### Runtime content-set tooling

Import resolves `ClientContentClosure` against an authorized source tree and stages only files
required by implemented runtime consumers.

Verification requires:

```text
resolved runtime code closure
        ==
manifest path keys
        ==
observed runtime payload path keys
```

It additionally verifies expected length, format signature, and SHA-256 identity.

This makes both of the following invalid:

- adding a runtime payload file and manifest entry that no implemented runtime consumer requires;
- removing a required runtime file from both payload and manifest.

### Legacy compatibility tooling

Legacy compatibility tooling is deliberately outside the runtime content closure.

`inspect-server-dat` accepts an explicit filesystem path, applies the preserved hardened retail
decoder, and reports the historical catalog without involving:

- `IClientContentSource`;
- runtime loose/package lookup;
- WDF fallback;
- a modern realm model;
- runtime networking endpoint selection.

The exact audited retail fixture belongs to `OpenConquer.Content.Tool.Tests`, not to the shipped
runtime content set.

## Server.dat Policy

Retail 5517 `Server.dat` is preserved as compatibility evidence and offline tooling input only.

It is not modern runtime configuration.

The production boundary is:

```text
retail Server.dat
        │
        ├── exact audited fixture
        ├── hardened legacy decoder
        ├── parity tests
        └── inspect-server-dat

OpenConquer.Client runtime
        │
        └── no Server.dat dependency
```

There is intentionally no runtime fallback to `Server.dat`, no hard-coded replacement server list,
and no translation of the historical file into a modern runtime realm catalog.

Detailed native evidence, cryptographic interpretation, parser invariants, protocol significance,
and preservation rationale are maintained in
[`../compatibility/server-dat.md`](../compatibility/server-dat.md).

### Verified retail identity

The exact audited fixture is locked to:

```text
encrypted length: 2816 bytes
RSA blocks: 11 × 256 bytes

encrypted SHA-256:
0b4d366786aa4498c7e470f10fd8bca716bc1d6cbda1eb3894666183f8327a90

inflated XML length: 38819 bytes

inflated XML SHA-256:
5d6b00ff722a8b37aa2981affecd478aee73bdc22cdc498a25b700242b55c35a

groups: 14
servers: 94
```

The independently recovered native public key uses exponent `65537` and a 2048-bit modulus whose raw
256-byte SHA-256 is:

```text
76acb04b08190b129985f8dee2b466efcd686eb1662cb598bd1a8154cb9196f1
```

OpenConquer stores the independently verified final modulus directly rather than reproducing the
native constructor's seed/schedule/BIGNUM assembly in operational tooling.

Native key-construction evidence remains parity evidence rather than runtime architecture.

### Hardened decoder

The preserved envelope decoder validates:

- non-empty encrypted input;
- complete 256-byte RSA blocks;
- a maximum of 64 encrypted blocks;
- a correctly sized public modulus;
- RSA representatives strictly below the modulus;
- PKCS#1 type-1 prefix `00 01`;
- at least eight `FF` padding bytes;
- a required zero separator;
- a non-empty extracted chunk;
- gzip signature validity;
- a 1 MiB maximum inflated XML size;
- deterministic malformed-data failures.

The explicit-file reader additionally prevents an oversized encrypted file from being accumulated in
memory beyond the maximum encrypted envelope size.

### XML and row structure

The XML reader:

- prohibits DTDs;
- disables external resolution;
- bounds XML document characters;
- requires exactly one `table_data[name=outenserver]` table;
- rejects duplicate row IDs;
- rejects duplicate field names within a row;
- bounds group count;
- bounds each group to the native 100-row server stride;
- uses checked row-index arithmetic;
- rejects missing structurally required rows and fields.

The verified historical fields are:

```text
id
Child
FlashName
FlashIcon
FlashHint
ServerName
ServerIP
ServerPort
```

The tooling model deliberately preserves these source-format concepts rather than projecting them
into generic modern names such as `DisplayName`, `Host`, `Port`, `Realm`, or `Endpoint`.

The inspected retail fixture proves that distinction is necessary. Multiple rows have different
`FlashName` and `ServerName` values.

For example:

```text
FlashName="Water"       ServerName="Fire"
FlashName="Cerberus"    ServerName="Gryphon"
FlashName="Pegasus"     ServerName="Basilisk"
FlashName="Cinderella"  ServerName="SnowWhite"
```

`ServerName` also has verified native protocol significance and therefore cannot safely be collapsed
into presentation metadata.

### Modern architecture

The historical `ServerIP` and `ServerPort` values are preserved only as source text in tooling.

They are not converted into modern runtime endpoint configuration.

Modern account authentication, realm discovery, realm selection, routing, and connection
authorization will use modern authenticated service boundaries rather than a locally shipped server
list.

A future modern realm model must represent stable logical realm identity rather than expose
infrastructure IP addresses and ports as player-facing configuration.

`Server.dat` therefore remains valuable compatibility evidence without becoming a dependency of the
modern production architecture.

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

A future slice that needs another retail runtime asset follows this sequence:

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

The runtime content set is never expanded speculatively.

Historical compatibility fixtures follow their own evidence-driven ownership rule and must not be
inserted into `ClientContentClosure` simply because tooling consumes them.

## Planned Consumer-Led Expansion

Likely future runtime content areas, subject to actual reconstruction order, include:

1. authenticated realm-discovery and realm-selection presentation resources;
2. first-party login and core UI resources where they remain game-runtime responsibilities;
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
- path containment and link rejection for rooted host-content boundaries;
- no extraction to attacker-controlled paths;
- bounded decoded image/data dimensions;
- deterministic diagnostics;
- no silent fallback outside verified compatibility behavior.

Cleanup failures must not replace an existing primary failure.

Explicit offline tooling does not inherit runtime content lookup behavior unless that behavior is
itself the subject of the compatibility test.

## Test Strategy

Runtime Content tests should remain synthetic wherever practical.

They should cover:

- path validation;
- case-insensitive loose lookup;
- containment and symlink/reparse rejection;
- package prefix-hash registration and duplicate/collision semantics;
- missing and unavailable package behavior;
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

Offline legacy-tooling tests separately cover:

- native `Server.dat` public-key evidence;
- exact retail fixture identity;
- RSA/PKCS#1/gzip envelope validation;
- typed `outenserver` XML projection;
- native row-index semantics;
- source-field preservation;
- malformed and adversarial legacy input.

Tests must not require redistribution of large retail payload families.

Small compatibility fixtures may be tracked when necessary to permanently lock a parity-sensitive
format boundary. Such fixtures do not need to be part of the runtime content closure when their
consumer is test or offline tooling rather than the game runtime.

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

When a slice changes a preserved compatibility tool, that tool's exact audited fixture must also be
exercised explicitly.

For the retained `Server.dat` boundary:

```bash
dotnet run \
  --project tools/OpenConquer.Content.Tool \
  --configuration Release \
  --no-build \
  -- inspect-server-dat \
  --file tests/OpenConquer.Content.Tool.Tests/TestData/retail-5517/Server.dat
```

Published client output must contain the same manifest-approved runtime closure as the checked-in
content set.

Historical tooling fixtures such as `Server.dat` must not appear in published client runtime
content.

## Non-Goals

The content system is not:

- a bulk retail-file mirror;
- a general-purpose game-engine asset pipeline;
- an
