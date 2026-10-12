# STARTUP-001S — Publisher-authenticated realm-directory authoring

## Ownership and native parity

The clean 5517 `Server.dat` (14 groups, 94 servers, `FlashName`, `FlashIcon`, `FlashHint`, `ServerName`, `ServerIP`, `ServerPort`) remains **offline evidence**. It is not a runtime authority or an automatic seed for live endpoints. The client accepts a distinct publisher-signed directory, requiring the native protocol identity to be separate from display metadata. STARTUP-001R introduced the closed set of 102 `gNNsNN` image IDs and signed schema 2. This slice makes that directory authorable without importing native RSA, executing SWFs, or weakening the signature gate.

The existing `OpenConquer.Product.Tool` now accepts `create-realm-catalog`; its product-release signing workflow remains separate. The command consumes a small unsigned source definition; no private key, expiration or monotonically increasing sequence belongs in that source. It validates and writes the exact client schema-2 fields in a consistent order, signs the **serialized bytes** using ECDSA P-256/SHA-256 in the 64-byte IEEE P1363 format expected by `SignedLoginCatalog.Read`, and writes a public-only build-time authority file. The command requires an external PKCS#8 DER key, never creates, prints, includes or rotates private keys, never contacts any network endpoint, and refuses to overwrite a prior publication directory.

## Unsigned definition format

Store the editable definition outside the publication directory, for example `realms.definition.json`. A template is provided at `docs/startup/examples/realms.definition.example.json`:

```json
{
  "regions": [
    {
      "id": "north",
      "name": "North",
      "servers": [
        {
          "id": "realm-one",
          "name": "Realm One",
          "protocolServerName": "RealmOne",
          "accountHost": "login.example.org",
          "accountPort": 9958,
          "iconId": "g01s01"
        }
      ]
    }
  ]
}
```

**Example only:** `login.example.org` is not a real publisher endpoint. Replace it with an explicitly controlled account-ingress host, not the host of an archived `Server.dat` row. `iconId` must be `none` or a packaged `g01s01`–`g09s05` ID that exists in STARTUP-001R (group 2 supports 13 entries, group 9 supports 5). Region/server IDs are ASCII `[A-Za-z0-9_.-]` and case-sensitive. Names are validated Unicode scalar strings. All endpoints, protocol names and icon identifiers are covered by the signature. For currently supported standard type-1060 framing this publisher further restricts `protocolServerName` to at most 15 ASCII bytes, leaving space for native C-string termination in the 16-byte field. This is intentionally stricter than the schema-2 *reader* and can be broadened only after native encoding evidence.

All object fields are required, duplicates and unknowns fail, input/output are bounded to 128 KiB, and no more than 64 regions, 128 servers per region or 1024 servers total can be published. Duplicate region IDs, duplicate per-region server IDs and repeated protocol server names fail. Timestamps are strict UTC whole seconds; `expires > now`, `issued <= now + 5 minutes`, and validity lasts at most 24 hours. Sequence must be a strictly increasing positive integer **chosen and tracked by the publisher**; the tool does not automatically reuse or overwrite old sequence values. The client currently retains its high-water sequence only for the running process; durable replay protection across restarts needs a separate explicit design and is not claimed here.

## Signing setup

Keep the private key in an operator-controlled directory **outside the repository, deployable content, and public web root**. For a local test identity only:

```bash
umask 077
mkdir -p "$HOME/.config/openconquer/realm-publisher"
openssl genpkey -algorithm EC -pkeyopt ec_paramgen_curve:prime256v1 -out "$HOME/.config/openconquer/realm-publisher/private.pem"
openssl pkcs8 -topk8 -nocrypt -in "$HOME/.config/openconquer/realm-publisher/private.pem" -outform DER -out "$HOME/.config/openconquer/realm-publisher/private.pk8"
chmod 600 "$HOME/.config/openconquer/realm-publisher/private.pk8"
```

Do not commit these keys. For a real publisher, use a separately controlled long-lived signing identity with audited custody and recovery procedures. This tool **does not** claim HSM support, automated rotation, secure deletion, or delegated signing. Generated test keys are not production identity keys.

## Offline publication

```bash
dotnet run --project tools/OpenConquer.Product.Tool -c Release --no-restore -- \
  create-realm-catalog \
  --definition /absolute/path/realms.definition.json \
  --private-key "$HOME/.config/openconquer/realm-publisher/private.pk8" \
  --sequence 1 \
  --issued-utc "$(date -u +%Y-%m-%dT%H:%M:%SZ)" \
  --expires-utc "$(date -u -v+12H +%Y-%m-%dT%H:%M:%SZ)" \
  --catalog-uri https://catalog.example.org/realms.json \
  --signature-uri https://catalog.example.org/realms.sig \
  --output /absolute/new/private-staging-directory
```

macOS `date -v+12H` is shown above; on GNU/Linux use `date -u -d '+12 hours'`. The target publication directory **must not exist**, and creation uses a sibling staging directory followed by a filesystem move, leaving no partially published directory. The resulting output contains `realms.json` (authenticated JSON bytes), `realms.sig` (raw 64-byte signature), and `authority.json` (HTTPS catalog/signature URLs and public SPKI key only). The `.sig` file is raw P1363 bytes, **not** DER, base64, or a product-release signature envelope.

Deploy `realms.json` and `realms.sig` to the exact HTTPS locations named in `authority.json` as one coordinated release, ensuring that the downloaded content is byte-for-byte unchanged. Do not serve the private key. `authority.json` is an **input to the trusted client build**, not a dynamically fetched authority source. Provision it with the existing property:

```bash
dotnet publish src/OpenConquer.Client -c Release \
  -p:OpenConquerCatalogAuthorityFile=/absolute/new/private-staging-directory/authority.json
```

**Do not silently replace an existing client trust anchor or use the development key in a shipped production client.** Signed v1 remains supported; this authoring command only emits v2. Even with a valid directory, Enter remains disabled until independently verified native authentication is integrated. This slice does not implement publishing over HTTP, status probes, AccountServer protocol, key rotation, or login submit.

## Security and verification gates

- Use a private-key path that resolves to a regular, non-linked file. On Unix it must not be group/other accessible. The input file and target directory may not overlap. Use an untrusted definition only after operator review; reject malformed/overlong JSON before signing.
- A production deployment must keep track of sequence monotonicity and coordinate expiry with operational publishing. Uploading one file before the other can cause brief unavailability, not fail-open authorization.
- Test `RealmCatalogPublisherTests`, full `OpenConquer.Product.Tool.Tests`, full solution, locked restore, formatter, Windows/macOS CI and client content-set verification. Verify a signed test directory end-to-end via the existing `LoginCatalogClient`, plus a real-driver screenshot of the signed server icons.
- Publication success demonstrates an authenticated *directory*, not that its hosts are reachable or that the native 5517 login path is complete.
