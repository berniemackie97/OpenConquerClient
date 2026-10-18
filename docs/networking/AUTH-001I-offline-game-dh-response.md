# AUTH-001I — Native client GameServer DH response and session material (offline)

## Proven baseline

Client `main` `0e34e1cd200c42acdc26ab20cfbed956b3c26075`
includes the verified AUTH-001G challenge decoder and AUTH-001H fixed-key
CAST5-CFB64 bootstrap transform. No GameServer socket, live DH exchange,
or secured game frame transport exists yet.

Evidence:
- Original retail 5517 native investigation:
  `20_notes/Network/13_connection_lifecycle.md`, functions
  `CClientSecurity__ProcessServerDhChallenge` (0x753801),
  `CClientSecurity__GenerateClientDhResponse` (0x753669), and
  `CGameSocket__ReceiveMsg` (0x6E1C83).
- `20_notes/Network/14_session_derived_legacy_cipher_mask.md`
  documents independent shared-secret/key-schedule initialization.
- OpenConquerServer `GameHandshakeExchange.Complete` and
  `GameClientKeyExchangeResponseDecoder` confirm key material, server
  parser framing, direction-specific IVs, and one-use session semantics.

## Native 5517 protocol

The client responds to a validated, already-decrypted challenge using
the *pinned* 512-bit DH prime and generator `0x05`. A fresh private exponent
is produced with the system CSPRNG and uniform rejection sampling in
`2..p-2`; the resulting public key is `g^x mod p` and the shared secret is
`serverPublic^x mod p`. Server public keys were previously checked to be
strictly within `(1,p-1)` by AUTH-001G.

Native OpenSSL `DH_compute_key` writes a **minimal big-endian** shared secret
at byte zero of a previously zeroed `DH_size`-sized buffer (64 bytes).
**Do not left-pad** it to a fixed 64-byte big-endian integer. The client
passes the complete 64-byte buffer to the subsequent CAST5 key expansion,
which uses at most its first 16 bytes. This is an interoperability rule,
not a recommendation for new cryptographic protocols.

The client response is not a normal `[length:u16][packetType:u16]` frame:

| Offset | Size | Content |
| --- | --- | --- |
| 0 | 7 | Fresh random prefix |
| 7 | 4 | Little-endian length of the remaining envelope |
| 11 | 4 + n | Little-endian length and random padding, 10–49 bytes |
| next | 4 + k | Little-endian length and uppercase public-key hex |
| last 8 | 8 | ASCII `TQClient` |

It is encrypted with a **fresh** fixed-key CAST5-CFB64 instance using
the independent zero IV, not with the newly computed DH session key.
The native random prefix and padding are opaque; no deterministic
production seed or insecure random generator is permitted. The
server's response decoder limits declared lengths to 31–1017 and
parses exactly the two chunks plus trailer.

## Ownership and lifecycle

`GameClientDhResponseEnvelope` is an exact-length atomic plaintext writer.
It validates prefix, padding and public-key lengths and content before
modifying the destination. Inputs may overlap its destination because
all variable content is copied into fixed stack spans first.

`GameClientDhExchange.Create` is the production entry point; it uses only
`RandomNumberGenerator` for private key, prefix and padding. Rejection
sampling is bounded (128 tries). The internal parameterized construction
uses the same full checks and is for deterministic protocol verification
only; it must not receive caller-provided entropy in a future live flow.

Each exchange owns one encrypted response, one 64-byte native session-key
material buffer and both copied eight-byte directional IVs. The response
can be written to an exactly sized caller-owned buffer **once**. Session
material can subsequently be transferred to three mutually nonoverlapping,
exactly sized caller buffers **once**. Invalid destinations are rejected
without touching outputs or consuming either issuance. The exchange
explicitly zeroes its owned secrets and response buffers at disposal and
also clears its internal key/IVs immediately after transfer.

The caller must clear all copies of encrypted response and, especially,
session key material after use. The future session owner must treat a
partially written response as terminal: successful in-memory copying is
NOT proof that the response reached the server. No retry or credential
submission is enabled by this slice.

BigInteger and the third-party CAST implementation may internally allocate
copies of secret state not guaranteed to be wiped by the runtime. This
limitation is not hidden; temporary byte arrays are explicitly cleared.
No secret is logged, printed or persisted.

## Tests and security boundary

A fixed independent reference uses test-only exponent `2`, server public
`0x19` (i.e., `5^2`), seven prefix bytes `01..07`, and ten `AA`
padding bytes. The exact 39-byte plaintext and independently computed
CAST5-CFB64 ciphertext are pinned, along with the resulting 64-byte
`02 71 00 ... 00` secret layout. The production RNG test decrypts a fresh
response, validates its structure, computes the server-side matching
shared secret from the public key, and confirms directional IVs and
right-side zero padding without requiring deterministic randomness.
Negative tests cover invalid ranges and lengths, destination overlap,
atomicity, single issuance, transfer ordering, use after disposal, and
response parser compatibility.

**Security qualification:** This is the native game's legacy **512-bit DH
group**, not modern, authenticated key agreement. DH groups of this size are
inadequate for secure production authentication, and fixed-key CAST5
bootstrap obfuscation does not authenticate any remote peer.
The prior loopback-only AccountServer credential guard remains unchanged.

No GameServer TCP connection, remote redirect trust, secured CAST5 session
cipher, frame reader/writer, proof 1052 send, world admission or login UI
is introduced. Those require a separate endpoint-trust design and fully
bounded connection lifecycle before a live client can safely use this code.

Acceptance: locked restore, formatting, Release build, full tests,
content-set verification, macOS/Windows/Linux Quality, Dependency Review,
independent review findings and explicit user merge decision.
