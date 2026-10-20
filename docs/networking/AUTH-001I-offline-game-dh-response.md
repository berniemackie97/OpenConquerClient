# AUTH-001I: Client GameServer DH response and session material (offline)

AUTH-001I builds the client's encrypted DH response to the GameServer
challenge and derives the session key material, without any network I/O. It
builds on the AUTH-001G challenge decoder and the AUTH-001H fixed-key
CAST5-CFB64 bootstrap transform. It adds no GameServer socket, live DH
exchange or secured game frame transport.

## Sources

- Research on the original retail 5517 client:
  `20_notes/Network/13_connection_lifecycle.md`, functions
  `CClientSecurity__ProcessServerDhChallenge` (0x753801),
  `CClientSecurity__GenerateClientDhResponse` (0x753669) and
  `CGameSocket__ReceiveMsg` (0x6E1C83).
- `20_notes/Network/14_session_derived_legacy_cipher_mask.md` documents how
  the shared secret and key schedule are initialized independently.
- OpenConquerServer `GameHandshakeExchange.Complete` and
  `GameClientKeyExchangeResponseDecoder` confirm the key material, the
  framing expected by the server parser, the direction-specific IVs and the
  single-use session semantics.

## 5517 protocol

The client answers a validated, already-decrypted challenge using the pinned
512-bit DH prime and the generator `0x05`. A fresh private exponent comes
from the system CSPRNG, using uniform rejection sampling in `2..p-2`. The
public key is `g^x mod p` and the shared secret is `serverPublic^x mod p`.
AUTH-001G has already checked that the server public key lies in the open
interval `(1,p-1)`.

The retail client calls OpenSSL `DH_compute_key`, which writes the shared
secret as a minimal big-endian number starting at byte zero of a previously
zeroed buffer of `DH_size` bytes (64 bytes). The secret must not be
left-padded to a fixed 64-byte big-endian integer. The client passes the
whole 64-byte buffer to the CAST5 key expansion that follows, which uses at
most its first 16 bytes. This is an interoperability rule, and not a
recommendation for new cryptographic protocols.

The client response does not use the usual `[length:u16][packetType:u16]`
frame:

| Offset | Size | Content |
| --- | --- | --- |
| 0 | 7 | Fresh random prefix |
| 7 | 4 | Little-endian length of the remaining envelope |
| 11 | 4 + n | Little-endian length and random padding, 10 to 49 bytes |
| next | 4 + k | Little-endian length and uppercase public-key hex |
| last 8 | 8 | ASCII `TQClient` |

The response is encrypted with a fresh fixed-key CAST5-CFB64 instance that
starts from its own zero IV. It is not encrypted with the newly computed DH
session key. The random prefix and padding are opaque. Production code must
not use a fixed seed or an insecure random generator for them. The server's
response decoder limits declared lengths to 31 to 1017, and parses the two
chunks plus the trailer and nothing else.

## Types and lifetime

`GameClientDhResponseEnvelope` writes the plaintext atomically into a
destination of the required length. It validates the length and content of
the prefix, the padding and the public key before changing the destination.
Inputs may overlap the destination, because all variable content is first
copied into fixed stack spans.

`GameClientDhExchange.Create` is the production entry point. It uses only
`RandomNumberGenerator` for the private key, prefix and padding. Rejection
sampling gives up after 128 tries. The internal constructor that takes these
values as parameters runs the same full checks and exists only for
reproducible protocol tests. A future live flow must never pass
caller-provided entropy to it.

Each exchange owns one encrypted response, one 64-byte buffer of 5517
session key material and copies of both eight-byte directional IVs. The
response can be written once, into a caller-owned buffer of the required
size. The session material can then be transferred once, into three
caller-owned buffers of the required sizes that must not overlap each other.
Invalid destinations are rejected without touching any output and without
using up either issuance. The exchange zeroes its secrets and response
buffers on disposal, and clears its internal key and IVs right after the
transfer.

The caller must clear every copy of the encrypted response and, above all,
of the session key material after use. The future session owner must treat a
partially written response as fatal to the session, because a successful
copy in memory does not prove that the response reached the server.
AUTH-001I enables no retries and no credential submission.

BigInteger and the third-party CAST implementation may make internal copies
of secret state that the runtime does not guarantee to wipe. This limitation
is stated openly here. Temporary byte arrays are cleared. No secret is
logged, printed or persisted.

## Testing

A fixed independent reference uses the test-only exponent `2`, the server
public key `0x19` (that is, `5^2`), the seven prefix bytes `01..07` and ten
`AA` padding bytes. The 39-byte plaintext and the independently computed
CAST5-CFB64 ciphertext are pinned, along with the resulting 64-byte secret
layout `02 71 00 ... 00`. The production RNG test decrypts a fresh response,
validates its structure, computes the matching server-side shared secret
from the public key, and checks the directional IVs and the zero padding on
the right. It does not need reproducible randomness. Negative tests cover
invalid ranges and lengths, destination overlap, atomicity, single issuance,
transfer ordering, use after disposal and compatibility with the response
parser.

The change passes locked restore, formatting, Release build, full tests,
content-set verification, and the macOS, Windows, Linux Quality and
Dependency Review jobs.

## Security

**Security warning:** the 5517 game uses a legacy 512-bit DH group with no
authentication. DH groups of this size are not adequate for secure
production authentication, and the fixed-key CAST5 bootstrap obfuscation
does not authenticate any remote peer. The loopback-only AccountServer
credential guard is unchanged.

AUTH-001I adds no GameServer TCP connection, trust in remote redirects,
secured CAST5 session cipher, frame reader or writer, proof 1052 send, world
admission or login UI. Before a live client can use this code safely, it
needs a separate design for endpoint trust and a connection lifecycle with a
time limit on every phase.
