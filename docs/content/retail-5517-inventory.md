# Retail 5517 Content Inventory

## Purpose

This document records the first evidence-based survey of the native Windows retail client content
used to reconstruct OpenConquer Client. It is an inventory, not a statement that every retail file
belongs in the modern runtime.

The source tree was inspected read-only. Counts and byte totals below describe loose files only;
entries stored inside WDF packages are not included.

Historical artifacts may be retained independently as compatibility fixtures when their exact bytes
are needed for parity testing or offline inspection. That does not make them part of the production
runtime content set.

## Evidence Labels

- **Observed** means the value came directly from the supplied retail files.
- **Inferred** means the role follows from names, signatures, or references and still requires
  confirmation against native behavior.
- **Unknown** means implementation must stop at an evidence gate rather than invent behavior.

## Source Identity

The surveyed source identifies itself as retail version `5517` through the four ASCII bytes in
`version.dat`.

Selected source fingerprints:

| Retail path                |       Bytes | SHA-256                                                            |
| -------------------------- | ----------: | ------------------------------------------------------------------ |
| `version.dat`              |           4 | `5078bca8d0b0f7f9d30f3c1883e2b42f6d616761d312331a6ca7dd5776d590a8` |
| `ini/GameSetUp.ini`        |         157 | `d32d919e831a52fb8942ca51492df34add95ea64530307f1fddd3ef07cfcd492` |
| `Server.dat`               |       2,816 | `0b4d366786aa4498c7e470f10fd8bca716bc1d6cbda1eb3894666183f8327a90` |
| `ini/DefaultGameSetup.ini` |          50 | `ac8263fb13bc46319fe75736ddb1892108c093101b5f098c3fcba6b447c1d986` |
| `ini/package.ini`          |          29 | `511028e125d43635d90f777806b0f0fee65895b5b1a604cf278001fee536a8f2` |
| `ini/GameMap.dat`          |      10,876 | `0676b7d6969dd8277438704a289b1f57e8debb89fe5d3f912819c3a7880686a0` |
| `data.wdf`                 | 392,245,257 | `fc628e4adeb7de48b0b7cde3c7793c7177a289a6defe9b90a33a1f683dcdb132` |
| `c3.wdf`                   | 359,069,116 | `ab68f57cc24ae10052031583ce7aa4676247fe8dde5a7bdd98e572adcf7b7243` |

These fingerprints identify the surveyed inputs without embedding a workstation-specific absolute
path in project metadata.

The checked-in runtime content manifest intentionally covers only the current consumer-led runtime
dependency closure. `Server.dat` remains listed here because this document records the surveyed
retail source, but its exact fixture is now retained separately as compatibility evidence rather
than as runtime content.

## Scope Summary

| Retail area |  Files | Directories, including root |       Bytes | This slice                                   |
| ----------- | -----: | --------------------------: | ----------: | -------------------------------------------- |
| `ini/`      |    331 |                           3 |  28,557,142 | Full inventory and classification            |
| `data/`     | 12,777 |                         357 | 195,662,666 | Full inventory by payload family             |
| `ani/`      |     54 |                           1 |   8,483,019 | Included because it indexes `data/` payloads |
| `map/`      |    977 |                           6 | 403,781,216 | Dependency boundary only                     |
| `c3/`       | 11,886 |                       1,207 | 393,769,348 | Dependency boundary only                     |
| `sound/`    |    527 |                           1 |  74,200,387 | Dependency boundary only                     |
| `Help/`     |    169 |                           2 |   4,231,626 | Deferred; legacy help application content    |

The inspected `ini/`, `data/`, `ani/`, and `map/` paths contain no symbolic links, whitespace or
non-ASCII path characters, or case-folded path collisions. The longest relative path in that set is
67 characters. Those observations describe this snapshot; import validation must still enforce the
same invariants for every source.

## Root Bootstrap Data

### `Server.dat`

Retail `Server.dat` is verified compatibility evidence for the native 5517 login/server bootstrap
flow.

It is **not** part of the modern OpenConquer client runtime content closure.

The production ownership is now:

```text
retail Server.dat
        │
        ├── exact preserved fixture
        ├── compatibility tests
        └── offline inspect-server-dat tooling

OpenConquer.Client runtime
        │
        └── no Server.dat dependency
```

The exact audited fixture is retained at:

```text
tests/OpenConquer.Content.Tool.Tests/TestData/retail-5517/Server.dat
```

Native analysis establishes that retail 5517 reads the file directly from the client root rather
than through WDF package routing. That remains historical compatibility evidence, but there is no
modern runtime `LooseOnly` consumer because the production client no longer reads the file.

Verified retail identity:

```text
length: 2816 bytes
SHA-256: 0b4d366786aa4498c7e470f10fd8bca716bc1d6cbda1eb3894666183f8327a90
RSA blocks: 11 × 256 bytes
```

Native `CConfigDataTableQueryProvider` constructs a 2048-bit RSA public key with exponent `65537`.

The independently recovered unsigned big-endian modulus has raw-byte SHA-256:

```text
76acb04b08190b129985f8dee2b466efcd686eb1662cb598bd1a8154cb9196f1
```

The verified native decode path is:

```text
11 × 256-byte RSA blocks
        ↓
public RSA operation
        ↓
PKCS#1 type-1 extraction
        ↓
2495-byte concatenated gzip payload
        ↓
38819-byte XML
        ↓
table_data[name=outenserver]
```

The inflated XML SHA-256 is:

```text
5d6b00ff722a8b37aa2981affecd478aee73bdc22cdc498a25b700242b55c35a
```

The verified schema exposes:

```text
id
Child
FlashName
FlashIcon
FlashHint
ServerName
ServerIP
ServerPort
```

Root row `id=0` declares 14 groups.

Group rows use IDs `1..14`; server rows begin at:

```text
0x65 + (groupIndex - 1) * 0x64
```

with `Child` on the group row defining that group's server count.

The 100-row stride is therefore structural evidence and the preserved tooling decoder rejects a
per-group count above 100.

The exact fixture currently decodes to:

```text
groups: 14
servers: 94
```

The tooling model intentionally preserves historical field semantics rather than normalizing them
into a modern runtime server or realm model.

That distinction is observable in the retail data itself. Multiple rows have different `FlashName`
and `ServerName` values, including:

```text
FlashName="Water"       ServerName="Fire"
FlashName="Cerberus"    ServerName="Gryphon"
FlashName="Pegasus"     ServerName="Basilisk"
FlashName="Cinderella"  ServerName="SnowWhite"
```

`ServerName` also has separate verified protocol significance in the native login boundary, so it
must not be conflated with presentation metadata.

Historical `ServerIP` and `ServerPort` are preserved as source text in tooling only. They are not
converted into modern runtime endpoints, and the inspection command never initiates a connection.

Detailed cryptographic, parser, protocol, and modern-ownership rationale is maintained in
[`../compatibility/server-dat.md`](../compatibility/server-dat.md).

## `ini/` Inventory

The directory name is misleading: `ini/` is a mixed database containing text configuration, compiled
tables, binary records, region data, and terrain-effect payloads.

| Extension | Files |     Bytes | Observed character                                   |
| --------- | ----: | --------: | ---------------------------------------------------- |
| `.dbc`    |    14 | 9,876,089 | Binary compiled tables                               |
| `.ini`    |   131 | 9,394,606 | Mostly sectioned or line-oriented text; 14 are empty |
| `.dat`    |    27 | 5,780,104 | Multiple unrelated binary formats; one empty file    |
| `.wdb`    |     1 | 3,434,463 | Binary database                                      |
| `.tme`    |   155 |    68,444 | Binary terrain-magic records                         |
| `.rgn`    |     3 |     3,436 | Binary region geometry                               |

### Functional families

These are planning categories, not claims about native class ownership.

| Family                           | Representative retail files                                                                                    | Proposed logical ownership                                       |
| -------------------------------- | -------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------- |
| Mutable client preferences       | `GameSetUp.ini`, `chatSetup.ini`                                                                               | User profile/configuration, seeded from retail defaults          |
| Default and compatibility policy | `DefaultGameSetup.ini`, `Compatible.ini`, `package.ini`                                                        | Bootstrap configuration and source-provider policy               |
| UI layout and presentation       | `GUI.ini`, `GUI800X600.ini`, `CHATGUI.ini`, `PlayGui.ini`, `DummyMovie*.ini`, `msgbox.ini`                     | Presentation catalogs                                            |
| Text and localization            | `StrRes.ini`, `Cn_Res.ini`, `nameRes.ini`, `numRes.ini`, `ProfessionalName.ini`, `StatusTips.ini`              | Localization catalogs                                            |
| Items and economy                | `itemtype.dat`, `ItemAdd.ini`, `ItemTexture.ini`, `Shop.dat`, `emoneyshop.ini`, `itemprice.ini`                | Gameplay definition catalogs                                     |
| Roles, NPCs, and monsters        | `Monster.dat`, `npc.ini`, `NpcX.ini`, `RolePart.ini`, `TransForm.ini`                                          | Gameplay and presentation definitions, split at typed boundaries |
| Magic, actions, and effects      | `MagicType.dat`, `Action.dat`, `Action*.ini`, `3DEffect*`, `MagicEffect.ini`, `effect.ini`                     | Gameplay definitions plus rendering bindings                     |
| Models, textures, and motion     | `3DObj.*`, `3DTexture.*`, `3dmotion.*`, `armor.*`, `weapon.*`, `mount.*`                                       | Rendering/model catalogs                                         |
| World and maps                   | `GameMap.dat`, `GameMapEx.ini`, `MapRelationInfo.ini`, `region.ini`, `terrainnpc.ini`, `TerrainMagic/`, `tme/` | World catalogs and map-content bindings                          |
| Audio bindings                   | `ActionSound.ini`, `MusicRegion.ini`, `sound.ini`                                                              | Audio catalogs                                                   |
| Client-only features             | poker, ranking, help, shop, sponsor, and arena files                                                           | Feature-specific catalogs; import only with the owning feature   |

### Format and precedence findings

- Each of the 14 `.dbc` files has a same-stem text `.ini` counterpart. The DBC files are binary, not
  renamed text. Which representation wins is **unknown** until the native loader is verified.
- Eleven `.tme` payloads are duplicated byte-for-byte between `ini/tme/` and `ini/TerrainMagic/`.
  Their path identities must be preserved even if a compiled package later deduplicates their bytes.
- Fifteen `ini/` files are zero length. Empty is observable input and must not be confused with a
  missing file.
- High-byte text appears throughout the configuration corpus. Much of it is consistent with a
  Chinese Windows code page, while `RacePointShop.ini` is detected as UTF-8. A blanket Latin-1 or
  UTF-8 policy would corrupt some content. Encoding becomes explicit per format or per catalog after
  native verification.
- Text syntax is not uniform. Some files use ordinary sections and keys, some are whitespace- or
  comma-delimited, some use numeric sections, and some carry `//` comments. Typed readers should
  share a small lexical layer only where the observed grammars actually overlap.

### Historical compatibility discrepancy

The supplied retail `ini/GameSetUp.ini` contains:

```ini
[ScreenMode]
ScreenModeRecord=2
```

The earlier pre-content-boundary `GameSetupConfiguration` implementation and its tests expected:

```ini
[ScreenModeRecord]
ScreenMode=2
```

That discrepancy has been corrected. The current typed reader and tests use the observed retail
shape.

## `data/` Inventory

### Payload formats

| Extension |  Files |       Bytes | Observed role                          |
| --------- | -----: | ----------: | -------------------------------------- |
| `.dds`    | 11,240 | 162,275,542 | UI, icon, portrait, and world textures |
| `.swf`    |    137 |  12,069,996 | Legacy Flash login/UI content          |
| `.jpg`    |    990 |   9,132,148 | Images and thumbnails                  |
| `.bmp`    |    103 |   4,969,386 | UI and launcher images                 |
| `.dat`    |      4 |   3,645,232 | Binary startup/role/map payloads       |
| `.msk`    |    258 |   2,876,761 | Map masks                              |
| `.ani`    |     28 |     326,950 | Windows RIFF animated cursors          |
| `.db`     |      2 |     178,688 | Windows `Thumbs.db` cache files        |
| `.tga`    |      8 |     175,142 | Targa images                           |
| `.cur`    |      4 |       8,952 | Windows cursors                        |
| `.ico`    |      1 |       2,238 | Windows icon                           |
| `.ini`    |      2 |       1,631 | Local data-area configuration          |

### Top-level payload families

| Retail path           | Files |      Bytes | Proposed logical destination                                  |
| --------------------- | ----: | ---------: | ------------------------------------------------------------- |
| `data/interface/`     | 2,998 | 62,564,769 | `presentation/ui/`                                            |
| `data/ItemMinIcon/`   | 1,922 |  8,165,246 | `presentation/icons/items-small/`                             |
| `data/MapItemIcon/`   | 1,607 |  2,984,904 | `presentation/icons/items-world/`                             |
| `data/EmotionIco/`    |   327 |  2,923,264 | `presentation/icons/emotes/`                                  |
| `data/PlayerFace/`    |   604 |  2,905,326 | `presentation/portraits/players/`                             |
| `data/Npcface/`       |    13 |     69,248 | `presentation/portraits/npcs/`                                |
| `data/Cursor/`        |    33 |    338,140 | `presentation/cursors/`                                       |
| `data/minimap/`       |    92 |  2,883,224 | `world/minimaps/`                                             |
| `data/map/`           | 4,528 | 83,927,721 | `world/presentation/`                                         |
| `data/main/`          |   578 | 27,870,445 | Bootstrap/login presentation and binary tables; split by role |
| `data/main3/`         |    13 |    349,824 | Bootstrap/login presentation; role requires verification      |
| `data/pic/`           |    57 |    130,010 | Presentation images; classify by consumer                     |
| `data/AutoPatch_pic/` |     5 |    550,545 | Patcher/launcher, excluded from game runtime by default       |

The destination names are planning domains only. They are not instructions to bulk-migrate those
families, rename retail files in place, or rewrite references by hand. Actual checked-in runtime
content remains consumer-led.

### Texture evidence

Of the `.dds`-named files with a valid DDS signature:

| Encoding | Files |
| -------- | ----: |
| DXT3     | 8,410 |
| DXT1     | 2,813 |
| DXT5     |    13 |

The most common dimensions are 128×128 (4,005 files), 64×64 (2,640), and 32×32 (1,849).

Four `.dds`-named item icons actually contain TGA payloads:

- `data/MapItemIcon/722755.dds`
- `data/MapItemIcon/710718.dds`
- `data/ItemMinIcon/722755.dds`
- `data/ItemMinIcon/710718.dds`

Decoders and inventory tooling must dispatch from validated signatures, retain the original path for
identity, and record extension/signature mismatches as diagnostics.

### Duplicate payloads

The loose `data/` tree contains 397 SHA-256 duplicate groups: 447 redundant file copies totaling
5,845,326 bytes. Some duplicates have different semantic identities, such as item and world icons.

Any future consumer/catalog layer must preserve every required logical path even if a later
content-addressed payload store deduplicates identical bytes internally.

### Legacy-only content

- The 28 `data/Cursor/*.ani` files are RIFF animated cursor binaries. They are unrelated to the text
  animation catalogs under top-level `ani/` despite sharing an extension.
- `data/PlayerFace/JPG/16/Thumbs.db` and `data/PlayerFace/JPG/64/Thumbs.db` are shell caches and are
  excluded from runtime packages.
- The 137 SWF files depend on retired Flash behavior. The default plan is to preserve them as source
  evidence but rebuild required flows in first-party OpenConquer UI. Embedding a Flash runtime
  requires a separate, explicit architecture and security decision.
- `data/main/role.dat`, `data/main/start.dat`, and `data/main/start-facebook.dat` are substantial
  binary inputs. Their formats and consumers remain an evidence gate.

## Adjacent Dependencies

### Animation catalogs

Top-level `ani/` contains 54 text catalogs. Entries such as `ItemMinIcon.Ani` map logical animation
or resource IDs to frames under `data/`; other catalogs bind map imagery.

These catalogs are relevant evidence whenever a future consumer depends on the corresponding
resources because the catalog IDs, frame ordering, and retail paths are part of the compatibility
contract. They remain inventory-only until an implemented consumer establishes the required lookup
and decoding behavior.

### WDF packages and loose overlays

`ini/package.ini` declares:

```text
data.wdf
c3.wdf
data3.wdf
```

The first two packages exist in the surveyed snapshot; `data3.wdf` does not.

Observed retail archive identities:

| Package     |     Entries |       Bytes | SHA-256                                                            |
| ----------- | ----------: | ----------: | ------------------------------------------------------------------ |
| `data.wdf`  |      14,739 | 392,245,257 | `fc628e4adeb7de48b0b7cde3c7793c7177a289a6defe9b90a33a1f683dcdb132` |
| `c3.wdf`    |      10,274 | 359,069,116 | `ab68f57cc24ae10052031583ce7aa4676247fe8dde5a7bdd98e572adcf7b7243` |
| `data3.wdf` | unavailable |           — | declared by retail but not present                                 |

Existing WDF packages begin with the `PFDW` signature.

Native analysis has established the package-registration and routing behavior required by the modern
content boundary:

- `GraphicData.dll`, not `conquer.exe`, reads `ini/package.ini`;
- `package.ini` is consumed as whitespace-delimited package-name tokens rather than as an INI file;
- failure to open `package.ini` is non-fatal and produces zero registered packages;
- `TqPackagesOpen` return values are discarded by the startup registration driver;
- package names are normalized by lowercasing ASCII and converting `\` to `/`;
- the registration prefix is the whole normalized declaration with everything from the final `.`
  onward removed;
- there is no basename extraction;
- the actual registered package identity is the 32-bit WDF hash of that prefix;
- package registration is first-wins by prefix hash;
- distinct prefix strings whose hashes collide therefore share one routing identity;
- prefix-hash ownership is established before the declared WDF is opened;
- a missing or unusable first package consequently blocks later declarations with the same routing
  hash;
- the missing retail `data3.wdf` still registers an empty `data3` package identity and is non-fatal;
- virtual-path routing hashes the first normalized path segment and selects the first registered
  package with the same prefix hash;
- the full normalized virtual path is hashed separately to obtain the WDF entry UID;
- a virtual path without `/` routes using its entire normalized string;
- native WDF hashing uses a 256-byte zero-padded buffer and silently truncates longer inputs;
- loose-only, package-only, and loose-then-package lookup modes are distinct native behaviors.

The WDF archive format is also verified:

```text
12-byte header
├── uint32 magic = PFDW
├── uint32 entry count
└── uint32 entry-table offset

payload region

entry table
└── 16 bytes per entry
    ├── uint32 UID
    ├── uint32 absolute payload offset
    ├── uint32 payload size
    └── uint32 reserved = 0
```

The entry table is sorted by UID ascending and native lookup uses binary search.

The current implementation preserves those verified behaviors while applying explicit modern safety
boundaries:

- WDF archive paths are rejected when host resolution encounters symbolic links or reparse points;
- expected package resolution/open/validation failures become non-fatal unavailable registrations;
- package-registration results are published as an immutable completed snapshot;
- archives are limited to 100,000 entries before allocation;
- header and index arithmetic is checked;
- the complete index must fit within the archive;
- every 16-byte index record must be present;
- the reserved DWORD must be zero;
- UIDs must be strictly ascending;
- duplicate UIDs are rejected;
- each entry payload must begin at or after the 12-byte header;
- each entry payload must end at or before the index table;
- malformed and truncated archives fail deterministically;
- opened entry streams are bounded to their selected payload range.

The 100,000-entry ceiling is a modern resource-safety limit rather than a retail format claim. It is
well above both surveyed retail archive counts.

The implementation intentionally does not reject overlap between two otherwise valid payload ranges.

Retail files are observed to be packed contiguously, but native evidence has not established that
overlap itself is an invalid format condition. Individual entry containment already prevents reads
from escaping the validated payload region, so overlap rejection remains an explicit compatibility
decision rather than deferred cleanup.

### Deferred areas

- `map/` holds DMap terrain containers and related scene data. `ini/GameMap.dat` is its primary
  index. A verified reader already exists in the sibling server codebase and can be used as
  supporting evidence, but client ownership and rendering composition remain separate work.
- `c3/` holds models and textures selected through the INI/DBC binding tables.
- `sound/` holds WAV effects and MP3 music selected through INI audio catalogs.

These areas remain deferred dependencies. Files from them enter the checked-in runtime content
closure only when an implemented client consumer requires them and the corresponding slice has
established the relevant compatibility and validation behavior.

## Inventory Conclusions

1. Retail directories are storage history, not suitable modern subsystem boundaries.
2. Original relative paths remain compatibility identities and must never be casually normalized.
3. Logical organization belongs in typed consumers and reviewed catalogs; physical runtime payload
   migration remains consumer-led.
4. Extension alone is not a safe format discriminator.
5. WDF routing and archive validation are explicit runtime boundaries: native evidence determines
   registration, hashing, routing, and lookup compatibility, while modern validation constrains
   legacy archives and host files as untrusted input.
6. `Server.dat` is explicit compatibility evidence, with independently verified native RSA material,
   bounded PKCS#1/gzip/XML decoding, typed historical projection, and exact parity tests, but it is
   intentionally owned by offline tooling rather than by the production client runtime.
7. Mutable preferences, immutable game definitions, presentation resources, historical compatibility
   fixtures, and obsolete launcher content require different ownership and distribution policies.
8. The **inventory** of the surveyed retail families is complete, while checked-in runtime migration
   remains intentionally consumer-led. The repository may additionally preserve small exact
   compatibility fixtures outside the runtime closure when required to permanently lock
   parity-sensitive behavior.
