# Retail Content Sets

Retail assets live outside the source assemblies and are grouped by immutable source version.

`retail-5517/` contains:

- `manifest.json` — deterministic identity, integrity, format-signature, and disposition metadata;
- `payload/ini/` — the complete source-preserved retail INI/database family;
- `payload/data/` — the complete source-preserved loose data family;
- `payload/ani/` — the complete animation catalogs that index loose data assets.

Paths beneath `payload/` retain their retail identity and casing. Modern code accesses them through
virtual content paths and typed catalogs; it does not depend on their host filesystem location.

Ordinary builds stage only the files required by implemented runtime consumers. This prevents every
compile from copying the full content set while ensuring later feature slices draw from one audited,
complete family rather than ad hoc loose copies.

## Reproducing the import

From the repository root:

```bash
dotnet run --project tools/OpenConquer.Content.Tool -- \
  import-retail-5517 \
  --source /path/to/retail/5517 \
  --destination /path/to/new/retail-5517
```

The destination must not already exist. The importer validates the 5517 version marker, rejects
links and case-insensitive path collisions, copies bytes into a staging directory, hashes every file
during the copy, writes the manifest deterministically, and only then publishes the completed
content set.

Validate the implemented startup slice against either a retail root or an imported payload:

```bash
dotnet run --project tools/OpenConquer.Content.Tool -- \
  validate-startup \
  --content-root content/retail-5517/payload
```

Verify that every preserved file is declared, present, and byte-identical to its manifest identity:

```bash
dotnet run --project tools/OpenConquer.Content.Tool -- \
  verify-content-set \
  --content-set content/retail-5517
```
