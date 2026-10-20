# STARTUP-001F: Region/server selection model and input

## Catalog

Production startup does not use retail `Server.dat` or its decoded metadata as the realm catalog. No endpoints, login transport, routing fallbacks, registration packets or made-up production realms are added. Selection uses the existing 2x8 region and 3x4 server chrome and keeps all artwork coordinates, including `AccountPanel.Y = 637`.

`LoginServerCatalog` is an immutable snapshot for the view. It does not supply endpoints or decide trust. Region IDs, display labels, realm IDs and the 5517 protocol `ServerName` are separate fields. The catalog checks its limits and rejects duplicate identities, control or directional text, and malformed identifiers. Whatever later produces a real catalog must first meet its own requirements for a signed source, transport, provenance and protocol endpoints, and only then build and hand over a snapshot. Input handling and drawing cannot by themselves allow login or world entry.

## Interaction

- The client starts with `LoginServerCatalog.Empty`, so no empty grid slot can be selected or start a connection.
- Replacing the catalog clears the selected region, the selected server, the page indexes, focus and any pending mouse capture, so no stale selection survives a catalog change.
- Selecting a different region clears the server selection and goes back to the first server page. Changing the region page clears both the region and server selection, and changing the server page clears the server selection, so nothing off-screen stays selected.
- A pointer selection needs both the press and the release inside the same occupied logical cell. Pointer coordinates use the current OpenGL viewport mapping, and letterbox areas never hit-test.
- Losing window focus or replacing the snapshot cancels pointer capture.
- Tab and Shift-Tab move between the region and server focus areas, arrow keys move between visible occupied cells, PageUp and PageDown change pages within the page count, Enter selects the focused cell, and Escape clears input focus without changing any login or world state.
- Focus and selection have different borders. Drawing the selection adds no per-frame texture creation, parsing or heap allocation.
- There is no made-up service status, protocol endpoint, character identity or Login/Register action.

## Limits

A snapshot holds at most 64 regions, 128 servers per region and 1024 servers in total. Input collections are copied and validated. No value from the catalog is used as a resource path. The protocol server name is stored as given, for a later authentication request that matches the 5517 client. It is never derived from the visible label.

There is no Flash decoding and no runtime asset change. The retail content manifest is unchanged.

## Testing

Unit tests cover validation, isolation from later changes to the input, duplicate identities, limit failures, paging, clearing stale selections, mouse capture, keyboard navigation, focus cancellation, and an empty catalog that allows no selection. Also run the existing locked restore, format verification, Release build and tests, content-set verification, published-client integrity, desktop smoke test on macOS and Windows, and rendering conformance checks.
