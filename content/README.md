# Retail Content Sets

Retail assets live outside the source assemblies and are grouped by immutable source version.

The checked-in `retail-5517/` set is intentionally **consumer-led**, not a bulk preservation of the
retail tree.

Its current contract is:

```text
implemented ClientContentClosure
        ==
manifest path set
        ==
physical payload path set
```

A file enters the repository content set only when an implemented client consumer requires it and
the corresponding slice has established its behavior, validation, and tests.

The current retail-5517 closure contains exactly:

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

`manifest.json` records the deterministic identity and integrity metadata for those five files.

This policy prevents unsupported retail families from entering the product simply because their
bytes are available. Content expansion follows real consumers rather than directory-sized imports.

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
- copies only required files through a staging directory;
- verifies copied lengths;
- hashes payload bytes during import;
- writes the manifest deterministically;
- publishes the completed set only after the closure succeeds.

It does **not** bulk-copy `ini/`, `data/`, `ani/`, or any other retail directory.

## Startup Validation

Validate the currently implemented startup consumers against either an authorized retail root or an
imported payload:

```bash
dotnet run --project tools/OpenConquer.Content.Tool -- \
  validate-startup \
  --content-root content/retail-5517/payload
```

## Content-Set Verification

Verify the checked-in set with:

```bash
dotnet run --project tools/OpenConquer.Content.Tool -- \
  verify-content-set \
  --content-set content/retail-5517
```

Verification requires all three views of the content set to agree:

1. the paths required by `ClientContentClosure`;
2. the paths declared by `manifest.json`;
3. the files physically present beneath `payload/`.

Manifest length, signature, and SHA-256 identities are then verified against the physical files.

Extra payload files, missing required files, undeclared files, and content-integrity changes all
fail verification.
