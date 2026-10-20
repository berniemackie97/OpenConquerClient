# STARTUP-001V: Keeping the selection across signed directory renewals

Builds on STARTUP-001U. When the client accepts a renewed publisher-signed realm catalog, `ClientApplication.AcceptCatalogIfReady` replaces the selection view through `LoginSelectionInput.ReplaceCatalog`, without conditions. That is right when entries are revoked. On its own, though, it also drops the user's region and server choice whenever the same publisher renews an otherwise unchanged directory, for example the local signed development directory that is renewed periodically. The catalog is still fully replaced on every renewal, with the same safety properties. After the replacement, the earlier choice is shown again, but only when the full signed identity and the account-server address of the region and server are unchanged. Account and password input is never kept or reused across a catalog change.

## Where retention runs

`LoginVerifiedSelectionRetention.Apply` runs only when the client application accepts a `LoginCatalogRefresh.Advance` transition. `LoginCatalogRefresh` still checks signatures, rejects stale sequence numbers, enforces expiry, checks that the clock does not go backwards, and manages the fetch lifecycle. By the time retention runs, the old and the new snapshot have both been checked against the same `LoginCatalogAuthority`, which is fixed at build time and cannot change while the client process runs. The retention helper cannot obtain an endpoint, sign a catalog or start authentication.

On every transition it first calls `LoginSelectionInput.ReplaceCatalog`. That cancels pointer capture, clears keyboard focus, drops object references, resets the pages and replaces the whole catalog. The old selection, which is mutable and untrusted, is never used as-is for the new view.

The helper then rebuilds a region the user had chosen from the new signed view. It does so only if the old selection belongs to the immediately preceding signed view, that view has not expired, and the new region has the same ID and display name (compared case-sensitively). The server choice is rebuilt only if the new server also has the same case-sensitive ID, display name, protocol server name and icon ID, and the signed endpoint lookups for the old and new server both succeed with the same host text and port. A server that keeps its ID but moves to a different account-server address is not reselected. The restored selection points to objects in the new signed snapshot, so no old endpoint reference survives the transition. Page indices are recomputed from the new positions, including when an entry moves across a page boundary (16 regions or 12 servers per page).

Nothing is restored when:

- the previous publication or the replacement has expired
- the catalog is unavailable, empty or revoked
- the region's identity or the server's metadata changed
- the account-server routing does not match
- the user had not chosen a region or server before

A replacement that arrives in the same frame cannot hide an expiry or a clock rollback. After the catalog recovers from any of these cases, the user has to choose again.

`LoginCredentialInput.Synchronize` still runs after every accepted catalog change. Because it compares catalog and server object identity, it clears the account text, password bytes and focus even when the server choice is restored. This keeps the existing credential protection in place, and a password never survives a publication change. The 5517 `Server.dat` and Flash selection identities described in `20_notes/Network/5517-server-selection-and-realm-handoff.md` are the historical source for keeping display names and protocol names apart. Keeping the selection across renewals is a usability choice for the signed directory. It does not describe how the original Flash screen behaved on refresh.

## Scope

Unchanged: the signed catalog wire schema, the publisher, the embedded public authority, Caddy, endpoint derivation, the 5517 type-1060 framing, the disabled Enter button, credential lifetime limits, external URLs, startup textures, font rendering, the account panel at Y=620 and the selection artwork. The region and server scans run only when an accepted catalog snapshot changes, never per frame, so normal frames do no extra work.

## Verification

Run a locked restore, the formatting check, a Release build and the full solution tests. The focused tests are `LoginVerifiedSelectionRetentionTests` and the existing credential-security, login-selection and catalog-refresh tests. Also check the retail content closure and Windows and macOS CI.

Cases to exercise:

- a signed renewal from sequence 7 to 8 with unchanged routing
- regions and servers reordered across pages
- a changed protocol name, display name, icon, host or port
- a missing region or server
- expiry followed by a new fetch
- stale catalog references
- a pointer capture in progress
- credentials being erased after an otherwise identical renewal

With a trusted published directory, check rendering at 1024x768 and 800x600. The selection art stays visible across an identical signed renewal, and all credential fields are cleared. Changing a server's routing deselects that server. A signed catalog does not enable authentication through Enter.
