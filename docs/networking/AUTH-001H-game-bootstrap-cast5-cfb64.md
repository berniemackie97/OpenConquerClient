# AUTH-001H — Native GameServer bootstrap CAST5-CFB64

## Baseline and evidence

Verified client `main` `7dcff85a1b5e241827cf3fd2eb6d3944a70dc2e6`,
after merged AUTH-001G (#125). The client already validates one
**decrypted** GameServer DH challenge envelope in
`GameServerChallengeDecoder`; it has no active GameServer transport.

Evidence is the original 5517 native network/handshake research in
`20_notes/Network/13_connection_lifecycle.md` and the independently
implemented OpenConquerServer
`GameHandshakeBootstrapCipher`,
`GameCast5Cfb64Cipher`, and their test fixtures.
The server encrypts its initial DH challenge using fixed-key
CAST5-CFB64; its client-key-response parser uses the same fixed-key
bootstrap encryption before the per-session cipher transition.

## Exact transform

- Block primitive: CAST5 (CAST-128), using the 16-byte fixed ASCII key
  `BC234xs45nme7HU9`.
- Mode: CFB with 64-bit feedback, byte-by-byte keystream consumption.
- IV: eight zero bytes, reinitialized **once per independent envelope**.
- Every 8 bytes: encrypt the current feedback register with CAST5.
- Outgoing byte: `plaintext XOR keystream`; place ciphertext in feedback.
- Incoming byte: `ciphertext XOR keystream`; place **incoming ciphertext**
  in feedback. This matters for in-place decoding.
- The feedback position must survive arbitrary TCP fragmentation within
  a single envelope; it must not be reset after the 15-byte challenge header.

`GameHandshakeBootstrapCipher` owns one fixed-key direction-specific
stream for one envelope and supports both encrypt/decrypt construction.
`Transform` validates complete output capacity and rejects partial
span overlap **before touching state or output**. Identical buffers are
valid. The instance owns no background tasks, sockets, pipes, retries,
or connection state; the eventual session owner must serialize fragments
in network order and dispose when that envelope completes or fails.

This slice uses `BouncyCastle.Cryptography` **2.7.0**, matching the
server and pinned in the client's central versions and locked restore
graphs (Networking, Client, Client.Tests, Rendering.Conformance).
It does not copy, reimplement, or vendor a CAST5 S-box implementation.
Owned feedback and keystream buffers are zeroed at disposal. The
third-party primitive may retain its internal expanded key schedule
until garbage collection; the bootstrap key itself is public and fixed.
Do not claim that disposing this object erases arbitrary provider memory.

## Verification evidence

The independent OpenConquerServer fixed zero-IV vector is:

| Plaintext | Ciphertext (hex) |
| --- | --- |
| `OpenConquer-5517-CFB64` | `6A13741CE64645FCEC5A9D7A9630A3D18A9F3539C28C` |

Tests independently assert this exact output, its decryption, fresh-IV
determinism, state retention across 3/17/1/51/25-byte fragments, and
in-place round trips. They also encrypt and decrypt the 207-byte fixed
server-source-derived AUTH-001G plaintext fixture across its first
15 bytes and later fragments, validating the recovered envelope with
the existing decoder. The 207-byte fixture is not a live captured
ciphertext; only the fixed CAST5 vector independently verifies crypto.

Negative tests cover undersized destination, prohibited overlapping
spans, terminal disposal, and ensuring invalid calls do not consume
feedback state.

## Trust boundary, exclusions, and next slice

CAST5 with an embedded public key and zero IV is **not** authenticated
encryption, does not provide unique session confidentiality, and
cannot authenticate a GameServer peer. A valid `TQServer` trailer and
well-formed DH fields are structural compatibility checks only.
The AccountServer 1055 redirect is still untrusted live metadata
because the legacy AccountServer transport is unauthenticated.

**No GameServer TCP connection, DH response builder, shared-secret
calculation, CAST session-key transition, game proof transmission,
UI login action, or automatic endpoint trust decision is introduced.**
The existing loopback-only account credential guard remains intact.

Only a separately audited handshake-lifetime/endpoint-trust slice may
consume the primitive on a live connection. That slice must use a
single continuous decryptor across the header/body reads, enforce
bounded deadlines, never retry uncertain writes, preserve parser
read-ahead exclusions and prove the endpoint security boundary.

Before merge: locked restore, formatter, Release build/tests,
content verification, macOS/Windows/Quality, Dependency Review,
review response audit and explicit user merge acceptance.
