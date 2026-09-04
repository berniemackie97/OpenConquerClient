# Retail Content Sets

Retail assets live outside the source assemblies and are grouped by immutable source version.

The checked-in `retail-5517/` set is intentionally **consumer-led**, not a bulk preservation of the
retail tree.

Its contract is:

```text
implemented ClientContentClosure
        ==
manifest path set
        ==
physical payload path set
```

A file enters the runtime content set only when an implemented client consumer requires it and the
corresponding slice has established its behavior, validation, and tests.

The current retail-5517 runtime closure contains exactly:

```text
payload/
├── data/
│   └── main/
│       ├── Logo1.bmp
│       └── Logo2.bmp
└── ini/
    ├── GameSetUp.ini
    ├── info.ini
    └── package.ini
```

`manifest.json` records deterministic identity and integrity metadata for those five files.

The current runtime consumers cover:

- screen-mode configuration;
- startup-logo path selection and bitmap decoding;
- WDF package declaration and routing behavior.

Historical retail files retained only for compatibility research or offline tooling are not part of
this runtime closure.

In particular, retail `Server.dat` is intentionally excluded from:

- `ClientContentClosure`;
- `content/retail-5517/payload`;
- the runtime content manifest;
- published client runtime content.

Its exact audited retail fixture is instead preserved under:

```text
tests/OpenConquer.Content.Tool.Tests/TestData/retail-5517/Server.dat
```

and is consumed only by the offline legacy tooling boundary in `OpenConquer.Content.Tool`.

This policy prevents unsupported or historical retail files from entering the product simply because
their bytes are available. Content expansion follows real runtime consumers rather than
directory-sized imports.

## Reproducing an Import

From the repository root:

```bash
dotnet run --project tools/OpenConquer.Content.Tool -- \
  import-retail-5517 \
  --source /path/to/retail/5517 \
  --destination /path/to/new/retail-5517
```

The destination must not already exist.

The importer:

- validates the expected retail source identity;
- resolves the exact `ClientContentClosure`;
- rejects links and case-insensitive path collisions;
- copies only required runtime files through a staging directory;
- verifies copied lengths;
- hashes payload bytes during import;
- writes the manifest deterministically;
- publishes the completed set only after the closure succeeds.

It does **not** bulk-copy `ini/`, `data/`, `ani/`, historical compatibility fixtures, or any other
retail directory.

## Startup Validation

Validate the currently implemented runtime startup consumers against either an authorized retail
root or an imported payload:

```bash
dotnet run --project tools/OpenConquer.Content.Tool -- \
  validate-startup \
  --content-root content/retail-5517/payload
```

`validate-startup` covers only runtime startup consumers represented by the current content closure.

Historical `Server.dat` decoding is deliberately separate from startup validation.

## Legacy Server.dat Inspection

Inspect an explicit retail `Server.dat` file with:

```bash
dotnet run --project tools/OpenConquer.Content.Tool -- \
  inspect-server-dat \
  --file /path/to/Server.dat
```

The command:

- reads only the explicit filesystem path supplied by the operator;
- applies the audited bounded RSA/PKCS#1/gzip decoder;
- parses the verified `outenserver` XML structure;
- preserves historical `FlashName`, `FlashIcon`, `FlashHint`, `ServerName`, `ServerIP`, and
  `ServerPort` semantics;
- emits deterministic, escaped diagnostic output.

It does not use runtime `IClientContentSource` lookup, WDF fallback, or the modern realm/networking
model.

The exact retail 5517 fixture is parity-tested independently of the runtime content set.

See [`../docs/compatibility/server-dat.md`](../docs/compatibility/server-dat.md) for the native
evidence, security interpretation, and preservation policy.

## Content-Set Verification

Verify the checked-in runtime set with:

```bash
dotnet run --project tools/OpenConquer.Content.Tool -- \
  verify-content-set \
  --content-set content/retail-5517
```

Verification requires all three runtime views to agree:

1. the paths required by `ClientContentClosure`;
2. the paths declared by `manifest.json`;
3. the files physically present beneath `payload/`.

Manifest length, signature, and SHA-256 identities are then verified against the physical files.

Extra payload files, missing required files, undeclared files, and content-integrity changes all
fail verification.

A compatibility fixture such as `Server.dat` is intentionally outside this equality because it is
test/tooling evidence rather than shipped runtime content.
