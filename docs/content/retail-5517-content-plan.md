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

Current closure:

```text
files: 3,780
bytes: 20,762,596
```

The exact path set is maintained by:

```text
content/retail-5517/manifest.json
content/retail-5517/payload
```

The Markdown plan intentionally does not duplicate all 3,780 manifest entries. GFX-UI-006
introduced verified parametric quickbar consumers whose ANI catalogs expand into broad action,
magic, item-icon, and glow families; manually mirroring that path set here would create a second,
drift-prone source of truth.

Current consumer groups are:

| Consumer | Configuration / catalogs | Asset lookup |
| --- | --- | --- |
| Startup | `ini/GameSetUp.ini`, `ini/info.ini`, startup logos | configuration/logo-specific |
| Native GUI font | `ini/Font.ini` | `LooseOnly` |
| Main HUD fixed controls | `ani/Control.ani` | `LooseThenPackage` frames |
| Quickbar fixed controls | `ani/Control.ani` | `LooseThenPackage` frames |
| Quickbar magic / XP magic | `ani/Magic.ani` | `LooseThenPackage` |
| Quickbar item icons | `ani/ItemMinIcon.Ani` | `LooseThenPackage` |
| Quickbar glows | `ani/effect.ani` | `LooseThenPackage` |
| Selected skill | `Magic0` from `ani/Magic.ani`, `Image0` from `ani/Control.ani` | `LooseThenPackage` |
| Selected-skill cooldown | `[SelectMagicNum]` in `ini/info.ini` plus font/code-page configuration | configuration only |
| Main-HUD category-8 status hints | `ani/Control.ani` `[Dialog21]`, `ini/StrRes.ini`, three `ini/Progress*.rgn` files | `LooseThenPackage` backdrop; `LooseOnly` strings and regions |

`ini/FontSetting.ini` and `ini/CodePage.ini` are optional native configuration inputs. They are
absent from the clean retail 5517 root and therefore are not copied into the curated runtime set.
Their managed loaders preserve the verified missing-file defaults.

The curated set contains exactly the closure resolved by `ClientContentClosure`. WDF archives and
`ini/package.ini` are import-time sources/configuration and are not shipped.

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
one retail source and verifies its exact encoded SHA-256 before reference decoding.

The importer materializes selected bytes into the curated payload. Package provenance is not
preserved after import.

All ANI-declared HUD frames in the closure are required, including repeated paths and frames not
currently uploaded to the GPU.

### Quickbar Dependency Expansion

GFX-UI-006 adds fixed and parametric quickbar dependencies.

Fixed `Control.ani` requirements include:

```text
Compose_CoverPic
Swapuse_UsemainbBtn
Swapuse_SwapmainbBtn
Equip_AddPic

Main3_Num0Pic .. Main3_Num9Pic
Equip_Num0 .. Equip_Num9
```

The importer also follows verified parametric `Control.ani` families:

```text
ButtonA<decimal>
Action_Dance<decimal>Btn
```

Additional catalogs are runtime requirements:

```text
ani/Magic.ani
ani/ItemMinIcon.Ani
ani/effect.ani
```

From `Magic.ani`, the closure includes sections matching:

```text
MagicSkillType<decimal>
XpSkillType<decimal>
```

From `ItemMinIcon.Ani`, the closure includes:

```text
ItemDefault
Item<decimal>
```

From `effect.ani`, the verified glow families are:

```text
FireLight
RedLight
BlueLight
RoyalBlueLight
YellowLight
```

`data/main3/skill38.dds` is a verified missing retail frame and is explicitly excluded rather than
turning the known retail defect into an import failure.

This parametric dependency expansion is why the exact closure is tracked in the manifest instead of
being duplicated as a Markdown path table.

### Selected-Skill Dependencies

GFX-UI-007 adds:

```text
ani/Magic.ani
    └── Magic0
        └── data/main/MainImgMagic.dds

ani/Control.ani
    └── Image0
        └── data/main/ImageDisable.dds

ini/Font.ini
ini/info.ini [SelectMagicNum]
```

`FontSetting.ini` remains an optional loose override and is absent from clean retail 5517.

The selected-skill DDS frames use the same `LooseThenPackage` resolution and deterministic
materialization rules as the other ANI-backed HUD assets.

### Category-8 Status-Hint Dependencies

GFX-UI-008 adds `Dialog21` frame 0 (`data/main/MsgDlg.dds`) together with
`ini/StrRes.ini`, `ini/ProgressXp.rgn`, `ini/ProgressMp.rgn`, and
`ini/ProgressHp.rgn`. The backdrop resolves from `data.wdf`; the strings and
regions are loose retail files. These five dependencies contribute 128,677 bytes.
The manifest remains the authoritative source of exact runtime paths and hashes.

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

Compose_CoverPic
Swapuse_UsemainbBtn
Swapuse_SwapmainbBtn
Equip_AddPic
Main3_Num<0..9>Pic
Equip_Num<0..9>
ButtonA<decimal>
Action_Dance<decimal>Btn

MagicSkillType<decimal>
XpSkillType<decimal>
ItemDefault
Item<decimal>

FireLight
RedLight
BlueLight
RoyalBlueLight
YellowLight

Magic0
Image0
```

The parametric forms above describe section-selection contracts, not synthetic ANI names generated at
runtime. Only sections actually present in the verified retail catalogs contribute frames to the
closure.

The known missing retail `data/main3/skill38.dds` frame is excluded explicitly from quickbar closure
materialization.

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
