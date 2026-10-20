# STARTUP-001S: Authoring the signed realm directory

The clean 5517 `Server.dat` (14 groups, 94 servers, with `FlashName`, `FlashIcon`, `FlashHint`, `ServerName`, `ServerIP` and `ServerPort`) is kept only as an offline reference. The client never treats it as a source of live endpoints and never seeds from it automatically. Instead, the client accepts a separate directory signed by the publisher, in which the 5517 protocol server name is kept apart from the display metadata. STARTUP-001R added signed schema 2 and the fixed set of 102 `gNNsNN` image IDs. This note covers the tool that writes and signs that directory. It does not import the retail RSA code, run SWF files or relax the signature check.

`OpenConquer.Product.Tool` has a `create-realm-catalog` command for this, separate from the tool's product-release signing workflow. The command reads a small unsigned source definition. That source never holds a private key, an expiry time or a sequence number. The command validates the definition, writes the client's schema-2 fields in a fixed order, and signs the serialized bytes with ECDSA P-256/SHA-256 in the 64-byte IEEE P1363 format that `SignedLoginCatalog.Read` expects. It also writes a public-only authority file for the client build. The private key must be supplied as an external PKCS#8 DER file. The command never creates, prints, embeds or rotates private keys, makes no network connections, and refuses to overwrite an existing publication directory.

## Unsigned definition format

Keep the editable definition outside the publication directory, for example as `realms.definition.json`. A template is provided at `docs/startup/examples/realms.definition.example.json`:

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

`login.example.org` is a placeholder. Replace it with an account-server host that you control. Do not copy the host from an archived `Server.dat` row. `iconId` is either `none` or one of the packaged IDs from `g01s01` to `g09s05` listed in STARTUP-001R (group 2 has 13 entries, group 9 has 5). Region and server IDs use ASCII `[A-Za-z0-9_.-]` and are case-sensitive. Names must be valid Unicode scalar strings. The signature covers all endpoints, protocol names and icon IDs.

For the standard type-1060 framing that the client supports today, the publisher limits `protocolServerName` to 15 ASCII bytes. That leaves room for the C string terminator in the 16-byte field. The schema-2 reader accepts more than this. The publisher's limit can be relaxed once the retail client's encoding of the field is confirmed.

Every object field is required, and duplicate or unknown fields are rejected. Input and output are each limited to 128 KiB. A directory can hold at most 64 regions, 128 servers per region and 1024 servers in total. Duplicate region IDs, duplicate server IDs within a region and repeated protocol server names are rejected. Timestamps must be UTC with whole seconds. The tool requires `expires > now` and `issued <= now + 5 minutes`, and a directory is valid for at most 24 hours.

The sequence number is a positive integer that must increase with every publication. The publisher chooses and tracks it. The tool never reuses or overwrites old sequence values on its own. The client remembers the highest sequence it has seen only while the process runs. Replay protection that survives a restart is not implemented and would need its own design.

## Signing setup

**Keep the private key in a directory you control, outside the repository, outside any deployed content and outside the public web root.** To create a key for local testing only:

```bash
umask 077
mkdir -p "$HOME/.config/openconquer/realm-publisher"
openssl genpkey -algorithm EC -pkeyopt ec_paramgen_curve:prime256v1 -out "$HOME/.config/openconquer/realm-publisher/private.pem"
openssl pkcs8 -topk8 -nocrypt -in "$HOME/.config/openconquer/realm-publisher/private.pem" -outform DER -out "$HOME/.config/openconquer/realm-publisher/private.pk8"
chmod 600 "$HOME/.config/openconquer/realm-publisher/private.pk8"
```

Do not commit these keys or use them as production identity keys. A real publisher needs a separate long-lived signing key with audited custody and a recovery procedure. The tool has no support for HSMs, automated rotation, secure deletion or delegated signing.

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

The example uses the macOS form `date -v+12H`. On GNU/Linux, use `date -u -d '+12 hours'`. The output directory must not exist yet. The tool writes into a sibling staging directory and then moves it into place, so a failed run never leaves a partly written publication. The output contains `realms.json` (the signed JSON bytes), `realms.sig` (the raw 64-byte signature) and `authority.json` (only the HTTPS catalog and signature URLs and the public SPKI key). The `.sig` file holds raw P1363 bytes. It is not DER, base64 or a product-release signature envelope.

Upload `realms.json` and `realms.sig` together, as one release, to the HTTPS URLs named in `authority.json`. The served files must be byte-for-byte identical to the output. Never serve the private key. The client does not fetch `authority.json` at run time. It is an input to the trusted client build, passed with the existing property:

```bash
dotnet publish src/OpenConquer.Client -c Release \
  -p:OpenConquerCatalogAuthorityFile=/absolute/new/private-staging-directory/authority.json
```

**Do not quietly replace a client's existing trust anchor, and never ship a production client built with the development key.** The client still accepts signed v1 directories, but this command only writes v2. A valid directory does not enable Enter. Enter stays disabled until 5517 authentication is implemented and independently verified. Publishing over HTTP (the tool only writes local files), server status probes, the AccountServer protocol, key rotation and login submission are not implemented.

## Security notes

- The private-key path must resolve to a regular file, not a link. On Unix, the file must not be accessible to group or other users. The definition file and the output directory must not overlap.
- Review any definition you did not write yourself before signing it. The tool rejects malformed or oversized JSON before it signs anything.
- A production deployment must track the sequence so that it always increases, and must schedule new publications around the expiry time. Uploading one file before the other can make the directory briefly unavailable, but it never makes a client accept a directory it should reject.
- A successful publication only shows that the directory is correctly signed. It does not show that its hosts are reachable, and the 5517 login path is still incomplete.

## Testing

- Run `RealmCatalogPublisherTests`, the full `OpenConquer.Product.Tool.Tests` suite and the full solution tests, together with a locked restore, the formatter check, Windows and macOS CI and the client content-set verification.
- Load a signed test directory end to end through the existing `LoginCatalogClient`, and take a screenshot with a real graphics driver that shows the signed server icons.
