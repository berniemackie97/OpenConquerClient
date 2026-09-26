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

Current runtime closure: 19 files.

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
│       └── mainDialog1.dds
└── ini/
    ├── GameSetUp.ini
    ├── info.ini
    └── package.ini
```

`manifest.json` records deterministic identity and integrity metadata for the complete closure.

Current consumers:

```text
screen-mode configuration
startup logos
WDF package registration

Progress45 HUD background
Dialog4 HUD panels

Progress40 life
Progress41 mana
Progress46 stamina
Progress47 extended stamina
```

`Control.Ani` is loose retail content.

HUD frame requirements use `LooseThenPackage` during import. The selected bytes are materialized into the curated payload regardless of retail loose/WDF provenance.

Actual winning loose-file casing is preserved. `Progress47` references `ProgressForce2A.dds`; the verified retail loose file is `ProgressForce2a.dds`.

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
