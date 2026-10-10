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

Current runtime closure: **3,784 files totaling 21,319,837 bytes**.

`manifest.json` is the authoritative, deterministic path, length, signature, and SHA-256
inventory for the curated `payload/`. The complete list is intentionally not duplicated in
Markdown; see [`../docs/content/retail-5517-content-plan.md`](../docs/content/retail-5517-content-plan.md).

Current consumer groups:

| Consumer | Retail inputs | Notes |
| --- | --- | --- |
| Startup | `ini/GameSetUp.ini`, `ini/info.ini`, startup logos | Verified retail startup requirements |
| Native text | `ini/Font.ini` and configuration defaults | Code-page/font behavior follows verified retail inputs |
| Main HUD | `ani/Control.ani`; background, vitals, skill/experience, action/check controls | Verified native frame geometry and state |
| Quickbar | `ani/Control.ani`, `ani/Magic.ani`, `ani/ItemMinIcon.Ani`, `ani/effect.ani` | Ten slots plus required action, magic, item-icon and glow families |
| Selected skill | `Magic0`, `Image0`, `[SelectMagicNum]` | Stateful image/cover and cooldown-text behavior |
| Category-8 status hints | `Dialog21`, `ini/StrRes.ini`, three `ini/Progress*.rgn` files | Backdrop, normal-font text and HUD hotspots |
| Category-9 Magic hints | `ini/MagicType.dat`, `ini/MagicEffect.ini`, `ini/SubProfessionInfo.ini`, configured `ini/Cn_Res.ini`; shared `StrRes.ini`/`Dialog21` | Learned-magic and zero-magic branches, native 12-pixel text, snapshot-backed rendering |

`Control.Ani` is loose retail content. ANI-backed frame requirements use
`LooseThenPackage` during import and their winning bytes are materialized into the payload.
Retail source casing is preserved when loose content wins lookup; conformance can assert
stricter, independently verified source provenance for individual assets. The experience
bar uses solid rectangles rather than an added retail image.

`ini/package.ini` registers WDF import sources but is not included in the runtime set.
Retail WDF archives are not shipped. Live Gameplay producers, other status-hint
categories, per-control tooltips, and downstream HUD effects remain deferred.

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
