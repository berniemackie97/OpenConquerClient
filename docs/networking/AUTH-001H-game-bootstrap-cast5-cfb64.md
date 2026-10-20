# AUTH-001H: GameServer bootstrap CAST5-CFB64

AUTH-001H implements the fixed-key CAST5-CFB64 transform that the 5517
GameServer handshake uses for its first envelopes. It builds on AUTH-001G,
whose `GameServerChallengeDecoder` validates one GameServer DH challenge
envelope after decryption. AUTH-001H adds the cipher only, with no
GameServer transport.

## Sources

The sources are the research on the original 5517 network handshake in
`20_notes/Network/13_connection_lifecycle.md`, and the independent
OpenConquerServer implementation: `GameHandshakeBootstrapCipher`,
`GameCast5Cfb64Cipher` and their test fixtures. The server encrypts its
initial DH challenge with fixed-key CAST5-CFB64. Its parser for the client
key response uses the same fixed-key bootstrap encryption, before the
switch to the per-session cipher.

## Transform

- Block cipher: CAST5 (CAST-128) with the 16-byte fixed ASCII key
  `BC234xs45nme7HU9`.
- Mode: CFB with 64-bit feedback. The keystream is consumed one byte at a
  time.
- IV: eight zero bytes, set once at the start of each independent
  envelope.
- Every 8 bytes, the current feedback register is encrypted with CAST5.
- Outgoing byte: `plaintext XOR keystream`. The ciphertext goes into the
  feedback register.
- Incoming byte: `ciphertext XOR keystream`. The incoming ciphertext goes
  into the feedback register, which matters for in-place decoding.
- The feedback position must carry over across any TCP fragmentation
  within one envelope. It must not be reset after the 15-byte challenge
  header.

`GameHandshakeBootstrapCipher` owns one fixed-key stream, in one
direction, for one envelope. It can be constructed for encryption or for
decryption. `Transform` checks that the output has room for the whole
input and rejects partially overlapping spans before it touches any state
or output. Passing the same buffer as input and output is allowed. The
instance owns no background tasks, sockets, pipes, retries or connection
state. The session that eventually owns it must feed it fragments in
network order and dispose of it when the envelope completes or fails.

The cipher uses `BouncyCastle.Cryptography` 2.7.0, the same version as the
server. The version is pinned in the client's central package versions and
in the locked restore graphs of Networking, Client, Client.Tests and
Rendering.Conformance. No CAST5 S-box implementation is copied,
reimplemented or vendored. The feedback and keystream buffers that the
cipher owns are zeroed on disposal. The third-party primitive may keep its
expanded key schedule in memory until garbage collection. The bootstrap
key itself is public and fixed. Disposing this object does not erase
memory held inside the provider.

## Testing

The independent OpenConquerServer vector, with a fixed zero IV, is:

| Plaintext | Ciphertext (hex) |
| --- | --- |
| `OpenConquer-5517-CFB64` | `6A13741CE64645FCEC5A9D7A9630A3D18A9F3539C28C` |

Tests check this output and its decryption independently. They also check
that a fresh zero IV always gives the same output, that state carries over
across 3/17/1/51/25-byte fragments, and that in-place round trips work.
The 207-byte AUTH-001G plaintext fixture is paired with a fixed ciphertext
generated separately with the CAST5/CFB-64 implementation of the Python
`cryptography` package (16-byte 5517 key, 8-byte zero IV). Before the
fixture was produced, that provider was checked against the server's
published short CAST5 ciphertext vector.

The test checks both that encryption gives that independent ciphertext and
that the fixed bytes decrypt correctly when split across the initial
15-byte challenge prefix and later fragments. The recovered envelope must
pass the existing challenge decoder. The fixture is derived from server
source code and was not captured from a live GameServer. It is a
CAST5-CFB64 interoperability reference between two implementations and
says nothing about authenticating a real peer.

Negative tests cover an undersized destination, prohibited overlapping
spans and calls after disposal, and check that invalid calls do not
consume feedback state.

The change passes locked restore, the formatter, Release build and tests,
content verification, and the macOS, Windows, Quality and Dependency
Review jobs.

## Security and scope

CAST5 with a publicly known embedded key and a zero IV is not
authenticated encryption. It gives no per-session confidentiality and
cannot authenticate a GameServer. A valid `TQServer` trailer and
well-formed DH fields are structural compatibility checks only. The
AccountServer 1055 redirect is still untrusted live metadata, because the
legacy AccountServer transport is unauthenticated.

AUTH-001H adds no GameServer TCP connection, DH response builder,
shared-secret calculation, switch to a CAST session key, game proof
transmission, UI login action or automatic endpoint trust decision. The
existing loopback-only guard on account credentials is unchanged.

Only a separately reviewed step that owns the handshake lifetime and
endpoint trust may use this primitive on a live connection. That step must
use one continuous decryptor across the header and body reads, enforce
deadlines, never retry uncertain writes, keep the parser's rule against
reading ahead, and establish how the endpoint is secured.
