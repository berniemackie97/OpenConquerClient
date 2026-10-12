# STARTUP-001J — Authenticated realm catalog lifetime

## Scope

The 001G acquisition path originally ran once per process. An expired signed catalog correctly revoked the login selection, but the client could not discover a replacement without restarting. This slice keeps the existing HTTPS-only, detached-signature, strict-schema trust boundary and adds a bounded, asynchronous refresh lifetime.

`LoginCatalogRefresh` owns a single pending acquisition and is driven only from `ClientApplication`'s render thread. Its fetch delegate is the existing `LoginCatalogClient.FetchAsync` path using the embedded publisher authority. The completed result is consumed on the render thread; the background task never touches selection or OpenGL state.

## Invariants

- First acquisition starts at the first render tick; never block a frame on network I/O.
- No more than one acquisition is outstanding. Ordinary success schedules the next check at most five minutes later, or one minute before expiry if sooner; a ten-second floor bounds short-lived-catalog polling. Failures retry after thirty seconds.
- A valid current catalog remains visible across transient failed refreshes. Expiry removes it and calls `LoginSelectionInput.ReplaceCatalog(Empty)`, clearing region/server selection, focused slots, and pending pointer capture. An expired or invalid acquisition never restores it.
- Within a process, accepted signed sequence numbers must increase strictly. An equal-sequence response can preserve a still-valid *existing* snapshot but cannot replace it or change endpoints. Lower sequences and expired replies are rejected. This is an in-process rollback fence **only**; it does not claim persistent rollback prevention across restarts.
- A backward wall-clock observation revokes the active catalog and prevents further acceptance for that process. It does not attempt to use wall-clock rollback to extend a signed document's lifetime. Restart after the host clock has been corrected to recover.
- Authentication catalog failure logging is generic and emitted once per consecutive failure period; no URLs, document bytes, or exception messages enter diagnostics.
- Shutdown and OpenGL context release cancel acquisition. Shutdown observes its completion before disposing the cancellation token. Cancellation, network failures, and malformed publisher documents do not install new server data.
- No credentials, account transport, registration endpoints, legacy `Server.dat`, local trust overrides, or native protocol packets are introduced.

## Acceptance

Unit-test initial acquisition, stable refresh, new-sequence replacement, equal/lower-sequence rejection, transient failures, expiry during an in-flight request, clock rollback, single-flight behavior, and cancellation drain. Then run locked restore, formatting, Release build and all tests, signed-catalog regression tests, retail content closure, and Windows/macOS CI. Verify manually that the desktop remains responsive while the catalog endpoint is unreachable and that expiry clears the visible selection and labels. Production authority is still publisher-provided and no test endpoint is compiled into the client.
