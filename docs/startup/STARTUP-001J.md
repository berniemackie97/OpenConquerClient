# STARTUP-001J: Realm catalog refresh

## Scope

The STARTUP-001G acquisition path originally ran once per process. When the signed catalog expired, the login selection was revoked as intended, but the client could not fetch a replacement without a restart. STARTUP-001J adds an asynchronous refresh with at most one request in flight. Catalog trust works as before: HTTPS only, a detached signature and a strict schema check.

`LoginCatalogRefresh` holds a single pending acquisition and is driven only from the render thread of `ClientApplication`. It fetches through the existing `LoginCatalogClient.FetchAsync` path, using the publisher key and catalog location embedded in the client. The render thread consumes the completed result. The background task never touches the selection or OpenGL state.

## Behavior

- The first acquisition starts on the first render tick. A frame never waits on network I/O.
- At most one acquisition is outstanding. After a normal success, the next check is scheduled no more than five minutes later, or one minute before expiry if that comes sooner. A ten-second minimum interval limits polling when a catalog is short-lived. After a failure, the client retries in thirty seconds.
- A valid current catalog stays visible while refreshes fail temporarily. On expiry the catalog is removed and `LoginSelectionInput.ReplaceCatalog(Empty)` is called, which clears the region and server selection, focused slots and any pending pointer capture. An expired or invalid acquisition never brings the catalog back.
- Within one process, each accepted signed sequence number must be higher than the last. A response with the same sequence can keep the existing snapshot in place while that snapshot is still valid, but it cannot replace it or change endpoints. Lower sequence numbers and expired replies are rejected. This rollback protection lasts only for the life of the process. It does not protect against rollback across restarts.
- If the client sees the wall clock move backward, it revokes the active catalog and accepts no further catalogs in that process. A clock set back therefore cannot extend a signed document's lifetime. To recover, correct the host clock and restart the client.
- Catalog failures are logged with a generic message, once per run of consecutive failures. URLs, document bytes and exception messages never appear in diagnostics.
- Shutdown and OpenGL context release cancel the acquisition. Shutdown waits for it to finish before disposing the cancellation token. Cancellation, network failures and malformed publisher documents never install new server data.
- No credentials, account transport, registration endpoints, legacy `Server.dat` data, local trust overrides or 5517 protocol packets are added.

## Testing

Unit tests cover the first acquisition, a stable refresh, replacement by a newer sequence, rejection of equal and lower sequences, temporary failures, expiry while a request is in flight, clock rollback, the single-request limit and draining on cancellation. Then run locked restore, formatting, the Release build and all tests, the signed-catalog regression tests, the retail content closure check and Windows/macOS CI. Check by hand that the desktop stays responsive while the catalog endpoint is unreachable and that expiry clears the visible selection and labels. The production publisher key and catalog location still come from the publisher, and no test endpoint is compiled into the client.
