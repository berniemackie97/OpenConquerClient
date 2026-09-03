# Retail 5517 Content Inventory

## Purpose

This document records the first evidence-based survey of the native Windows retail client content
used to reconstruct OpenConquer Client. It is an inventory, not a statement that every retail file
belongs in the modern runtime.

The source tree was inspected read-only. Counts and byte totals below describe loose files only;
entries stored inside WDF packages are not included.

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
| `ini/DefaultGameSetup.ini` |          50 | `ac8263fb13bc46319fe75736ddb1892108c093101b5f098c3fcba6b447c1d986` |
| `ini/package.ini`          |          29 | `511028e125d43635d90f777806b0f0fee65895b5b1a604cf278001fee536a8f2` |
| `ini/GameMap.dat`          |      10,876 | `0676b7d6969dd8277438704a289b1f57e8debb89fe5d3f912819c3a7880686a0` |
| `data.wdf`                 | 392,245,257 | `fc628e4adeb7de48b0b7cde3c7793c7177a289a6defe9b90a33a1f683dcdb132` |
| `c3.wdf`                   | 359,069,116 | `ab68f57cc24ae10052031583ce7aa4676247fe8dde5a7bdd98e572adcf7b7243` |

These fingerprints identify the surveyed inputs without embedding a workstation-specific absolute
path in project metadata. The checked-in content manifest intentionally covers only the current
consumer-led dependency closure; this inventory remains the evidence record for surveyed retail
families that have not yet entered that closure.

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
families, rename retail files in place, or rewrite references by hand. Actual checked-in content
remains consumer-led.

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

The first two packages exist in the surveyed snapshot; `data3.wdf` does not. Existing WDF packages
begin with the `PFDW` signature.

Subsequent native analysis established the package-routing behavior required by the modern content
boundary:

- `package.ini` is consumed as whitespace-delimited package-name tokens rather than as an INI file;
- a missing `package.ini` produces zero registered packages and is non-fatal;
- the package prefix is derived from the normalized declaration by removing everything from its
  final `.` onward;
- registration is first-wins by prefix;
- a missing declared WDF still reserves its prefix, so a later duplicate cannot replace it;
- the first normalized virtual-path segment selects the package prefix;
- the full normalized virtual path is hashed for WDF lookup;
- native normalization lowercases ASCII and converts `\` to `/`;
- loose-only, package-only, and loose-then-package lookup modes are distinct native behaviors.

The current implementation preserves those verified boundaries for valid retail inputs while
rejecting unsafe modern host paths.

A later hardening slice remains responsible for the full untrusted-WDF validation boundary,
including strict index and payload-range validation. Existing-but-malformed archive behavior must
remain tied to native evidence rather than inferred from filesystem behavior.

### Deferred areas

- `map/` holds DMap terrain containers and related scene data. `ini/GameMap.dat` is its primary
  index. A verified reader already exists in the sibling server codebase and can be used as
  supporting evidence, but client ownership and rendering composition remain separate work.
- `c3/` holds models and textures selected through the INI/DBC binding tables.
- `sound/` holds WAV effects and MP3 music selected through INI audio catalogs.

These areas remain deferred dependencies. Files from them enter the checked-in content closure only
when an implemented client consumer requires them and the corresponding slice has established the
relevant compatibility and validation behavior.

## Inventory Conclusions

1. Retail directories are storage history, not suitable modern subsystem boundaries.
2. Original relative paths remain compatibility identities and must never be casually normalized.
3. Logical organization belongs in typed consumers and reviewed catalogs; physical payload migration
   remains consumer-led.
4. Extension alone is not a safe format discriminator.
5. WDF routing semantics and archive validation are separate concerns: native evidence determines
   compatibility behavior, while modern readers must still validate legacy archives as untrusted
   input.
6. Mutable preferences, immutable game definitions, presentation resources, and obsolete launcher
   content need different ownership and distribution policies.
7. The **inventory** of the surveyed retail families is complete, but checked-in migration is
   intentionally consumer-led. The repository currently preserves only the exact files required by
   implemented consumers; additional retail files enter the content set only with the feature slice
   that consumes them.
