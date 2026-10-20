# Retail 5517 Server.dat Compatibility Record

## Status

Offline tooling only. The OpenConquer client does not use `Server.dat` at runtime.

The repository keeps the retail `Server.dat` because it documents the 5517 login and server
bootstrap flow.

How the file is used:

```text
retail Server.dat
        │
        ├── preserved exact fixture
        ├── parity tests
        └── offline inspection tooling

modern client runtime
        │
        └── no Server.dat dependency
```

The runtime never falls back to `Server.dat`. There is no hard-coded server list to replace it, and
the file is not converted into a runtime realm catalog.

## Retail Identity

Identity of the retail 5517 file:

```text
length: 2816 bytes
SHA-256: 0b4d366786aa4498c7e470f10fd8bca716bc1d6cbda1eb3894666183f8327a90
RSA blocks: 11 × 256 bytes
```

A byte-for-byte copy is tracked as a test fixture at:

```text
tests/OpenConquer.Content.Tool.Tests/TestData/retail-5517/Server.dat
```

The fixture is used only by tests and tooling, so it is not staged under
`content/retail-5517/payload`.

## Location and Lookup

The 5517 client reads `Server.dat` directly from the client root, as a loose file. It does not look
it up in a WDF package.

OpenConquer does not need to model this with `ContentLookupMode.LooseOnly`, because its runtime does
not read `Server.dat` at all.

The offline tooling takes a filesystem path instead:

```text
operator-selected file
        ↓
ServerDatFileReader
```

No package lookup or fallback is involved.

## Cryptographic Envelope

In the retail client, `CConfigDataTableQueryProvider` builds a 2048-bit RSA public key with exponent
`65537`.

The modulus was recovered separately. Its SHA-256, taken over the raw 256-byte unsigned big-endian
value, is:

```text
76acb04b08190b129985f8dee2b466efcd686eb1662cb598bd1a8154cb9196f1
```

OpenConquer stores this final modulus directly.

The tooling does not reproduce the retail constructor's seed schedule and BIGNUM assembly. Tests
cover those construction details where useful.

Decoding goes through these steps:

```text
11 × 256-byte RSA blocks
        ↓
RSA public operation
        ↓
PKCS#1 type-1 extraction
        ↓
2495-byte concatenated gzip payload
        ↓
38819-byte XML
        ↓
table_data[name=outenserver]
```

The SHA-256 of the inflated XML is:

```text
5d6b00ff722a8b37aa2981affecd478aee73bdc22cdc498a25b700242b55c35a
```

## Security Notes

The RSA layer does not keep the server list secret from the client. The client ships with the public
key needed to recover the payload.

What the scheme does provide is consistent with publisher-controlled authenticity and tamper
resistance: data produced with the matching private key can be checked or recovered by the client
with the embedded public key.

OpenConquer does not reuse this mechanism to establish trust. Service discovery, authentication,
routing, and update trust each need their own security design.

## Tooling Decoder

The decoder treats every input file as untrusted.

The envelope checks require:

- non-empty input
- complete 256-byte RSA blocks
- no more than 64 encrypted blocks
- a 256-byte public modulus
- every RSA representative to be less than the modulus
- PKCS#1 type-1 prefix `00 01`
- at least eight `FF` padding bytes
- a zero separator
- a non-empty extracted payload chunk
- a valid gzip signature
- an inflated XML size of at most 1 MiB
- reproducible rejection of malformed data

Before it reads the whole encrypted payload into memory, the file reader checks the file against
this size limit:

```text
64 × 256 bytes = 16384 bytes
```

## XML Structure

The inflated document must contain exactly one:

```text
table_data[name=outenserver]
```

The parser:

- disables DTD processing
- disables external XML resolution
- limits the number of characters it parses
- rejects duplicate `outenserver` tables
- rejects duplicate row IDs
- rejects duplicate field names within a row
- allows at most 1024 groups
- allows at most 100 servers per group
- uses overflow-checked arithmetic for row indexes
- requires the rows and fields the structure depends on

Unknown fields are ignored, so the parser depends only on the fields it reads.

## Retail Schema

The retail file uses these fields:

```text
id
Child
FlashName
FlashIcon
FlashHint
ServerName
ServerIP
ServerPort
```

Root row `id=0` declares the number of groups.

Retail 5517 contains:

```text
groups: 14
servers: 94
```

Group rows use IDs `1..14`.

The ID of a group's first server row is:

```text
0x65 + (groupIndex - 1) × 0x64
```

The group's `Child` value gives the number of consecutive server rows that belong to it.

The retail format spaces groups 100 rows apart, so the tooling parser rejects any group with more
than 100 servers.

## Model Naming

The tooling model keeps the names used by the file format:

```text
ServerDatCatalog
ServerDatGroup
ServerDatServer
```

It also keeps the original field names and meanings:

```text
FlashName
FlashIcon
FlashHint
ServerName
ServerIp
ServerPort
```

These fields are not mapped to generic properties like the ones below, because those names would
suggest a runtime meaning the fields do not have:

```text
DisplayName
Host
Port
Realm
Endpoint
```

The retail fixture shows why this matters. In several rows, `FlashName` and `ServerName` differ:

```text
FlashName="Water"       ServerName="Fire"
FlashName="Cerberus"    ServerName="Gryphon"
FlashName="Pegasus"     ServerName="Basilisk"
FlashName="Cinderella"  ServerName="SnowWhite"
```

`FlashName` therefore cannot stand in for the server identifier that the protocol uses.

## ServerName in the Login Protocol

In the 5517 account-login protocol, `ServerName` is part of the login request.

`MsgAccount` packet type `1060` contains a 16-byte server-name field at offset `+260`.

`ServerName` therefore has to stay separate from display metadata.

Runtime server selection must carry the protocol value separately from any display label or catalog
identifier. Dropping the file does not remove this login requirement. Where the runtime gets the
value, and how it maps to a server, still has to be checked against the retail client and server.

## ServerIP and ServerPort

`ServerIP` and `ServerPort` give the account (login) server that the 5517 client contacts before
authentication.

The tooling model keeps them as the original text. The parser does not turn them into runtime
endpoints:

- `ServerIP` is not converted into a host type
- `ServerPort` is not parsed into a network endpoint
- the inspection tool never opens a connection
- nothing at runtime falls back to these values

The handoff to the game world after authentication is a separate part of the protocol.

## Runtime Decision

Retail `Server.dat` is not used at runtime. Leaving it out changes where server configuration comes
from. It does not change the 5517 account-authentication protocol.

The launcher must keep the original 5517 AccountServer authentication, credential transformations,
results, and login-to-game handoff. Any new runtime source for server discovery and selection needs
a clear owner and trust model, and must keep protocol server names separate from UI labels.
Endpoints must not be hardcoded in UI code.

Runtime networking aims for parity with the 5517 login flow. This replaces an earlier proposal for a
post-authentication realm-routing lifecycle. This decoder is not a basis for new connection-ticket
or identity-provider protocols. No runtime server catalog or authentication implementation exists
yet.

See the
[native-parity networking direction](../architecture/architecture.md#native-parity-networking-direction)
for the runtime design that replaces `Server.dat` discovery.

## Tooling Ownership

All `Server.dat` code lives under:

```text
tools/OpenConquer.Content.Tool/Legacy/ServerDat/
```

Tests live under:

```text
tests/OpenConquer.Content.Tool.Tests/Legacy/ServerDat/
```

The retail fixture lives under:

```text
tests/OpenConquer.Content.Tool.Tests/TestData/retail-5517/
```

There is no `OpenConquer.Legacy` runtime library. If another non-runtime consumer needs this code
later, where it lives can be decided then, based on what that consumer needs.

## Inspection Command

To inspect a file offline:

```bash
dotnet run --project tools/OpenConquer.Content.Tool -- \
  inspect-server-dat \
  --file /path/to/Server.dat
```

The report includes:

- the absolute path of the inspected file
- group count
- total server-row count
- group IDs and their Flash metadata
- server IDs
- `FlashName`
- `FlashIcon`
- `FlashHint`
- `ServerName`
- `ServerIP`
- `ServerPort`

Strings from the file are escaped before printing, so embedded control characters cannot inject fake
report lines.

## Runtime Content Policy

`Server.dat` is left out of:

```text
ClientContentClosure
content/retail-5517/payload
content/retail-5517/manifest.json
published OpenConquer.Client content
```

The runtime content closure is the set resolved by `ClientContentClosure` and recorded in
`content/retail-5517/manifest.json`. The
[retail 5517 content plan](../content/retail-5517-content-plan.md) describes what it contains.
`Server.dat` is never part of it.

The retail `Server.dat` is kept separately, as the test fixture described above.

## Verification

Parity tests pin:

- the encrypted fixture length
- the encrypted fixture SHA-256
- the 11-block encrypted layout
- the retail public modulus
- the inflated XML length
- the inflated XML SHA-256
- the 14-group structure
- rejection of bad RSA, PKCS#1, and gzip input
- the XML parser hardening

Running `inspect-server-dat` on the tracked retail fixture reports:

```text
Groups: 14
Servers: 94
```
