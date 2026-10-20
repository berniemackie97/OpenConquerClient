# AUTH-001G: GameServer DH challenge envelope (offline)

AUTH-001G validates the plaintext of one GameServer DH challenge that has
already been decrypted. It builds on the AccountServer 1059/1060/1055
exchange, the 1100/account-1052 continuation and the offline GameServer 1052
proof from AUTH-001F. It adds no GameServer CAST5, DH or TCP transport. The
decoder does not decrypt (the fixed-key CAST5-CFB64 bootstrap transform is
AUTH-001H), open a socket, compute a DH secret, negotiate session crypto,
decide whether to trust a peer or send packet 1052. The client DH exchange and
transport ownership come in later steps. A parsed DH key does not authenticate
the server.

## Sources

- Research on the retail client:
  `20_notes/Network/13_connection_lifecycle.md`,
  `CClientSecurity__ProcessServerDhChallenge` (`0x753801`),
  `CClientSecurity__GenerateClientDhResponse` (`0x753669`),
  `CGameSocket__ReceiveMsg` (`0x6E1C83`), and
  `20_notes/Network/14_session_derived_legacy_cipher_mask.md`.
- Independent server implementation: OpenConquerServer
  `GameHandshakeExchange.BuildChallenge` and its existing protocol tests.

## Envelope format

The GameServer sends a raw handshake envelope encrypted with the bootstrap
CAST5-CFB64 cipher. It is not an ordinary length/type packet. The transport
that reads it must decrypt the first 15 bytes, check the declared length
against the limits, and then read the remainder and nothing more, so that it
does not consume the secured packet that follows.

| Plaintext offset | Length | Purpose |
| --- | --- | --- |
| 0 | 11 | Opaque prefix bytes |
| 11 | 4 | Signed little-endian declared length |
| 15 | variable | Six consecutive `[length:i32LE][bytes]` entries |
| last 8 | 8 | ASCII `TQServer` signature |

The retail client accepts declared lengths `12..1024` and treats
`11 + declared` bytes as the logical challenge. It also decrypts four bytes
past that logical end, counting from offset 15. This client does not reproduce
that out-of-bounds read. The managed parser requires a complete frame of the
declared size and a valid trailer, with no read-ahead, extra bytes or trailing
chunks.

The six entries, in order:

1. Opaque junk/padding
2. Server outbound IV (client inbound IV, 8 bytes)
3. Server inbound IV (client outbound IV, 8 bytes)
4. DH prime, in hex
5. Generator, in hex
6. Server public key, in hex

The server currently sends 12 bytes of junk, 8-byte IVs, a fixed
128-hex-character prime and the generator `05`. The reader accepts nonempty
junk of `1..64` bytes. It pins the DH group to the OpenConquerServer values
and accepts only public keys in the interval `2..p-2`. Hex public keys can
have an odd number of digits, as in OpenSSL output.

Pinning the group is a compatibility and security restriction of this client.
Other groups, even ones the original binary accepts, would need separate
research and cryptographic review. Hostile chunk lengths are rejected without
allocating memory based on the declared value, and any invalid structure is
rejected.

## Types and trust

`GameServerChallengeDecoder.TryGetFrameLength` returns false until 15
decrypted prefix bytes are available. After that it returns the total frame
length, which is always within the limits, or throws if the declared length is
impossible. `TryRead` returns false for incomplete frames, throws for
malformed or overlong frames, and outputs a fully validated challenge only on
success.

The challenge result has a private constructor and can only be created through
a validating factory. Both IVs are copied, and only read-only spans are
exposed, so neither invalid keys nor later changes to the caller's arrays can
forge its stored parameters. The future owner can therefore clear the
temporary decrypted frame. The public key is encoded as ASCII only after its
length, hex digits and group range have been checked. There is no global
mutable crypto state, no allocation sized by untrusted input, and no retry,
opportunistic parsing or speculative protocol variant.

The fixed CAST5 bootstrap key exists for protocol compatibility and is not
authenticated encryption. Neither the `TQServer` signature in the decrypted
challenge nor a successful DH exchange proves who the connected server is. The
AccountServer 1055 redirect stays untrusted unless the endpoint is
authenticated some other way. The loopback-only credential restriction from
AUTH-001D is unchanged. Putting this decoder on a live, unauthenticated remote
connection would not make the login secure.

## Testing

- A fixed 207-byte decrypted reference fixture, built independently from the
  literal prime, group, chunks and trailer in the OpenConquerServer challenge
  builder. It is not a capture from a live server, and the reference bytes do
  not depend on constants in the production decoder.
- Tests cover fragmentation of the prefix and body, malformed declared
  lengths, corrupted chunk lengths, a wrong trailer, excess bytes, an
  unexpected group or generator, unsafe DH public keys, missing fields and
  ownership of the copied IVs.
- The authenticated directory, AccountServer traffic, login UI, GameServer
  networking, 5517 account variants, rendering and launcher are unchanged.
- Locked restore, format verification, Release build, all tests, content
  verification, and the macOS, Windows, Linux Quality and Dependency Review
  jobs pass.
