# Retail 5517 Content Plan

## Status

Active and consumer-led.

OpenConquer does not bulk-import the retail client. Runtime content is added only when implemented
client code requires it and the corresponding native behavior has been established.

The runtime invariant is:

```text
ClientContentClosure
        ==
manifest path keys
        ==
physical payload path keys
```

Path-key comparison is case-insensitive. Manifest source paths preserve the casing selected during
import.

## Goals

The content boundary must:

- preserve compatibility-sensitive retail paths;
- support verified loose and WDF-backed lookup behavior;
- treat retail data as untrusted input;
- keep runtime content separate from compatibility-only fixtures;
- produce deterministic curated content sets;
- expand only with implemented runtime consumers.

Native evidence defines compatibility behavior. The modern architecture does not need to reproduce
obsolete native deployment mechanisms when the observable behavior can be preserved more safely.

## Runtime Closure

The current `retail-5517` closure contains nine requirements:

```text
Data/Main/Logo1.bmp                 LooseOnly
Data/Main/Logo2.bmp                 LooseOnly
ani/Control.ani                     LooseOnly
data/main/ProgressBk.dds            LooseThenPackage
data/main/mainDialog1.dds           LooseThenPackage
data/main/mainDialog2.dds           LooseThenPackage
ini/GameSetUp.ini                   LooseOnly
ini/info.ini                        LooseOnly
ini/package.ini                     LooseOnly
```

The checked-in payload is:

```text
payload/
├── ani/
│   └── Control.Ani
├── data/
│   └── main/
│       ├── Logo1.bmp
│       ├── Logo2.bmp
│       ├── MainDialog2.dds
│       ├── ProgressBk.dds
│       └── mainDialog1.dds
└── ini/
    ├── GameSetUp.ini
    ├── info.ini
    └── package.ini
```

These files support:

- logical screen-mode configuration;
- startup-logo configuration and decoding;
- WDF package registration;
- static `Progress45` main-HUD background rendering;
- static `Dialog4` main-HUD panel rendering.

## HUD Content Lookup

The verified 5517 HUD dependency chain is:

```text
ani/Control.ani
    ├── [Progress45]
    │       └── Frame0=data/main/ProgressBk.dds
    └── [Dialog4]
            ├── Frame0=data/main/mainDialog1.dds
            └── Frame1=data/main/mainDialog2.dds
```

`Control.ani` is required through `LooseOnly`.

The three DDS frame paths use `LooseThenPackage`.

In the audited clean retail source:

```text
ProgressBk.dds      -> data.wdf
mainDialog1.dds     -> data.wdf
mainDialog2.dds     -> loose MainDialog2.dds
```

Import resolves those requirements through the production content source and materializes the
selected bytes into the curated payload.

Package provenance is therefore an import-time compatibility concern. The shipped curated set does
not preserve WDF storage merely to preserve WDF storage.

Detailed native rendering evidence belongs in
[`../compatibility/native-graphics.md`](../compatibility/native-graphics.md).

## Content Boundary

Runtime lookup is composed from:

```text
ClientContentRoot
        │
        └── contained case-insensitive loose lookup

PackagedClientContentSource
        │
        ├── LooseOnly
        ├── PackageOnly
        └── LooseThenPackage
```

Consumers declare the lookup behavior they require. There is no universal loose/package precedence
outside that requirement.

`ClientContentClosure` describes the exact set needed by implemented runtime consumers.

The importer resolves that closure against an authorized retail root and writes a self-contained
curated set.

## Import Policy

`import-retail-5517`:

- validates the retail version marker;
- resolves the runtime closure;
- honors each requirement's lookup mode;
- rejects unsafe host paths, links, and case-insensitive ambiguity;
- reads package-backed content without extracting whole WDF archives;
- preserves the actual source casing when a loose file wins;
- materializes package-backed entries under their virtual content paths;
- hashes bytes while copying;
- writes the manifest deterministically;
- publishes only after the complete import succeeds.

The importer does not bulk-copy retail directories or WDF archives.

A failed import must not leave a published partial content set.

## WDF Policy

`ini/package.ini` is interpreted using the verified native package-registration behavior.

The runtime WDF boundary preserves the compatibility behavior required by current consumers:

```text
package declaration
        ↓
normalized prefix
        ↓
native prefix hash
        ↓
registered WDF
        ↓
virtual-path hash
        ↓
bounded entry stream
```

Important invariants are:

- registration is first-wins by native prefix hash;
- a missing or unavailable first package still owns its routing hash;
- later colliding declarations do not replace it;
- archive headers and entry tables are bounded and validated;
- entry UIDs must be strictly ascending and unique;
- entry streams cannot escape their declared payload range.

Modern resource limits are allowed when they do not reject valid audited retail content.

## Image and ANI Policy

ANI files are parsed as compatibility data, not application configuration.

Frame paths remain retail virtual paths and are resolved through an explicit lookup mode.

The production image boundary currently supports the formats required by implemented and verified
consumers, including:

```text
ANI frame
    ├── TGA
    └── DDS / DXT3
            ↓
        top-left RGBA
```

DDS decoding validates the supported single-level DXT3 contract before allocation and rejects
unsupported or malformed structures.

Production decoder correctness is tested separately from real-driver rendering conformance.

## Compatibility-Only Content

A retail artifact does not enter `ClientContentClosure` merely because tooling can read it.

`Server.dat` is the current explicit example.

It is excluded from:

```text
ClientContentClosure
content/retail-5517/payload
runtime manifest
published client content
```

Its audited fixture is owned by offline tooling tests:

```text
tests/OpenConquer.Content.Tool.Tests/TestData/retail-5517/Server.dat
```

Detailed `Server.dat` behavior and preservation policy belong in
[`../compatibility/server-dat.md`](../compatibility/server-dat.md).

## Verification

`verify-content-set` requires agreement between:

```text
runtime closure
        ==
manifest
        ==
payload
```

It also verifies declared length, signature, and SHA-256 identity.

The verifier rejects:

- missing required files;
- undeclared payload files;
- manifest entries outside the closure;
- path-key inconsistencies;
- changed lengths;
- changed bytes;
- changed format signatures;
- unsupported manifest versions.

## Expansion Rule

Every runtime content expansion follows:

```text
audit native consumer
        ↓
establish retail dependency and lookup mode
        ↓
implement typed production consumer
        ↓
define malformed and missing-input behavior
        ↓
add focused tests
        ↓
extend ClientContentClosure
        ↓
import exact selected dependencies
        ↓
verify closure == manifest == payload
        ↓
run relevant real-driver conformance
        ↓
run release gate
```

Content is not imported speculatively for future features.

## Release Gate

A content-affecting slice must pass:

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

Graphics slices that change compatibility-sensitive rendering or content must also run the
real-driver conformance suite against an authorized retail root:

```bash
dotnet run \
  --project tests/Conformance/OpenConquer.Rendering.Conformance/OpenConquer.Rendering.Conformance.csproj \
  --configuration Release \
  --no-build \
  --no-restore \
  -- \
  --content-root <authorized-retail-5517-root>
```

## Non-Goals

The content system is not:

- a retail-client mirror;
- a general extraction tool;
- a place to retain unused assets;
- a reason to copy native architecture;
- a runtime home for compatibility-only historical data.

Its job is to provide the smallest deterministic content boundary required by implemented
OpenConquer client behavior.
