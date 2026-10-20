# STARTUP-001R: Server icons in the signed realm directory

## Sources and scope

Adds version 2 of the signed catalog schema, which publishers opt into, and static server images bundled with the client so the login screen can look more like the retail one. The clean 5517 `Server.dat` is used only as an offline historical record. The 5517 code that reads it forwards `FlashName`, `FlashIcon`, `FlashHint`, `ServerName` and the routing fields as separate values. `FlashIcon` is an opaque token. It is not a number derived from the selection slot. `20_notes/Architecture/03_login_flash_startup_command_contract.md` and `05_startup_data_files_and_native_replacement.md` document this 5517 behavior. Production endpoints still come only from the signed catalog, never from `Server.dat`.

The clean `OpenConquer-5517-Login-Reference.zip` contains 102 separate SWF files under `data/main/flash/Group1` through `Group9`. The `DefineBitsJPEG3` bitmap (symbol 1) in each of the 102 files was decoded offline to straight-alpha RGBA and Brotli-compressed. The shipped client never decodes SWF or JPEG data. The images are not mapped automatically to the historical table of 14 groups and 94 servers. The publisher must assign each server an image ID, and that ID is validated. `STARTUP-001R-icon-manifest.tsv` lists each image's source file, the SHA-256 of its decoded RGBA and its compressed size in bytes as packaged. Concatenating all 102 RGBA pixel buffers in order (by group number, then server number, both ascending) gives SHA-256 `39e3799ed5e4c274076127819ccad5b1fdac3905135db068d0b1c095765d5432`.

As first implemented, 100 of the bitmaps were 156x56, `g09s01` was 164x85 and `g09s02` was 154x54. Each was scaled into an estimated 3x4 server cell without changing its aspect ratio. The presentation creates the textures lazily, caches at most 102 distinct icons for the current OpenGL context and disposes them when the context is torn down. A server label with an icon uses the existing row gap below its cell. Labels without an image (v1) stay centered as before. Catalog acquisition and refresh, selection state, credentials, action buttons and the account ingress protocol are unchanged.

[STARTUP-001Z](STARTUP-001Z.md) later replaced these assets. Each asset is now the movie's root shape as the `son` holder shows it (170x90, or 164x85 for `g09s01`), drawn unscaled at the holder origin. The raw JPEG3 pictures were premultiplied. The SHA-256 of the concatenated RGBA is now `c57d20964dc906cd5073f4094d1b91dd3cb7c9461fcad52cd4bb89fc4d45392c`.

## Signed schema version 2

The signed JSON root still has `schemaVersion`, `productId`, `sequence`, `issuedAt`, `expiresAt` and `regions`. A version 2 server object must have the version 1 fields plus `iconId`, with no other fields and no duplicates. Region objects still have `id`, `name` and `servers`. The excerpt below only illustrates the schema and is not a real catalog document:

```json
{"schemaVersion":2,"productId":"OpenConquer","sequence":7,"issuedAt":"2026-10-11T19:00:00Z","expiresAt":"2026-10-11T21:00:00Z","regions":[{"id":"north","name":"North","servers":[{"id":"example","name":"Example Realm","protocolServerName":"NativeName","accountHost":"login.example.org","accountPort":9958,"iconId":"g01s01"}]}]}
```

`iconId` is one of a closed set of local identifiers, such as `g01s01`, `g02s13` or `g09s05`, or `none` for no server image. The number of servers per group in this asset pack is `12, 13, 12, 12, 12, 12, 12, 12, 5`. Case changes, gaps, unknown IDs, path syntax and remote URLs are rejected. `none` creates no icon texture and keeps the older centered label. Version 1 is unchanged and rejects any attempt to add a v2 field. Version 2 requires the icon field. The existing publisher P-256 detached signature still covers the payload as bytes, and the validity times, product identity, sequence and endpoint checks still apply. No key material, live publisher configuration, server endpoint or permissive fallback ships with this change.

Real publication needs a separate, safe workflow for authoring and signing catalogs and for provisioning the publisher key. Do not hand-edit a published signed payload or bypass signature validation to make test realms show up in the UI.

## Resources and trust

- Every accepted icon token is looked up in a finite local map before an embedded resource name is built. Signed data never supplies a path and cannot trigger a filesystem or network fetch.
- The images are embedded in the `OpenConquer.Content` assembly. `content/retail-5517` and its five-file closure are unchanged.
- Each decoded image must have exactly its expected RGBA dimensions and no trailing decoded data.
- The UI never derives icon IDs from server names, the region page, the server index, the host or the protocol name.
- Icon IDs affect display only. `TryGetAccountEndpoint` still returns an endpoint only for the same region and server object instances that passed verification.
- No online status, FlashHint tooltip, dynamic SWF, publisher signing key, new socket or login submission is added.

## Testing

Run locked restore, formatting verification, the Release build and tests for the full solution, the catalog, icon and layout tests, retail content-set verification and Windows/macOS CI. The content test decodes and hashes all 102 embedded icons and checks the special sizes found in the retail files. The client test signs valid and invalid version 2 documents with a temporary local P-256 key and checks that unsupported identifiers are rejected. For visual checks on a real driver at 1024x768 and 800x600, configure a trusted schema 2 test directory. The default unconfigured UI and existing signed v1 catalogs must not change. Selecting source images by anything beyond the stable IDs, and reproducing the `Server.dat` mappings of the 5517 client, are left for later work on 5517 data and catalog authoring.
