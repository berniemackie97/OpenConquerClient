# STARTUP-001F — Region/server selection contract and input ownership

## Authority

Production startup does **not** read retail `Server.dat` or its decoded metadata as an operational realm catalog. This slice introduces no endpoints, login transport, routing fallbacks, registration packets, or synthetic production realms. It uses the existing 2×8 region and 3×4 server chrome and retains all established artwork coordinates, including `AccountPanel.Y = 637`.

`LoginServerCatalog` is an immutable **view snapshot**, not an endpoint/trust provider. Region IDs, display labels, realm IDs, and native-protocol `ServerName` are distinct fields. The catalog validates bounds and rejects duplicate identities, control/directional text, and malformed identifiers. A future authoritative catalog producer must satisfy its own signed-source/transport/provenance and protocol-endpoint contract **before** constructing and handing off such a snapshot. Input and presentation cannot themselves grant login or world-entry authorization.

## Interaction

- The client initially owns `LoginServerCatalog.Empty`, so no empty grid slot can be selected or activate a connection.
- Catalog replacement clears selected region, selected server, page indexes, focus, and pending mouse capture; stale selection cannot survive authority changes.
- Selecting a different region clears server selection and returns to the first server page. Region-page changes invalidate region and server selection; server-page changes invalidate server selection, preventing invisible stale targets.
- Pointer selection requires press/release inside the exact same occupied logical cell. Pointer coordinates use the current OpenGL viewport mapping; letterboxes never hit-test.
- Losing window focus or replacing the snapshot cancels pointer capture.
- Tab/Shift-Tab navigate the region/server focus areas, arrows move within visible occupied cells, PageUp/PageDown change bounded pages, Enter commits focused selection, and Escape clears input focus without asserting any login/world state.
- Focus and selection have distinct border feedback, with no per-frame texture creation, parsing, or heap allocations introduced by selection rendering.
- No fabricated service status, protocol endpoint, character identity, or Login/Register action exists in this slice.

## Constraints and acceptance

Maximum 64 regions, 128 servers per region, and 1024 servers per snapshot. Collection input is defensively copied and validated. No catalog-provided values are used as resource paths. The protocol server name is stored verbatim for a later native-verified authentication request; it is not inferred from the visible label.

Unit tests cover validation, mutation isolation, duplicated identities, bound failures, paging, stale selection invalidation, mouse capture, keyboard navigation, focus cancellation, and empty-catalog fail closure. Run the existing locked restore, formatting verification, Release build/tests, content-set verification, published-client integrity, macOS/Windows desktop smoke test, and rendering conformance gates.

No Flash decoding or runtime asset changes. The exact retail content manifest remains unchanged.
