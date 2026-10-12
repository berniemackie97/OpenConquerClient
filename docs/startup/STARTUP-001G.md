# STARTUP-001G — Authenticated realm-catalog acquisition

## Authority

The client does not trust `Server.dat`, arbitrary CLI configuration, a mutable user file, a DNS lookup, or HTTP transport alone to define the account-login destination. It accepts only a bounded server catalog with an ECDSA P-256/SHA-256 detached signature over the **exact** JSON bytes. The issuer key and the two HTTPS locations are embedded at **client build/publish time**, and is covered by the existing publisher-signed installed-release manifest **when launched from a verified managed installation**. Direct developer execution does not independently authenticate the binary. The catalog-signing private key **never** enters the source tree, client binary, or launcher.

If the public authority descriptor is not compiled into the client, the startup catalog remains empty. An invalid configured authority aborts startup. A failed download, invalid signature, stale document, invalid endpoint, or unsupported schema leaves the catalog empty. No legacy fallbacks, test roots, placeholder addresses, or implicit trust-key CLI switches exist.

### Build authority (public values only)

Supply `-p:OpenConquerCatalogAuthorityFile=/absolute/ci/path/catalog-authority.json` to **build and publish**, using a release-managed source. The file must be UTF-8 JSON with exactly:

```json
{
  "catalogUri": "https://YOUR-AUTHORIZED-HOST.example/catalog/realms.json",
  "signatureUri": "https://YOUR-AUTHORIZED-HOST.example/catalog/realms.sig",
  "publicKeySpki": "BASE64-ENCODED-P256-X509-SUBJECT-PUBLIC-KEY-INFO"
}
```

The values above are **documentation syntax, not shipped configuration**. Do not use them as production endpoints or generate throwaway production signing keys. Release production credentials and public trust need an explicit operator/publisher decision. Never take the authority JSON from an untrusted user-controlled runtime path.

An unauthenticated developer build has no embedded authority and remains fail-closed. Test-only ephemeral signing keys are generated within the test process and never embedded in a production build. Authority files larger than 4096 bytes, malformed/duplicate/unknown fields, non-P-256 keys, non-HTTPS URLs, redirects, credentials, query strings, and nonstandard ports are rejected.

### Signed payload v1

The issuer signs the exact bytes of a UTF-8 JSON document (without normalizing whitespace or keys). Signature file is exactly 64 raw IEEE-P1363 bytes (`r || s`) from ECDSA P-256 with SHA-256. Document schema:

```json
{
  "schemaVersion": 1,
  "productId": "OpenConquer",
  "sequence": 1,
  "issuedAt": "2026-10-11T19:00:00Z",
  "expiresAt": "2026-10-11T20:00:00Z",
  "regions": [
    {
      "id": "region-id",
      "name": "Region label",
      "servers": [
        {
          "id": "server-id",
          "name": "Server label",
          "protocolServerName": "NATIVE-PROTOCOL-SERVER-NAME",
          "accountHost": "issuer-controlled.example",
          "accountPort": 9958
        }
      ]
    }
  ]
}
```

All values are **schema examples only**; the client ships no populated issuer document or endpoint. The timestamp must be UTC seconds in the shown format. Signature is verified **before parsing**. Unknown/duplicate fields are fatal. Issue time cannot be more than 5 minutes in the future; expiry must be in the future and within 24 hours of issue. Region and server names/identifiers pass the established immutable view-model validators. Protocol names remain distinct from labels. Account ingress hosts/ports are separately validated and never inferred from UI text or the old `Server.dat`.

Limits: 128 KiB JSON, 64-byte detached signature, at most 64 regions, 128 servers per region, and 1024 servers total. Only HTTP 200 from the exact configured HTTPS URI is accepted; no redirects, automatic decompression, cookies or ambient client credentials. Total acquisition timeout is 12 seconds. The network task does not block the desktop startup loop.

## Runtime ownership

- `Startup/Catalog` owns issuer key validation, signed catalog parsing, bounded HTTP acquisition, and the typed account-ingress directory.
- `ClientApplication` starts acquisition after the main desktop window is created and consumes the completed result on its render thread, preserving single-threaded selection state.
- The existing `LoginSelectionInput.ReplaceCatalog` atomically revokes previous selection/focus/capture on new authority data.
- Expiry clears the active catalog and any selection. Shutdown cancels outstanding acquisition and leaves no ongoing UI mutation.
- There is no account connection in this slice. Future authentication must obtain the ingress **from the verified catalog's directory** for the **selected identity**, after verifying the catalog is still current. View-model labels must never be used as an endpoint.

## Boundaries and acceptance

This slice deliberately does not introduce a browser, Flash runtime, generated server rows, online/offline badges, server names painted into the OpenGL grid, Login/Register submission, native login packets, or world authorization. UTF-8 catalog display names are retained as Unicode values; the legacy CP936 text pipeline must not silently substitute glyphs. Text rendering is a separate, explicitly reviewed consumer of the authenticated view.

A detached signature plus a maximum 24-hour lifetime bounds, but does not eliminate, replay of a previously valid document across process restarts. The `sequence` field is verified as positive and retained for future persistent anti-rollback policy. It is **not** currently a cross-process rollback fence, and should not be represented as one.

Verify locked restore, format, Release build, all unit tests, retail content closure and publish isolation, real macOS/Windows startup, cancellation, expiry, and both supported logical resolutions. Production catalog acquisition remains disabled until the publisher supplies a genuine authority descriptor as part of the controlled signed client release.
