# Retail 5517 Content Plan

## Content Rules

Runtime content is driven by consumers: a file is included only because implemented code needs it.
These three sets must match:

```text
ClientContentClosure
        ==
manifest path keys
        ==
payload path keys
```

Path keys are compared case-insensitively. Manifest source paths keep the casing of the imported
source.

A retail asset joins the runtime set only when an implemented consumer needs it and the retail
client's dependency on it has been confirmed.

## Runtime Closure

Current closure:

```text
files: 3,787
bytes: 21,331,584
```

The full path list is kept in:

```text
content/retail-5517/manifest.json
content/retail-5517/payload
```

This document does not repeat all 3,785 manifest entries. The parametric quickbar consumers added in
GFX-UI-006 pull in whole families of action, magic, item-icon, and glow frames from their ANI
catalogs. Copying that list here would create a second list that drifts out of date.

Current consumer groups:

| Consumer | Configuration / catalogs | Asset lookup |
| --- | --- | --- |
| Startup | `ini/GameSetUp.ini`, `ini/info.ini`, startup logos | configuration/logo-specific |
| Login version label | `version.dat` (start.dat `setVer`) | `LooseOnly` |
| GUI font | `ini/Font.ini` | `LooseOnly` |
| World terrain index | `ini/GameMap.dat` | `LooseOnly` |
| Main HUD fixed controls | `ani/Control.ani` | `LooseThenPackage` frames |
| Quickbar fixed controls | `ani/Control.ani` | `LooseThenPackage` frames |
| Quickbar magic / XP magic | `ani/Magic.ani` | `LooseThenPackage` |
| Quickbar item icons | `ani/ItemMinIcon.Ani` | `LooseThenPackage` |
| Quickbar glows | `ani/effect.ani` | `LooseThenPackage` |
| Selected skill | `Magic0` from `ani/Magic.ani`, `Image0` from `ani/Control.ani` | `LooseThenPackage` |
| Selected-skill cooldown | `[SelectMagicNum]` in `ini/info.ini` plus font/code-page configuration | configuration only |
| Main-HUD category-8 status hints | `ani/Control.ani` `[Dialog21]`, `ini/StrRes.ini`, three `ini/Progress*.rgn` files | `LooseThenPackage` backdrop; `LooseOnly` strings and regions |
| Main-HUD category-9 Magic hints | `ini/MagicType.dat`, `ini/MagicEffect.ini`, `ini/SubProfessionInfo.ini`, configured `ini/Cn_Res.ini`; shared `ini/StrRes.ini` and `[Dialog21]` | `LooseOnly` metadata/strings; shared `LooseThenPackage` backdrop |

`ini/FontSetting.ini` and `ini/CodePage.ini` are optional retail configuration files. A clean retail
5517 root does not have them, so they are not copied into the curated runtime set. When the files
are missing, their managed loaders fall back to the same defaults as the retail client.

The curated set contains the closure resolved by `ClientContentClosure` and nothing else. WDF
archives and `ini/package.ini` are used only during import and are not shipped.

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

The experience bar is drawn with solid rectangles, as in the retail client, so it needs no retail
image.

Retail source of each file:

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

The conformance tests check the action-strip sources as a separate requirement.

The four main-HUD check controls are imported with `LooseThenPackage`. Rendering conformance
separately requires each of their eight frame paths to resolve from exactly one retail source, and
checks each file's encoded SHA-256 before the reference decode.

The importer writes the selected bytes into the curated payload. Whether a file came from a package
is not recorded after import.

Every ANI-declared HUD frame in the closure is required, including repeated paths and frames that
are not uploaded to the GPU today.

### Quickbar Dependencies

GFX-UI-006 adds fixed and parametric quickbar dependencies.

Fixed `Control.ani` dependencies include:

```text
Compose_CoverPic
Swapuse_UsemainbBtn
Swapuse_SwapmainbBtn
Equip_AddPic

Main3_Num0Pic .. Main3_Num9Pic
Equip_Num0 .. Equip_Num9
```

The importer also follows these parametric `Control.ani` families:

```text
ButtonA<decimal>
Action_Dance<decimal>Btn
```

These catalogs are also required at runtime:

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

From `effect.ani`, the closure includes these glow families:

```text
FireLight
RedLight
BlueLight
RoyalBlueLight
YellowLight
```

`data/main3/skill38.dds` is a frame the retail install is known to be missing. It is excluded so
that this retail defect does not make the import fail.

Because of this parametric expansion, the full closure is tracked in the manifest instead of a path
table in this document.

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

`FontSetting.ini` is still an optional loose override and is not present in a clean retail 5517
install.

The selected-skill DDS frames are resolved with `LooseThenPackage` and written to the payload by the
same reproducible rules as the other ANI-backed HUD assets.

### Category-8 Status-Hint Dependencies

GFX-UI-008 adds `Dialog21` frame 0 (`data/main/MsgDlg.dds`), `ini/StrRes.ini`, `ini/ProgressXp.rgn`,
`ini/ProgressMp.rgn`, and `ini/ProgressHp.rgn`. The backdrop comes from `data.wdf`. The strings and
regions are loose retail files. Together these five files add 128,677 bytes. The manifest is the
source of truth for runtime paths and hashes.

### Category-9 Magic-Hint Dependencies

Category-9 Magic hints load `ini/MagicType.dat`, `ini/MagicEffect.ini`, and
`ini/SubProfessionInfo.ini` with `LooseOnly`. In retail 5517, the configured keyed client-string
resource resolves to `ini/Cn_Res.ini`. `ini/StrRes.ini` and the `Dialog21` backdrop are shared with
the category-8 status hints.

The renderer handles learned-magic details and the special zero-magic cases. It wraps text on
encoded bytes as the retail client does, colors each line, and uses the 12-pixel magic font. It
reads a runtime snapshot, because there is no live Gameplay producer yet. Real-driver conformance
against independent references covers 800x600 and 1024x768. Other hint categories and control
tooltips are not implemented.

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

Each consumer declares its lookup mode.

`ClientContentClosure` defines the shipped dependency set.

## Import

`import-retail-5517` runs these steps:

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

Before it resolves package-backed files, the importer reads `ini/package.ini` from the authorized
retail source to register the WDF archives. That file is import configuration only. It is not a
runtime dependency and is not copied into the curated content set.

The importer does not bulk-copy retail directories or archives.

A failed import leaves no partial set published.

## WDF

Retail `ini/package.ini` lists the packages to register for import-time lookup.

Package lookup follows the retail rules:

```text
normalize declaration
derive package prefix
hash prefix
first registration wins
route virtual path by prefix hash
hash full virtual path for entry UID
read bounded WDF entry
```

Import validation also requires:

```text
bounded archive/index arithmetic
maximum 100000 entries
complete entry records
reserved DWORD = 0
strictly ascending unique UIDs
payload contained before index
no host-path links or reparse traversal
```

Where the retail client tolerates a declared package that is missing or unusable, registration does
not fail either.

The curated runtime set stores the resolved assets as loose files, so it needs neither
`ini/package.ini` nor the source WDF archives.

## ANI and Images

ANI files are read as retail-format compatibility data.

Image formats the closure needs (both supported in production):

```text
TGA
DDS / single-level DXT3
```

Decoders check the format structure before allocating memory and reject unsupported variants.

Current HUD ANI consumers:

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

The parametric forms above are patterns for selecting sections. The runtime does not generate ANI
names from them. Only sections that exist in the retail catalogs add frames to the closure.

The known missing retail frame `data/main3/skill38.dds` is left out when the quickbar closure is
materialized.

## Excluded Content

Reference material stays out of the runtime closure unless a production consumer needs it.

`Server.dat` is used only by tooling:

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

It checks:

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

Rendering conformance separately checks the identities and sources of the retail files used by the
implemented HUD consumers.

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

category-8 HUD status hints
category-9 learned-magic and zero-magic hints
category-9 retail magic/effect/string provenance
category-9 rendering at 800×600 and 1024×768

independent DXT3 reference decoding
exact framebuffer comparison
```

## Adding Content

To add retail content:

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

Content is never bulk-imported ahead of a consumer that needs it.

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

Graphics changes also need a real-driver conformance run against an authorized retail 5517 root.
