# WORLD-004: Map-data-ID-driven content loading

The server sends distinct world map and map-data identities in packet
1110. The map-data ID selects the terrain record from `ini/GameMap.dat`; archive
paths must not be inferred from the world-map ID.

`GameMapContent.Load` accepts the previously loaded `GameMapCatalog`,
a client content source, and the server-supplied map-data ID.
It resolves the catalog archive path, reads the archive through
the bounded content reader, then applies `GameMapArchive`,
`GameMapCellGrid`, and `GameMapSceneSections` in order. The
catalog's companion field is retained without assigning an unproven
rendering meaning.

The `GameMap.dat` fixture resolves map-data ID 1010 to
`map/map/newbie.7z` with companion value 256. Integration tests use
the committed index and seven-zip archive to verify
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
operator will import and verify the source payload locally before
promotion. No GameServer packet or world-ownership change is required
for this client-only loading boundary.
