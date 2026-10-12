# STARTUP-001R — Authenticated realm-directory icon contract

## Source identity and implementation scope

This adds opt-in signed catalog schema version 2 and locally bundled static **server** images for retail-inspired login presentation. The original clean-5517 `Server.dat` is a historical, offline-only record: the native producer forwards separate `FlashName`, `FlashIcon`, `FlashHint`, `ServerName`, and routing fields. `FlashIcon` is an opaque token, not a number inferred from selection slot. `20_notes/Architecture/03_login_flash_startup_command_contract.md` and `05_startup_data_files_and_native_replacement.md` document those native facts. Modern production endpoints continue to come **only** from the authenticated signed catalog, never `Server.dat`.

The clean `OpenConquer-5517-Login-Reference.zip` contains 102 individual SWF files under `data/main/flash/Group1` through `Group9`. All 102 `DefineBitsJPEG3` bitmap symbol 1 images were decoded offline to straight-alpha RGBA and Brotli-compressed. SWF/JPEG decoding never occurs in the shipped client. The assets have no automatic mapping to the historic 14-group/94-server table; the publisher must assign an explicit, validated image ID. Their source file, individual decoded RGBA SHA-256 and packaged compressed byte count are listed in `STARTUP-001R-icon-manifest.tsv`. The ordered concatenation of all 102 RGBA pixel buffers (ascending numeric group then numeric server index) has SHA-256 `39e3799ed5e4c274076127819ccad5b1fdac3905135db068d0b1c095765d5432`.

100 bitmap images are 156×56, `g09s01` is 164×85, and `g09s02` is 154×54. The original image dimensions are preserved. The presentation scales them into existing 3×4 server cells without changing aspect ratio, creates textures lazily, caches no more than 102 unique icons for the current OpenGL context, and disposes them during context teardown. Server labels with icons occupy the existing row gap beneath each cell; no-image v1 labels remain centered as before. The signed-catalog acquisition/refresh, selection state, credentials, action buttons and account-ingress protocol are unchanged.

## Signed schema version 2

The signed JSON root remains `schemaVersion`, `productId`, `sequence`, `issuedAt`, `expiresAt`, and `regions`. A version-2 server object requires exactly the version-1 fields plus `iconId`, with no additional fields or duplicates. Region objects remain `id`, `name`, and `servers`. Example excerpt **for schema illustration only** (not an authority document):

```json
{"schemaVersion":2,"productId":"OpenConquer","sequence":7,"issuedAt":"2026-10-11T19:00:00Z","expiresAt":"2026-10-11T21:00:00Z","regions":[{"id":"north","name":"North","servers":[{"id":"example","name":"Example Realm","protocolServerName":"NativeName","accountHost":"login.example.org","accountPort":9958,"iconId":"g01s01"}]}]}
```

`iconId` is a closed local identifier such as `g01s01`, `g02s13`, `g09s05`, or `none` for no server illustration. Group counts permitted by this asset pack are `12, 13, 12, 12, 12, 12, 12, 12, 5`. Case changes, gaps, unrecognized IDs, path syntax and remote URLs are rejected. `none` produces no icon texture and preserves the older centered label. Version 1 remains strictly unchanged and rejects any attempted v2 field injection; version 2 requires the icon field. The payload remains covered **as bytes** by the existing publisher P-256 detached signature, validity times, product identity, sequence and endpoint checks. No key material, live authority, server endpoint or permissive fallback ships in this slice.

For operational publication, the next slice must supply a separate safe authoring/signing and authority provisioning workflow; do not hand-edit a published signed payload or bypass signature validation to force the UI to show test realms.

## Resource and trust boundaries

- All accepted icon tokens are compared against a finite local map before constructing an embedded resource name; signed data supplies no path and cannot trigger filesystem or network fetches.
- Resources are embedded in the `OpenConquer.Content` assembly; neither `content/retail-5517` nor its five-file closure changes.
- All decoded images are required to have exactly their expected bounded RGBA dimensions and no trailing decoded data.
- The UI does not infer icon IDs from server names, region page, server index, host or protocol name.
- New server image identifiers affect presentation only. The `TryGetAccountEndpoint` authority boundary remains tied to the exact verified region/server object instances.
- There is no status/online claim, tooltip/FlashHint, dynamic SWF, publisher signing key, new socket or login submission added.

## Acceptance gates

Run locked restore, formatting verification, Release build and tests on the full solution, focused catalog/icon/layout tests, retail content-set verification, and Windows/macOS CI. The content test decodes and hashes **all 102** embedded icons and checks native special sizes. The client test signs both valid and intentionally invalid version-2 documents with a local ephemeral P-256 key and checks that unsupported identifiers cannot be accepted. Use an explicitly configured trusted schema-2 test directory for driver-backed visual inspection at 1024×768 and 800×600. Unconfigured default UI and old signed-v1 catalogs must remain unchanged. Actual source-image selection semantics beyond the explicit stable IDs and exact native `Server.dat` mappings remain future native/authoring integration work.
