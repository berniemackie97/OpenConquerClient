# Retail 5517 Content Inventory

This document records the first survey of the content shipped with the retail Windows client that
OpenConquer Client is reconstructed from. It lists what the retail client contains. Being listed
here does not mean a file belongs in the OpenConquer runtime.

The source tree was inspected read-only. Counts and byte totals below cover loose files only.
Entries stored inside WDF packages are not counted.

A retail file can still be kept as a compatibility fixture when its exact bytes are needed for
parity tests or offline inspection. That does not make it part of the runtime content set.

## Confidence Labels

- **Observed**: the value comes directly from the supplied retail files.
- **Inferred**: the role follows from names, signatures or references, and still needs to be
  confirmed against the retail client's behavior.
- **Unknown**: the behavior has not been confirmed. Implementation waits at this point and does not
  invent behavior.

## Source Identity

The surveyed client identifies itself as retail version `5517` in the four ASCII bytes of
`version.dat`.

Fingerprints of selected source files:

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

The fingerprints identify the surveyed files without storing an absolute path from the survey
machine in the project.

The checked-in runtime content manifest lists only the files that implemented client code needs
today. `Server.dat` appears in the table above because this document describes the surveyed retail
files. Its exact bytes are kept separately as a compatibility fixture, not as runtime content.

## Scope Summary

| Retail area |  Files | Directories, including root |       Bytes | Survey coverage                              |
| ----------- | -----: | --------------------------: | ----------: | -------------------------------------------- |
| `ini/`      |    331 |                           3 |  28,557,142 | Full inventory and classification            |
| `data/`     | 12,777 |                         357 | 195,662,666 | Full inventory by payload family             |
| `ani/`      |     54 |                           1 |   8,483,019 | Included because it indexes `data/` payloads |
| `map/`      |    977 |                           6 | 403,781,216 | Listed as a dependency only                  |
| `c3/`       | 11,886 |                       1,207 | 393,769,348 | Listed as a dependency only                  |
| `sound/`    |    527 |                           1 |  74,200,387 | Listed as a dependency only                  |
| `Help/`     |    169 |                           2 |   4,231,626 | Deferred (legacy help application content)   |

The inspected `ini/`, `data/`, `ani/` and `map/` trees have no symbolic links, no whitespace or
non-ASCII characters in paths, and no paths that collide when case is folded. The longest relative
path in that set is 67 characters. This is true of this snapshot only. Import validation must still
check the same rules for every source.

## Root Bootstrap Data

### `Server.dat`

Retail `Server.dat` is a verified reference for the 5517 login/server bootstrap flow.

It is not part of the OpenConquer client's runtime content.

Where the file is used:

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

The fixture, byte-identical to the audited retail file, is kept at:

```text
tests/OpenConquer.Content.Tool.Tests/TestData/retail-5517/Server.dat
```

Analysis of the retail binaries shows that retail 5517 reads the file directly from the client root,
without going through WDF package routing. OpenConquer keeps this as a compatibility reference, but
there is no runtime `LooseOnly` consumer because the client no longer reads the file.

Retail file identity:

```text
length: 2816 bytes
SHA-256: 0b4d366786aa4498c7e470f10fd8bca716bc1d6cbda1eb3894666183f8327a90
RSA blocks: 11 × 256 bytes
```

The retail `CConfigDataTableQueryProvider` builds a 2048-bit RSA public key with exponent `65537`.

The modulus was recovered independently. As unsigned big-endian raw bytes, its SHA-256 is:

```text
76acb04b08190b129985f8dee2b466efcd686eb1662cb598bd1a8154cb9196f1
```

The retail client decodes the file in these steps:

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

SHA-256 of the inflated XML:

```text
5d6b00ff722a8b37aa2981affecd478aee73bdc22cdc498a25b700242b55c35a
```

The schema has these fields:

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

Group rows use IDs `1..14`. The server rows of each group start at:

```text
0x65 + (groupIndex - 1) * 0x64
```

The `Child` value on the group row gives the number of servers in that group.

Groups are therefore 100 rows apart, and the tooling decoder rejects a group with more than 100
servers.

The fixture currently decodes to:

```text
groups: 14
servers: 94
```

The tooling model keeps the original field meanings. It does not convert them into a runtime server
or realm model.

The retail data shows why this matters. Several rows have different `FlashName` and `ServerName`
values, for example:

```text
FlashName="Water"       ServerName="Fire"
FlashName="Cerberus"    ServerName="Gryphon"
FlashName="Pegasus"     ServerName="Basilisk"
FlashName="Cinderella"  ServerName="SnowWhite"
```

`ServerName` also has a confirmed role in the 5517 login protocol, so it must not be treated as
display metadata.

The tooling keeps `ServerIP` and `ServerPort` as the original text. They are never turned into
runtime endpoints, and the inspection command never opens a connection.

The cryptography, the parser, the protocol details and the reasons for this ownership split are
described in [`../compatibility/server-dat.md`](../compatibility/server-dat.md).

## `ini/` Inventory

Despite its name, `ini/` is a mixed database. It holds text configuration, compiled tables, binary
records, region data and terrain-effect payloads.

| Extension | Files |     Bytes | Observed character                                   |
| --------- | ----: | --------: | ---------------------------------------------------- |
| `.dbc`    |    14 | 9,876,089 | Binary compiled tables                               |
| `.ini`    |   131 | 9,394,606 | Mostly sectioned or line-oriented text; 14 are empty |
| `.dat`    |    27 | 5,780,104 | Multiple unrelated binary formats; one empty file    |
| `.wdb`    |     1 | 3,434,463 | Binary database                                      |
| `.tme`    |   155 |    68,444 | Binary terrain-magic records                         |
| `.rgn`    |     3 |     3,436 | Binary region geometry                               |

### Functional families

These groups are for planning. They do not describe which retail classes own the files.

| Family                           | Representative retail files                                                                                    | Proposed logical ownership                                       |
| -------------------------------- | -------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------- |
| Mutable client preferences       | `GameSetUp.ini`, `chatSetup.ini`                                                                               | User profile/configuration, seeded from retail defaults          |
| Default and compatibility policy | `DefaultGameSetup.ini`, `Compatible.ini`, `package.ini`                                                        | Bootstrap configuration and source-provider policy               |
| UI layout and presentation       | `GUI.ini`, `GUI800X600.ini`, `CHATGUI.ini`, `PlayGui.ini`, `DummyMovie*.ini`, `msgbox.ini`                     | Presentation catalogs                                            |
| Text and localization            | `StrRes.ini`, `Cn_Res.ini`, `nameRes.ini`, `numRes.ini`, `ProfessionalName.ini`, `StatusTips.ini`              | Localization catalogs                                            |
| Items and economy                | `itemtype.dat`, `ItemAdd.ini`, `ItemTexture.ini`, `Shop.dat`, `emoneyshop.ini`, `itemprice.ini`                | Gameplay definition catalogs                                     |
| Roles, NPCs, and monsters        | `Monster.dat`, `npc.ini`, `NpcX.ini`, `RolePart.ini`, `TransForm.ini`                                          | Gameplay and presentation definitions, split by typed reader     |
| Magic, actions, and effects      | `MagicType.dat`, `Action.dat`, `Action*.ini`, `3DEffect*`, `MagicEffect.ini`, `effect.ini`                     | Gameplay definitions plus rendering bindings                     |
| Models, textures, and motion     | `3DObj.*`, `3DTexture.*`, `3dmotion.*`, `armor.*`, `weapon.*`, `mount.*`                                       | Rendering/model catalogs                                         |
| World and maps                   | `GameMap.dat`, `GameMapEx.ini`, `MapRelationInfo.ini`, `region.ini`, `terrainnpc.ini`, `TerrainMagic/`, `tme/` | World catalogs and map-content bindings                          |
| Audio bindings                   | `ActionSound.ini`, `MusicRegion.ini`, `sound.ini`                                                              | Audio catalogs                                                   |
| Client-only features             | poker, ranking, help, shop, sponsor, and arena files                                                           | Feature-specific catalogs; import only with the owning feature   |

### Format and precedence findings

- Each of the 14 `.dbc` files has a text `.ini` file with the same stem. The DBC files are real
  binary files, not renamed text. Which of the two the client uses is unknown until the retail
  loader has been checked.
- Eleven `.tme` payloads are byte-for-byte identical in `ini/tme/` and `ini/TerrainMagic/`. Both
  paths must keep working even if a compiled package later stores the bytes only once.
- Fifteen `ini/` files are zero length. An empty file is valid input and must not be confused with
  a missing file.
- Text with high-bit bytes appears throughout the configuration files. Much of it is consistent
  with a Chinese Windows code page, but `RacePointShop.ini` is detected as UTF-8. A single Latin-1
  or UTF-8 rule for every file would corrupt some content. Each format or catalog gets its own
  declared encoding once the retail behavior has been checked.
- Text syntax varies. Some files use ordinary sections and keys, some are delimited by whitespace
  or commas, some use numeric sections, and some contain `//` comments. Typed readers should share
  a small lexer only where the grammars really overlap.

### Corrected `GameSetUp.ini` layout

The supplied retail `ini/GameSetUp.ini` contains:

```ini
[ScreenMode]
ScreenModeRecord=2
```

An earlier `GameSetupConfiguration` implementation, written before the Content layer existed, and
its tests expected:

```ini
[ScreenModeRecord]
ScreenMode=2
```

This has been fixed. The current typed reader and its tests use the layout found in the retail
file.

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

The destination names are planning areas only. They are not a plan to move these families in bulk,
rename retail files in place or rewrite references by hand. Files enter the checked-in runtime
content only when client code needs them.

### Textures

Of the `.dds`-named files with a valid DDS signature:

| Encoding | Files |
| -------- | ----: |
| DXT3     | 8,410 |
| DXT1     | 2,813 |
| DXT5     |    13 |

The most common dimensions are 128x128 (4,005 files), 64x64 (2,640) and 32x32 (1,849).

Four item icons with a `.dds` name actually contain TGA data:

- `data/MapItemIcon/722755.dds`
- `data/MapItemIcon/710718.dds`
- `data/ItemMinIcon/722755.dds`
- `data/ItemMinIcon/710718.dds`

Decoders and inventory tools must pick the format from the validated file signature, keep the
original path as the file's identity, and report extension/signature mismatches as diagnostics.

### Duplicate payloads

The loose `data/` tree has 397 groups of files with identical SHA-256 hashes, which add up to 447
redundant copies totaling 5,845,326 bytes. Some copies serve different purposes, such as an item
icon and a world icon with the same bytes.

Any future consumer or catalog must keep every logical path it needs, even if a content-addressed
store later keeps identical bytes only once.

### Legacy-only content

- The 28 `data/Cursor/*.ani` files are RIFF animated cursor binaries. They share an extension with
  the text animation catalogs in top-level `ani/` but are unrelated to them.
- `data/PlayerFace/JPG/16/Thumbs.db` and `data/PlayerFace/JPG/64/Thumbs.db` are shell caches and are
  left out of runtime packages.
- The 137 SWF files need Flash, which is retired. The default plan is to keep them as reference
  material and rebuild the flows that are needed in OpenConquer's own UI. Embedding a Flash runtime
  would need its own architecture and security decision.
- `data/main/role.dat`, `data/main/start.dat` and `data/main/start-facebook.dat` are large binary
  inputs. Their formats and the code that reads them are not known yet, and work that depends on
  them waits until they are.

## Adjacent Dependencies

### Animation catalogs

Top-level `ani/` contains 54 text catalogs. Entries in catalogs such as `ItemMinIcon.Ani` map
logical animation or resource IDs to frames under `data/`. Other catalogs bind map imagery.

Any future code that uses these resources must follow the catalogs, because the catalog IDs, frame
order and retail paths are part of the compatibility contract. Until such code exists and defines
the lookup and decoding rules, the catalogs are only inventoried here.

### WDF packages and loose overlays

`ini/package.ini` declares:

```text
data.wdf
c3.wdf
data3.wdf
```

The first two packages exist in the surveyed snapshot. `data3.wdf` does not.

Retail archives in the snapshot:

| Package     |     Entries |       Bytes | SHA-256                                                            |
| ----------- | ----------: | ----------: | ------------------------------------------------------------------ |
| `data.wdf`  |      14,739 | 392,245,257 | `fc628e4adeb7de48b0b7cde3c7793c7177a289a6defe9b90a33a1f683dcdb132` |
| `c3.wdf`    |      10,274 | 359,069,116 | `ab68f57cc24ae10052031583ce7aa4676247fe8dde5a7bdd98e572adcf7b7243` |
| `data3.wdf` | unavailable |           — | declared by retail but not present                                 |

Both existing WDF packages begin with the `PFDW` signature.

Analysis of the retail binaries has established how packages are registered and how paths are
routed to them. OpenConquer's content layer depends on this behavior:

- `GraphicData.dll`, not `conquer.exe`, reads `ini/package.ini`.
- `package.ini` is read as whitespace-delimited package names. It is not parsed as an INI file.
- If `package.ini` cannot be opened, startup continues with zero registered packages.
- The startup registration code ignores the return value of `TqPackagesOpen`.
- Package names are normalized by lowercasing ASCII letters and converting `\` to `/`.
- The registration prefix is the whole normalized declaration with everything from the last `.`
  onward removed.
- No basename is extracted.
- The registered package identity is the 32-bit WDF hash of that prefix.
- The first package registered with a given prefix hash wins.
- Different prefix strings whose hashes collide therefore share one routing identity.
- The prefix hash is claimed before the declared WDF file is opened.
- So a missing or unusable first package still blocks later declarations with the same routing
  hash.
- The missing retail `data3.wdf` still registers an empty `data3` package identity, and startup
  continues.
- Virtual-path routing hashes the first normalized path segment and picks the first registered
  package with the same prefix hash.
- The full normalized virtual path is hashed separately to get the WDF entry UID.
- A virtual path without `/` is routed by its whole normalized string.
- The retail WDF hash uses a 256-byte zero-padded buffer and silently truncates longer inputs.
- Loose-only, package-only and loose-then-package lookups are separate modes in the retail client.

The WDF archive layout has also been confirmed:

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

The entry table is sorted by ascending UID, and the retail client finds entries with a binary
search.

OpenConquer keeps all of this behavior and adds these safety checks:

- WDF archive paths are rejected if resolving them on the host runs into a symbolic link or
  reparse point.
- Expected failures to resolve, open or validate a package produce an unavailable registration and
  do not stop startup.
- The package registration results are published as one immutable snapshot after registration
  completes.
- An archive may have at most 100,000 entries. This is checked before anything is allocated.
- Header and index arithmetic is overflow-checked.
- The whole index must fit inside the archive.
- Every 16-byte index record must be present.
- The reserved DWORD must be zero.
- UIDs must be strictly ascending.
- Duplicate UIDs are rejected.
- Each entry's payload must begin at or after the end of the 12-byte header.
- Each entry's payload must end at or before the index table.
- Malformed and truncated archives are always rejected with a defined error.
- An opened entry stream can read only its own payload range.

The 100,000-entry limit is an OpenConquer resource-safety limit and not part of the retail format.
It is well above the entry counts of both surveyed retail archives.

Overlapping payload ranges are allowed, as long as each range is otherwise valid.

The retail archives are packed contiguously, but the retail code does not show that overlap makes an
archive invalid. Each entry is already confined to the validated payload region, so reads cannot
escape it. Allowing overlap is a compatibility decision, not cleanup left for later.

### Deferred areas

- `map/` holds DMap terrain containers and related scene data, with `ini/GameMap.dat` as the main
  index. The sibling server codebase already has a verified reader that can serve as a reference,
  but client ownership and rendering composition are separate work.
- `c3/` holds models and textures selected through the INI/DBC binding tables.
- `sound/` holds WAV effects and MP3 music selected through INI audio catalogs.

These areas are deferred. Their files are added to the checked-in runtime content only when
implemented client code needs them and the related compatibility and validation behavior is in
place.

## Implications

1. Retail directories reflect how the files were historically stored. They are not a good basis
   for modern subsystem boundaries.
2. Original relative paths are part of each file's compatibility identity and must never be
   normalized casually.
3. Logical organization belongs in typed consumers and reviewed catalogs. Runtime payload files are
   moved only when consuming code needs them.
4. The file extension alone does not reliably identify a format.
5. WDF routing and archive validation are runtime concerns with two sources of rules. The retail
   binaries define registration, hashing, routing and lookup. OpenConquer's own validation treats
   legacy archives and host files as untrusted input.
6. `Server.dat` is a compatibility reference. Its RSA key material was recovered and checked
   independently, its PKCS#1/gzip/XML decoding is size-limited, it is mapped to a typed historical
   model, and parity tests compare exact output. It belongs to offline tooling, not to the client
   runtime.
7. Mutable preferences, immutable game definitions, presentation resources, historical
   compatibility fixtures and obsolete launcher content need different ownership and distribution
   policies.
8. The inventory of the surveyed retail families is complete. Runtime content is migrated only as
   client code needs it. The repository may also keep small, byte-identical compatibility fixtures
   outside the runtime content when they are needed to lock down parity-sensitive behavior.
