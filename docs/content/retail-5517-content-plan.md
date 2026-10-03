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

Retail assets enter the runtime set only when an implemented consumer requires them and the native
dependency has been verified.

## Runtime Closure

Current closure: 58 files totaling 1,866,395 bytes.

| Path | Lookup |
| --- | --- |
| `Data/Main/Logo1.bmp` | `LooseOnly` |
| `Data/Main/Logo2.bmp` | `LooseOnly` |
| `ani/Control.ani` | `LooseOnly` |
| `data/interface/Style01/Action/MissionBtnClick.dds` | `LooseThenPackage` |
| `data/interface/Style01/Action/MissionBtnEmboss.dds` | `LooseThenPackage` |
| `data/interface/Style01/Action/MissionBtnNormal.dds` | `LooseThenPackage` |
| `data/main/ChatBtn.dds` | `LooseThenPackage` |
| `data/main/ChatBtnClick.dds` | `LooseThenPackage` |
| `data/main/GoodBtn.dds` | `LooseThenPackage` |
| `data/main/GoodBtnClick.dds` | `LooseThenPackage` |
| `data/main/GroupBtn.dds` | `LooseThenPackage` |
| `data/main/GroupBtnClick.dds` | `LooseThenPackage` |
| `data/main/LevWordBtn.dds` | `LooseThenPackage` |
| `data/main/LevWordBtnClick.dds` | `LooseThenPackage` |
| `data/main/MapChk1.dds` | `LooseThenPackage` |
| `data/main/MapChk2.dds` | `LooseThenPackage` |
| `data/main/NpcEquip.dds` | `LooseThenPackage` |
| `data/main/NpcEquipClick.dds` | `LooseThenPackage` |
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
| `data/main/ProgressPower.dds` | `LooseThenPackage` |
| `data/main/ProgressPowerH.dds` | `LooseThenPackage` |
| `data/main/OrganiseBtnClick.dds` | `LooseThenPackage` |
| `data/main/OrganiseBtnEmboss.dds` | `LooseThenPackage` |
| `data/main/OrganiseBtnNormal.dds` | `LooseThenPackage` |
| `data/main/OrganiseBtnUnClick.dds` | `LooseThenPackage` |
| `data/main/PkArre.dds` | `LooseThenPackage` |
| `data/main/PkArreClick.dds` | `LooseThenPackage` |
| `data/main/PkFree.dds` | `LooseThenPackage` |
| `data/main/PkFreeClick.dds` | `LooseThenPackage` |
| `data/main/PkGroup.dds` | `LooseThenPackage` |
| `data/main/PkGroupClick.dds` | `LooseThenPackage` |
| `data/main/PkSafe.dds` | `LooseThenPackage` |
| `data/main/PkSafeClick.dds` | `LooseThenPackage` |
| `data/main/QueryBtn.dds` | `LooseThenPackage` |
| `data/main/QueryBtnClick.dds` | `LooseThenPackage` |
| `data/main/RunChk1.dds` | `LooseThenPackage` |
| `data/main/RunChk2.dds` | `LooseThenPackage` |
| `data/main/ScreenMoveChk1.dds` | `LooseThenPackage` |
| `data/main/ScreenMoveChk2.dds` | `LooseThenPackage` |
| `data/main/SetBtn.dds` | `LooseThenPackage` |
| `data/main/SetBtnClick.dds` | `LooseThenPackage` |
| `data/main/SkillBtn.dds` | `LooseThenPackage` |
| `data/main/SkillBtnClick.dds` | `LooseThenPackage` |
| `data/main/SkillBtnL.dds` | `LooseThenPackage` |
| `data/main/mainDialog1.dds` | `LooseThenPackage` |
| `data/main/mainDialog2.dds` | `LooseThenPackage` |
| `ini/GameSetUp.ini` | `LooseOnly` |
| `ini/info.ini` | `LooseOnly` |

The curated set contains exactly these resolved dependencies. WDF archives and `ini/package.ini` are
not shipped.

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
├── Progress42
│   ├── ProgressPower.dds
│   ├── ProgressPower.dds
│   └── ProgressPowerH.dds
├── Progress45
│   └── ProgressBk.dds
├── Progress46
│   ├── ProgressForce.dds
│   └── ProgressForceA.dds
├── Progress47
│   ├── ProgressForce2.dds
│   └── ProgressForce2A.dds
├── Dialog4
│   ├── mainDialog1.dds
│   └── mainDialog2.dds
├── Check40
│   ├── RunChk1.dds
│   └── RunChk2.dds
├── Check43
│   ├── MapChk2.dds
│   └── MapChk1.dds
├── Check46
│   ├── ScreenMoveChk1.dds
│   └── ScreenMoveChk2.dds
├── Button411
│   ├── NpcEquip.dds
│   └── NpcEquipClick.dds
├── Button40
│   ├── QueryBtn.dds
│   └── QueryBtnClick.dds
├── Button410
│   ├── LevWordBtn.dds
│   └── LevWordBtnClick.dds
├── Button42
│   ├── GoodBtn.dds
│   └── GoodBtnClick.dds
├── Button43
│   ├── SetBtn.dds
│   └── SetBtnClick.dds
├── Main3_MissionBtn
│   ├── MissionBtnNormal.dds
│   ├── MissionBtnClick.dds
│   └── MissionBtnEmboss.dds
├── Button45
│   ├── ChatBtn.dds
│   └── ChatBtnClick.dds
├── Button46
│   ├── GroupBtn.dds
│   └── GroupBtnClick.dds
├── Button47
│   ├── PkFree.dds
│   └── PkFreeClick.dds
├── Button49
│   ├── PkSafe.dds
│   └── PkSafeClick.dds
├── Button48
│   ├── PkGroup.dds
│   └── PkGroupClick.dds
├── Button412
│   ├── PkArre.dds
│   └── PkArreClick.dds
├── Main3_OrganiseBtn
│   ├── OrganiseBtnNormal.dds
│   ├── OrganiseBtnClick.dds
│   ├── OrganiseBtnUnClick.dds
│   └── OrganiseBtnEmboss.dds
└── Button41
    ├── SkillBtn.dds
    ├── SkillBtnClick.dds
    └── SkillBtnL.dds
```

The experience bar is rendered from native solid-rectangle behavior and therefore adds no retail
image dependency.

Verified retail provenance:

```text
Control.Ani         → loose

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

ProgressPower.dds   → data.wdf
ProgressPowerH.dds  → data.wdf

Button40 frames     → data.wdf
Button410 frames    → data.wdf
Button42 frames     → data.wdf
Button43 frames     → data.wdf
Button45 frames     → data.wdf
Button46 frames     → data.wdf
Button47 frames     → data.wdf
Button49 frames     → data.wdf
Button48 frames     → data.wdf
Button412 frames    → data.wdf
Button41 frames     → data.wdf

Main3_MissionBtn frames  → loose
Main3_OrganiseBtn frames → loose
```

The action-strip provenance assertions are independent conformance requirements.

For the four main-HUD check controls, production import remains `LooseThenPackage`. Rendering
conformance independently requires each of the eight verified frame paths to resolve from exactly
one retail source and verifies its exact encoded SHA-256 before reference decoding. Production import
still resolves ANI frame requirements through `LooseThenPackage`.

The importer materializes selected bytes into the curated payload. Package provenance is not
preserved after import.

All ANI-declared HUD frames in the closure are required, including repeated paths and frames not
currently uploaded to the GPU.

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

The importer opens `ini/package.ini` from the authorized retail source to register WDF archives
before resolving package-backed requirements. That file is import configuration, not a runtime
dependency, and is not copied into the curated content set.

The importer does not bulk-copy retail directories or archives.

Failed imports leave no published partial set.

## WDF

Retail `ini/package.ini` defines package registration for import-time resolution.

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

Import validation additionally requires:

```text
bounded archive/index arithmetic
maximum 100000 entries
complete entry records
reserved DWORD = 0
strictly ascending unique UIDs
payload contained before index
no host-path links or reparse traversal
```

Missing or unusable declared packages remain non-fatal registrations where required by verified
behavior.

The curated runtime set contains the resolved asset bytes as loose files and therefore does not
require `ini/package.ini` or the source WDF archives.

## ANI and Images

ANI files are compatibility data.

Current production image support required by the closure:

```text
TGA
DDS / single-level DXT3
```

Decoders validate format structure before allocation and reject unsupported variants.

Current HUD ANI consumers include:

```text
Progress40
Progress41
Progress42
Progress45
Progress46
Progress47
Dialog4

Button40
Button410
Button42
Button43
Main3_MissionBtn
Button45
Button46
Button47
Button49
Button48
Button412
Main3_OrganiseBtn
Button41

Check40
Check43
Check46
Button411
```

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

Rendering conformance separately verifies exact retail identities and provenance for the implemented
HUD consumers.

Current graphics conformance covers:

```text
HUD chrome
HUD vitals
skill / experience HUD
10-button action strip
alternate PK skins
ANI frame modulo behavior

four main-HUD CMyCheck controls
two-state CMyCheck frame selection
native check-control geometry and draw order
natural 32×32 check-control rendering
exact check-control retail frame hashes

independent DXT3 reference decoding
exact framebuffer comparison
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
