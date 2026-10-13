# STARTUP-001V — Authenticated realm-renewal selection continuity

## Baseline and intent

After merged STARTUP-001U, main `443fa64633a734b3d443f8e84302b5b6f76bbacb`, the first-party client accepts renewed publisher-signed realm catalogs, but `ClientApplication.AcceptCatalogIfReady` unconditionally replaces the selection view with `LoginSelectionInput.ReplaceCatalog`. This is correct for revocation yet unnecessarily loses a previously explicit region/server selection when the same publisher renews an otherwise unchanged directory (for example, the local signed development directory renewed periodically). This slice keeps the safety properties of that replacement and restores *only* an established visual choice when full signed identity and account-ingress values are unchanged. It does not store or reuse account/password input across a catalog change.

## Authenticated refresh boundary

`LoginVerifiedSelectionRetention.Apply` is called only at the client application's accepted `LoginCatalogRefresh.Advance` transition; `LoginCatalogRefresh` still verifies signatures, rejects stale sequences, enforces expiration and clock monotonicity, and owns acquisition lifecycle. The original and replacement snapshots are already authenticated with the same immutable build-time `LoginCatalogAuthority` in that client process. The retention helper cannot acquire an endpoint, sign a catalog, or initiate authentication.

On every transition it first calls `LoginSelectionInput.ReplaceCatalog` to cancel pointer capture, clear keyboard focus, release object references, reset pages and replace the entire catalog. No untrusted/mutable existing selection is treated as authority for the new view.

It may then reconstruct a *previously explicit* region choice from the new signed view only when the former selection still belongs to the immediately previous, unexpired authenticated view, and the new region has the **same case-sensitive ID and display name**. It may reconstruct the server choice only when the new server also retains the exact case-sensitive ID, display name, protocol server name and icon ID, and both authenticated endpoint lookups succeed with identical host text and port. In particular, keeping a server ID while changing the account-ingress address is not sufficient to retain its selection. The new selection points to the **new signed snapshot's objects**; no stale endpoint reference crosses the transition. Page indices are recomputed from the new positions, including across 16-region / 12-server page boundaries.

No choice is restored after an expired previous publication, an expired replacement, an unavailable catalog, an empty/revoked catalog, changed region identity, changed server metadata, mismatched ingress routing, or absence of an original explicit choice. Expiry or clock rollback cannot be disguised by a replacement in the same frame. On recovery, selection must be explicit again.

`LoginCredentialInput.Synchronize` is intentionally still called after each accepted catalog view change. It clears account text, password bytes and focus based on catalog and server object identity, even if the server choice is safely restored. This preserves the existing privilege boundary and avoids password retention across publication changes. The native 5517 `Server.dat`/Flash selection identities documented in `20_notes/Network/5517-server-selection-and-realm-handoff.md` are historical source evidence for distinguishing display and protocol names. This new renewal behavior is a modern authenticated-directory UX policy, **not** a claim about original Flash refresh behavior.

## Scope

No changes to the signed catalog wire schema, publisher, embedded public authority, Caddy, endpoint derivation, native type-1060 framing, Enter activation, credential lifetime limits, external URLs, startup textures, font rendering, account-panel Y=620, or selection artwork. Bounded region/server scans are permitted only when an accepted catalog snapshot changes, not per frame. On the normal frame path, no extra work is performed.

## Verification

Run locked restore, formatting verification, Release build and full solution tests; focus `LoginVerifiedSelectionRetentionTests`, existing credential-security, login-selection and catalog-refresh tests; verify the retail content closure and Windows/macOS CI. Specifically exercise signed sequence 7 to 8 with unchanged routing; reordered region/server pages; changed protocol/display/icon/host/port; missing region/server; expiry followed by reacquisition; stale catalog references; pending pointer capture; and credential erasure after an otherwise identical renewal. Test 1024×768 and 800×600 rendering with a trusted published directory: selection art remains visible across an identical signed renewal, while all credential fields clear. A server routing change must revoke the selected server. A signed catalog does not enable Enter authentication.
