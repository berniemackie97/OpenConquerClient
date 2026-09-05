# Retail 5517 Server.dat Compatibility Record

## Status

**Verified legacy compatibility evidence; offline tooling only.**

Retail `Server.dat` is preserved because it is important evidence for the native 5517 login/server
bootstrap flow.

It is **not** part of the modern OpenConquer client runtime architecture.

The production policy is:

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

There is no runtime fallback to `Server.dat`, no hard-coded replacement server list, and no
translation of the historical file into a modern runtime realm catalog.

## Retail Identity

The audited retail 5517 file is:

```text
length: 2816 bytes
SHA-256: 0b4d366786aa4498c7e470f10fd8bca716bc1d6cbda1eb3894666183f8327a90
RSA blocks: 11 × 256 bytes
```

The exact fixture is tracked at:

```text
tests/OpenConquer.Content.Tool.Tests/TestData/retail-5517/Server.dat
```

The fixture is test/tooling evidence. It is deliberately not staged under
`content/retail-5517/payload`.

## Native Location and Lookup Behavior

Native 5517 reads `Server.dat` directly from the client root.

The audited behavior is a loose-file operation rather than WDF package lookup.

That fact remains compatibility evidence, but the modern implementation no longer needs to model it
through `ContentLookupMode.LooseOnly` because the modern runtime does not read `Server.dat` at all.

Offline tooling instead accepts an explicit filesystem path:

```text
operator-selected file
        ↓
ServerDatFileReader
```

No package lookup or fallback is involved.

## Cryptographic Envelope

Native `CConfigDataTableQueryProvider` constructs a 2048-bit RSA public key using exponent `65537`.

The independently recovered unsigned big-endian modulus has raw 256-byte SHA-256:

```text
76acb04b08190b129985f8dee2b466efcd686eb1662cb598bd1a8154cb9196f1
```

OpenConquer stores the independently verified final modulus directly.

It does not reproduce the native constructor's seed schedule and BIGNUM assembly in production
tooling. Those construction details remain compatibility evidence and are locked by tests where
useful.

The verified decode pipeline is:

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

The inflated XML SHA-256 is:

```text
5d6b00ff722a8b37aa2981affecd478aee73bdc22cdc498a25b700242b55c35a
```

## Security Interpretation

The historical RSA construction should not be treated as encryption that keeps the server list
secret from the client.

The client contains the public material required to recover the payload.

The useful property of the native construction is instead consistent with publisher-controlled
authenticity and tamper resistance: data produced with the corresponding private-key operation can
be checked or recovered by the client using embedded public material.

That historical mechanism is not reused as a modern production trust system.

Modern service discovery, authentication, routing, and update trust each belong to their appropriate
modern security boundary.

## Hardened Tooling Decoder

The preserved decoder treats every supplied file as untrusted input.

The envelope boundary requires:

- non-empty input;
- complete 256-byte RSA blocks;
- no more than 64 encrypted blocks;
- a 256-byte public modulus;
- every RSA representative to be strictly less than the modulus;
- PKCS#1 type-1 prefix `00 01`;
- at least eight `FF` padding bytes;
- a zero separator;
- a non-empty extracted payload chunk;
- gzip signature validation;
- a maximum inflated XML size of 1 MiB;
- deterministic malformed-data rejection.

The file reader additionally bounds the encrypted file to:

```text
64 × 256 bytes = 16384 bytes
```

before allowing the complete encrypted payload to accumulate in memory.

## XML Structure

The inflated document must contain exactly one:

```text
table_data[name=outenserver]
```

The parser:

- prohibits DTD processing;
- disables external XML resolution;
- bounds parsed document characters;
- rejects duplicate `outenserver` tables;
- rejects duplicate row IDs;
- rejects duplicate field names within a row;
- bounds group count to 1024;
- bounds servers per group to 100;
- uses checked row-index arithmetic;
- requires structurally required rows and fields.

Unknown fields are tolerated so the parser remains narrowly coupled to the verified fields it
actually consumes.

## Verified Retail Schema

The audited fields are:

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

The first server row for a group is derived as:

```text
0x65 + (groupIndex - 1) × 0x64
```

and the group's `Child` value determines how many sequential server rows belong to it.

The 100-row stride is structural native evidence. The modern tooling parser therefore rejects a
per-group server count greater than 100.

## Historical Model Semantics

The tooling model intentionally preserves source-format terminology:

```text
ServerDatCatalog
ServerDatGroup
ServerDatServer
```

and historical field semantics such as:

```text
FlashName
FlashIcon
FlashHint
ServerName
ServerIp
ServerPort
```

These names are deliberately not projected into generic modern properties such as:

```text
DisplayName
Host
Port
Realm
Endpoint
```

because doing so would incorrectly imply modern runtime semantics.

The inspected retail fixture demonstrates why this distinction matters.

Several rows contain different `FlashName` and `ServerName` values. Examples include:

```text
FlashName="Water"       ServerName="Fire"
FlashName="Cerberus"    ServerName="Gryphon"
FlashName="Pegasus"     ServerName="Basilisk"
FlashName="Cinderella"  ServerName="SnowWhite"
```

`FlashName` is therefore not interchangeable with the native protocol server identifier.

## Protocol Significance of ServerName

Native 5517 account-login protocol evidence shows that `ServerName` participates in the login
boundary.

`MsgAccount` packet type `1060` contains a 16-byte server-name field at offset `+260`.

The historical `ServerName` field must therefore remain distinct from presentation metadata.

The runtime server-selection design must preserve the protocol value independently of a display
label or any modern catalog identifier. Removing the historical file does not remove this native
login requirement. The runtime source and mapping require their own native/server contract audit.

## ServerIP and ServerPort

Historical `ServerIP` and `ServerPort` describe the native pre-authentication login/account ingress
represented by `Server.dat`.

They are preserved as source text by the tooling model.

The tooling parser deliberately does not reinterpret them as modern runtime endpoints.

In particular:

- `ServerIP` is not converted into a modern host abstraction;
- `ServerPort` is not parsed into a runtime networking endpoint;
- no connection is initiated from inspection tooling;
- no runtime fallback uses these values.

The native post-authentication world handoff is a separate protocol concern.

## Modern Runtime Decision

Retail `Server.dat` remains outside the production runtime. Its removal changes the source of server
configuration, not the native account-authentication protocol.

The launcher must preserve original 5517 AccountServer authentication, credential transformations,
results, and login-to-game handoff. A replacement runtime source for server discovery/selection
requires an explicit trust and ownership boundary, with native protocol server names kept distinct
from UI labels. Endpoints must not be hardcoded in UI code.

The earlier proposed post-authentication realm-routing lifecycle is superseded by the native-parity
reconstruction requirement. Do not infer new connection-ticket or identity-provider protocols from
this historical decoder. No runtime server catalog or authentication implementation exists yet.

See [launcher architecture](../architecture/architecture.md#native-parity-authentication-and-launcher-lifecycle)
and [the remaining roadmap](../architecture/launcher-roadmap.md).

## Tooling Ownership

All retained `Server.dat` implementation lives beneath:

```text
tools/OpenConquer.Content.Tool/Legacy/ServerDat/
```

Tests live beneath:

```text
tests/OpenConquer.Content.Tool.Tests/Legacy/ServerDat/
```

The exact retail fixture lives beneath:

```text
tests/OpenConquer.Content.Tool.Tests/TestData/retail-5517/
```

There is intentionally no speculative `OpenConquer.Legacy` runtime library.

If a second independent non-runtime consumer eventually requires this code, ownership can be
reevaluated from actual requirements.

## Inspection Command

The supported offline inspection command is:

```bash
dotnet run --project tools/OpenConquer.Content.Tool -- \
  inspect-server-dat \
  --file /path/to/Server.dat
```

The report includes:

- absolute inspected path;
- group count;
- total server-row count;
- group IDs and historical Flash metadata;
- server IDs;
- `FlashName`;
- `FlashIcon`;
- `FlashHint`;
- `ServerName`;
- `ServerIP`;
- `ServerPort`.

Source strings are escaped before rendering so embedded control characters cannot inject fake report
lines.

## Runtime Content Policy

`Server.dat` is intentionally absent from:

```text
ClientContentClosure
content/retail-5517/payload
content/retail-5517/manifest.json
published OpenConquer.Client content
```

The current runtime content closure therefore contains five files:

```text
data/main/Logo1.bmp
data/main/Logo2.bmp
ini/GameSetUp.ini
ini/info.ini
ini/package.ini
```

The exact retail `Server.dat` remains preserved independently as compatibility evidence.

## Verification

Parity tests permanently lock:

- encrypted fixture length;
- encrypted fixture SHA-256;
- 11-block encrypted shape;
- native public modulus identity;
- inflated XML length;
- inflated XML SHA-256;
- 14-group structure;
- hardened RSA/PKCS#1/gzip rejection behavior;
- hardened XML parsing behavior.

The `inspect-server-dat` command has also been exercised successfully against the exact tracked
retail fixture and reports:

```text
Groups: 14
Servers: 94
```

This boundary preserves the historical evidence without making obsolete deployment mechanics part of
the modern production architecture.
