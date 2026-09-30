# Retail Content Sets

Runtime retail content is consumer-led and versioned by source set.

Invariant:

```text
ClientContentClosure
        ==
manifest path keys
        ==
payload path keys
```

Content enters the runtime set only for an implemented, verified consumer.

## retail-5517

Current runtime closure: 50 files totaling 1,857,179 bytes.

```text
payload/
├── ani/
│   └── Control.Ani
├── data/
│   ├── interface/
│   │   └── Style01/
│   │       └── Action/
│   │           ├── MissionBtnClick.dds
│   │           ├── MissionBtnEmboss.dds
│   │           └── MissionBtnNormal.dds
│   └── main/
│       ├── ChatBtn.dds
│       ├── ChatBtnClick.dds
│       ├── GoodBtn.dds
│       ├── GoodBtnClick.dds
│       ├── GroupBtn.dds
│       ├── GroupBtnClick.dds
│       ├── LevWordBtn.dds
│       ├── LevWordBtnClick.dds
│       ├── Logo1.bmp
│       ├── Logo2.bmp
│       ├── MainDialog2.dds
│       ├── OrganiseBtnClick.dds
│       ├── OrganiseBtnEmboss.dds
│       ├── OrganiseBtnNormal.dds
│       ├── OrganiseBtnUnClick.dds
│       ├── PkArre.dds
│       ├── PkArreClick.dds
│       ├── PkFree.dds
│       ├── PkFreeClick.dds
│       ├── PkGroup.dds
│       ├── PkGroupClick.dds
│       ├── PkSafe.dds
│       ├── PkSafeClick.dds
│       ├── ProgressBk.dds
│       ├── ProgressForce.dds
│       ├── ProgressForce2.dds
│       ├── ProgressForce2a.dds
│       ├── ProgressForceA.dds
│       ├── ProgressHP.dds
│       ├── ProgressHPA.dds
│       ├── ProgressHPH.dds
│       ├── ProgressMP.dds
│       ├── ProgressMPA.dds
│       ├── ProgressMPH.dds
│       ├── ProgressPower.dds
│       ├── ProgressPowerH.dds
│       ├── QueryBtn.dds
│       ├── QueryBtnClick.dds
│       ├── SetBtn.dds
│       ├── SetBtnClick.dds
│       ├── SkillBtn.dds
│       ├── SkillBtnClick.dds
│       ├── SkillBtnL.dds
│       └── mainDialog1.dds
└── ini/
    ├── GameSetUp.ini
    └── info.ini
```

`manifest.json` records deterministic identity and integrity metadata for the complete closure.

Current consumers:

```text
screen-mode configuration
startup logos

Progress45 HUD background
Dialog4 HUD panels

Progress40 life
Progress41 mana
Progress42 skill
Progress46 stamina
Progress47 extended stamina

main-HUD experience rendering
10-button main-HUD action strip
PK-mode button skins
PK timed blinking
Organise timed blinking
```

`Control.Ani` is loose retail content.

HUD frame requirements use `LooseThenPackage` during import. The selected bytes are materialized into the curated payload regardless of retail loose/WDF provenance.

Verified action-strip provenance is checked independently by rendering conformance:

```text
Control.Ani
→ loose

Main3_MissionBtn frames
→ loose

Main3_OrganiseBtn frames
→ loose

Button40
Button410
Button42
Button43
Button45
Button46
Button47
Button49
Button48
Button412
Button41
→ package
```

Actual winning loose-file casing is preserved. `Progress47` references `ProgressForce2A.dds`; the verified retail loose file is `ProgressForce2a.dds`.

`ini/package.ini` is retail import configuration used to resolve WDF-backed requirements from the authorized source tree. It is not part of the curated runtime closure.

WDF archives are import sources and are not shipped.

## Import

```bash
dotnet run --project tools/OpenConquer.Content.Tool -- \
  import-retail-5517 \
  --source /path/to/retail/5517 \
  --destination /path/to/new/retail-5517
```

The destination must not exist.

Importer contract:

```text
validate retail version
resolve ClientContentClosure
honor lookup mode
reject unsafe paths and ambiguity
read required package entries only
preserve winning loose casing
copy through staging
record length, signature, SHA-256
write deterministic manifest
publish only after complete success
```

No bulk retail directories or WDF archives are copied.

## Verify

```bash
dotnet run --project tools/OpenConquer.Content.Tool -- \
  verify-content-set \
  --content-set content/retail-5517
```

Verification requires:

```text
closure == manifest == payload
```

It verifies path identity, manifest schema, length, signature, and SHA-256.

## Startup Validation

```bash
dotnet run --project tools/OpenConquer.Content.Tool -- \
  validate-startup \
  --content-root content/retail-5517/payload
```

## Compatibility-Only Content

Historical evidence does not enter the runtime closure without a production consumer.

Retail `Server.dat` is tooling-only:

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

Inspect explicitly:

```bash
dotnet run --project tools/OpenConquer.Content.Tool -- \
  inspect-server-dat \
  --file /path/to/Server.dat
```

See [`../docs/compatibility/server-dat.md`](../docs/compatibility/server-dat.md).
