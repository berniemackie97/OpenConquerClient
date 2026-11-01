# Retail Content Sets

Runtime retail content is organized and versioned by source set.

These three path sets must always match:

```text
ClientContentClosure
        ==
manifest path keys
        ==
payload path keys
```

A file enters the runtime set only when an implemented consumer needs it and that consumer's
behavior has been verified.

## retail-5517

`manifest.json` lists the path, length, signature, and SHA-256 hash of every file in the curated
`payload/`. It is the source of truth for the file list and is written deterministically. The full
list is not repeated in Markdown. See
[`../docs/content/retail-5517-content-plan.md`](../docs/content/retail-5517-content-plan.md).

Current consumer groups:

| Consumer | Retail inputs | Notes |
| --- | --- | --- |
| Startup | `ini/GameSetUp.ini`, `ini/info.ini`, startup logos | Startup requirements confirmed against retail |
| Text rendering | `ini/Font.ini` and configuration defaults | Code-page and font behavior follow the confirmed retail inputs |
| Main HUD | `ani/Control.ani`; background, vitals, skill/experience, action/check controls | Confirmed retail frame geometry and state |
| Quickbar | `ani/Control.ani`, `ani/Magic.ani`, `ani/ItemMinIcon.Ani`, `ani/effect.ani` | Ten slots plus the action, magic, item-icon and glow families they need |
| Selected skill | `Magic0`, `Image0`, `[SelectMagicNum]` | Stateful image/cover and cooldown-text behavior |
| Category-8 status hints | `Dialog21`, `ini/StrRes.ini`, three `ini/Progress*.rgn` files | Backdrop, normal-font text and HUD hotspots |
| Category-9 Magic hints | `ini/MagicType.dat`, `ini/MagicEffect.ini`, `ini/SubProfessionInfo.ini`, configured `ini/Cn_Res.ini`; shared `StrRes.ini`/`Dialog21` | Learned-magic and zero-magic branches, retail 12-pixel text, rendering from snapshots |

`Control.Ani` is loose retail content, meaning a file on disk rather than an entry in a WDF package.
During import, ANI frame requirements are resolved with `LooseThenPackage`, and the bytes of the
file that wins the lookup are copied into the payload. When a loose file wins, its retail casing is
kept. Conformance tests can also require a specific, independently confirmed source for individual
assets. The experience bar is drawn with solid rectangles, so it needs no retail image.

`ini/package.ini` registers the WDF archives used as import sources. It is not part of the runtime
set, and the retail WDF archives are not shipped. Live Gameplay producers, other status-hint
categories, per-control tooltips, and downstream HUD effects are not implemented yet.

## Import

```bash
dotnet run --project tools/OpenConquer.Content.Tool -- \
  import-client-content \
  --source /path/to/retail/5517 \
  --destination /path/to/new/retail-5517
```

The destination must not exist yet.

The importer performs these steps:

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

It never copies whole retail directories or WDF archives.

## Verify

```bash
dotnet run --project tools/OpenConquer.Content.Tool -- \
  verify-content-set \
  --content-set content/retail-5517
```

Verification requires all three sets to match:

```text
closure == manifest == payload
```

It checks path identity, the manifest schema, and each file's length, signature, and SHA-256 hash.

## Startup Validation

```bash
dotnet run --project tools/OpenConquer.Content.Tool -- \
  validate-startup \
  --content-root content/retail-5517/payload
```

## Compatibility-Only Content

Retail files kept only as research references stay out of the runtime closure unless production
code uses them.

Retail `Server.dat` is used only by tooling and is stored at:

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

To inspect it:

```bash
dotnet run --project tools/OpenConquer.Content.Tool -- \
  inspect-server-dat \
  --file /path/to/Server.dat
```

See [`../docs/compatibility/server-dat.md`](../docs/compatibility/server-dat.md).
