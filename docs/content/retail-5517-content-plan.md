# Retail 5517 Content Plan

## Contract

Runtime content is consumer-led.

```text
ClientContentClosure
        ==
manifest path keys
        ==
payload path keys
```

Path-key comparison is case-insensitive. Manifest source paths preserve imported source casing.

Retail assets enter the runtime set only when an implemented consumer requires them and the native dependency has been verified.

## Runtime Closure

Current closure: 19 files.

| Path | Lookup |
| --- | --- |
| `Data/Main/Logo1.bmp` | `LooseOnly` |
| `Data/Main/Logo2.bmp` | `LooseOnly` |
| `ani/Control.ani` | `LooseOnly` |
| `data/main/ProgressBk.dds` | `LooseThenPackage` |
| `data/main/ProgressForce.dds` | `LooseThenPackage` |
| `data/main/ProgressForce2.dds` | `LooseThenPackage` |
| `data/main/ProgressForce2A.dds` | `LooseThenPackage` |
| `data/main/ProgressForceA.dds` | `LooseThenPackage` |
| `data/main/ProgressHP.dds` | `LooseThenPackage` |
| `data/main/ProgressHPA.dds` | `LooseThenPackage` |
| `data/main/ProgressHPH.dds` | `LooseThenPackage` |
| `data/main/ProgressMP.dds` | `LooseThenPackage` |
| `data/main/ProgressMPA.dds` | `LooseThenPackage` |
| `data/main/ProgressMPH.dds` | `LooseThenPackage` |
| `data/main/mainDialog1.dds` | `LooseThenPackage` |
| `data/main/mainDialog2.dds` | `LooseThenPackage` |
| `ini/GameSetUp.ini` | `LooseOnly` |
| `ini/info.ini` | `LooseOnly` |
| `ini/package.ini` | `LooseOnly` |

The curated set contains exactly these resolved dependencies. WDF archives are not shipped.

## HUD Dependency Chain

```text
ani/Control.ani
├── Progress40
│   ├── ProgressHP.dds
│   ├── ProgressHPA.dds
│   └── ProgressHPH.dds
├── Progress41
│   ├── ProgressMP.dds
│   ├── ProgressMPA.dds
│   └── ProgressMPH.dds
├── Progress45
│   └── ProgressBk.dds
├── Progress46
│   ├── ProgressForce.dds
│   └── ProgressForceA.dds
├── Progress47
│   ├── ProgressForce2.dds
│   └── ProgressForce2A.dds
└── Dialog4
    ├── mainDialog1.dds
    └── mainDialog2.dds
```

Verified retail provenance:

```text
ProgressBk.dds      → data.wdf
mainDialog1.dds     → data.wdf
mainDialog2.dds     → loose MainDialog2.dds

ProgressHP.dds      → data.wdf
ProgressHPA.dds     → data.wdf
ProgressHPH.dds     → data.wdf
ProgressMP.dds      → data.wdf
ProgressMPA.dds     → data.wdf
ProgressMPH.dds     → data.wdf
ProgressForce.dds   → data.wdf
ProgressForceA.dds  → data.wdf

ProgressForce2.dds  → loose
ProgressForce2A.dds → loose ProgressForce2a.dds
```

The importer materializes selected bytes into the curated payload. Package provenance is not preserved after import.

All ANI-declared HUD frames in the closure are required, including frames not currently uploaded to the GPU.

## Content Resolution

```text
ClientContentRoot
        +
PackagedClientContentSource
        ↓
LooseOnly
PackageOnly
LooseThenPackage
```

Consumers declare lookup mode explicitly.

`ClientContentClosure` defines the shipped dependency set.

## Import

`import-retail-5517`:

```text
validate retail version
resolve runtime closure
apply declared lookup modes
reject unsafe host paths and ambiguity
read required WDF entries only
preserve winning loose-file casing
materialize selected bytes
record length, SHA-256, signature
write deterministic manifest
publish only after complete success
```

The importer does not bulk-copy retail directories or archives.

Failed imports leave no published partial set.

## WDF

`ini/package.ini` defines package registration.

Verified compatibility rules:

```text
normalize declaration
derive package prefix
hash prefix
first registration wins
route virtual path by prefix hash
hash full virtual path for entry UID
read bounded WDF entry
```

Runtime validation additionally requires:

```text
bounded archive/index arithmetic
maximum 100000 entries
complete entry records
reserved DWORD = 0
strictly ascending unique UIDs
payload contained before index
no host-path links or reparse traversal
```

Missing or unusable declared packages remain non-fatal registrations where required by verified behavior.

## ANI and Images

ANI files are compatibility data.

Current production image support required by the closure:

```text
TGA
DDS / single-level DXT3
```

Decoders validate format structure before allocation and reject unsupported variants.

## Excluded Content

Compatibility evidence does not enter the runtime closure unless a production consumer requires it.

`Server.dat` remains tooling-only evidence:

```text
tests/OpenConquer.Content.Tool.Tests/TestData/retail-5517/Server.dat
```

It is excluded from:

```text
ClientContentClosure
content/retail-5517/payload
runtime manifest
published client content
```

## Verification

`verify-content-set` requires:

```text
runtime closure
        ==
manifest
        ==
payload
```

It verifies:

```text
required paths
no undeclared payload files
no manifest entries outside closure
path-key consistency
length
signature
SHA-256
manifest schema
```

## Expansion Rule

```text
verify native consumer
→ establish path and lookup mode
→ implement typed consumer
→ define malformed/missing behavior
→ add tests
→ extend ClientContentClosure
→ import exact dependencies
→ verify closure == manifest == payload
→ run relevant real-driver conformance
→ run release gate
```

No speculative bulk imports.

## Release Gate

```bash
dotnet restore OpenConquer.Client.slnx --locked-mode
dotnet format OpenConquer.Client.slnx --verify-no-changes --no-restore
dotnet build OpenConquer.Client.slnx --configuration Release --no-restore
dotnet test OpenConquer.Client.slnx --configuration Release --no-build --no-restore

dotnet run \
  --project tools/OpenConquer.Content.Tool \
  --configuration Release \
  --no-build \
  --no-restore \
  -- verify-content-set \
  --content-set content/retail-5517

git diff --check
```

Graphics slices also require real-driver conformance against an authorized retail 5517 root.
