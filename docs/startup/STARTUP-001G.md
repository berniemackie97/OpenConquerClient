# STARTUP-001G: Signed realm catalog download

## Trust model

The account-login destination never comes from `Server.dat`, arbitrary command-line settings, a user-editable file, a DNS lookup or HTTP transport on its own. The client accepts only a size-limited server catalog with a detached ECDSA P-256/SHA-256 signature over the raw JSON bytes. The issuer key and the two HTTPS locations are compiled in when the client is built and published. When the client is launched from a verified managed installation, they are covered by the existing publisher-signed manifest of the installed release. When a developer runs the client directly, nothing authenticates the binary. The private catalog-signing key never goes into the source tree, the client binary or the launcher.

If the public authority descriptor is not compiled in, the startup catalog stays empty. An invalid configured authority stops startup. A failed download, invalid signature, stale document, invalid endpoint or unsupported schema leaves the catalog empty. There are no legacy fallbacks, test roots, placeholder addresses or hidden command-line switches for trust keys.

### Build authority (public values only)

Pass `-p:OpenConquerCatalogAuthorityFile=/absolute/ci/path/catalog-authority.json` to both build and publish, pointing at a file managed by the release process. The file must be UTF-8 JSON with these fields and no others:

```json
{
  "catalogUri": "https://YOUR-AUTHORIZED-HOST.example/catalog/realms.json",
  "signatureUri": "https://YOUR-AUTHORIZED-HOST.example/catalog/realms.sig",
  "publicKeySpki": "BASE64-ENCODED-P256-X509-SUBJECT-PUBLIC-KEY-INFO"
}
```

The values above only show the syntax; they are not shipped configuration. Do not use them as production endpoints, and do not generate throwaway production signing keys. Production credentials and the public trust key for a release are a decision for the operator and publisher. Never read the authority JSON from a runtime path that an untrusted user controls.

A developer build without an authority file has no embedded authority and accepts no catalog. Signing keys used only in tests are generated inside the test process and never embedded in a production build. Authority files are rejected if they are larger than 4096 bytes, have malformed, duplicate or unknown fields, or contain a key that is not P-256, a non-HTTPS URL, a redirect, credentials, a query string or a nonstandard port.

### Signed payload v1

The issuer signs the bytes of a UTF-8 JSON document as they are, with no normalization of whitespace or keys. The signature file holds 64 raw IEEE-P1363 bytes (`r || s`) from ECDSA P-256 with SHA-256. Document schema:

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

All values are schema examples. The client ships no filled-in issuer document or endpoint. Timestamps are UTC with whole seconds, in the format shown. The signature is checked before the document is parsed. Unknown or duplicate fields are fatal. The issue time may be at most 5 minutes in the future. The expiry must be in the future and no more than 24 hours after the issue time. Region and server names and identifiers go through the same immutable view-model validators as before. Protocol names stay separate from labels. Account ingress hosts and ports are validated separately and are never derived from UI text or the old `Server.dat`.

Limits: 128 KiB of JSON, a 64-byte detached signature, at most 64 regions, 128 servers per region and 1024 servers in total. Only an HTTP 200 response from the configured HTTPS URI itself is accepted. Redirects, automatic decompression, cookies and ambient client credentials are not used. The whole download has a 12-second timeout. The network task does not block the desktop startup loop.

## Runtime ownership

- `Startup/Catalog` owns issuer key validation, signed catalog parsing, the size- and time-limited HTTP download, and the typed account-ingress directory.
- `ClientApplication` starts the download after the main desktop window is created and picks up the finished result on its render thread, so selection state stays single-threaded.
- `LoginSelectionInput.ReplaceCatalog` clears the previous selection, focus and capture in one step when new catalog data arrives.
- When the catalog expires, the active catalog and any selection are cleared. Shutdown cancels a download in progress, and nothing changes the UI after that.
- There is no account connection yet. When authentication is added, it must take the ingress for the selected identity from the verified catalog's directory, after checking that the catalog is still current. View-model labels must never be used as an endpoint.

## Limitations

This step adds no browser, Flash runtime, generated server rows, online/offline badges, server names drawn into the OpenGL grid, Login/Register submission, 5517 login packets or world authorization. UTF-8 catalog display names are kept as Unicode strings. The legacy CP936 text pipeline must not silently substitute glyphs for them. Drawing that text is a separate consumer of the authenticated view, reviewed on its own.

A detached signature and a lifetime of at most 24 hours limit replay of an old, once-valid document across process restarts, but they do not prevent it. The `sequence` field is checked to be positive and kept for a future anti-rollback policy that persists between runs. It does not currently stop rollback across processes, and it should not be described as doing so.

The production catalog download stays disabled until the publisher supplies a real authority descriptor as part of a controlled, signed client release.

## Testing

Check locked restore, format, Release build, all unit tests, the retail content closure and publish isolation, real startup on macOS and Windows, cancellation, expiry, and both supported logical resolutions.
