# WORLD-004: Map-data-ID-driven content loading

The server sends distinct world map and map-data identities in packet
1110. The native map creation path uses the map-data ID to resolve its
terrain record from `ini/GameMap.dat`; it does not infer an archive
path from the world map ID. See `20_notes/Game_Map/cgamemap_class.md`
(`CGameMap_CreateFromDataId`, `0x61DA1F`).

`GameMapContent.Load` accepts the previously loaded `GameMapCatalog`,
a client content source, and the server-supplied map-data ID.
It resolves the exact retail archive path, reads the archive through
the bounded content reader, then applies `GameMapArchive`,
`GameMapCellGrid`, and `GameMapSceneSections` in order. The
catalog's companion field is retained without assigning an unproven
rendering meaning.

The clean retail 5517 `GameMap.dat` resolves map-data ID 1010 to
`map/map/newbie.7z` with companion value 256. Integration tests use
the unchanged committed retail index and seven-zip archive to verify
the grid, scenery, and backdrop. Missing map IDs and archives fail
explicitly; supplying Newbie Village bytes under the catalog's
`newplain.7z` path is rejected by the archive entry-identity check.

The content set deliberately remains unchanged: the existing tracked
payload contains `ini/GameMap.dat`, but the archive closure is not
shipped yet. Nothing calls this loader during live world entry until
the first map scene owns its required archive, ANI, puzzle, sprite, and
texture content. Enabling it earlier would break logins against
existing managed products. When that runtime consumer is wired, the
Content Tool closure must include its proven dependencies, and the
operator will import and verify the retail payload locally before
promotion. No GameServer packet or world-ownership change is required
for this client-only loading boundary.
