# AUTH-001G — Native GameServer DH challenge envelope (offline)

## Audited dependency and scope

Baseline `OpenConquerClient` `main` `eb19ba2dfc0060760e35426f9ac56022cb2acaf2`, after AUTH-001F (#123).
The merged client has the AccountServer 1059/1060/1055, 1100/account-1052
continuation and offline GameServer 1052 proof, but **no** GameServer CAST5,
DH, or TCP transport yet.

Primary native evidence: `20_notes/Network/13_connection_lifecycle.md`,
`CClientSecurity__ProcessServerDhChallenge` (`0x753801`),
`CClientSecurity__GenerateClientDhResponse` (`0x753669`),
`CGameSocket__ReceiveMsg` (`0x6E1C83`), and
`20_notes/Network/14_session_derived_legacy_cipher_mask.md`.
Independent server contract: OpenConquerServer
`GameHandshakeExchange.BuildChallenge` and its existing protocol tests.

This slice **only validates the plaintext of one already-decrypted challenge**.
It does not implement the fixed-key CAST5 transform, create a socket,
perform DH, compute a secret, negotiate session crypto, authorize a peer,
or transmit packet 1052. A parsed DH key is not authentication.

## Native and server-compatible envelope

The GameServer sends a **raw**, bootstrap CAST5-CFB64-encrypted handshake
envelope, not an ordinary length/type packet. The next slice's transport must
decrypt an initial **15 bytes**, then bound and read precisely the remainder,
without consuming any following secured packet.

| Plaintext offset | Length | Purpose |
| --- | --- | --- |
| 0 | 11 | Opaque prefix bytes |
| 11 | 4 | Signed little-endian declared length |
| 15 | variable | Six consecutive `[length:i32LE][bytes]` entries |
| last 8 | 8 | ASCII `TQServer` signature |

The native client accepts declared lengths `12..1024` and considers
`11 + declared` bytes the logical challenge. It historically decrypts
four bytes beyond this logical end from offset 15; **do not reproduce that
out-of-bounds behavior**. The managed parser requires a complete exact
frame and a valid trailer, with no read-ahead, extra bytes, or trailing
chunks.

The six ordered entries are opaque junk/padding; server outbound IV
(client inbound IV, 8 bytes); server inbound IV (client outbound IV,
8 bytes); DH prime hex; generator hex; server public key hex.
The server currently emits 12 bytes of junk, 8-byte IVs, a fixed
128-hex-character prime and `05` generator. The reader accepts bounded
nonempty junk (`1..64`) but **pins the actual DH group** to the
OpenConquerServer values and accepts only the public-key interval
`2..p-2`. Hex public keys can be odd length, as in OpenSSL output.

The strict group check is a deliberate application compatibility/security
restriction: other groups, even if accepted by the original binary, require
separate evidence and cryptographic review. Rejected hostile chunk lengths
never allocate based on their declared value; invalid structure fails closed.

## Ownership and trust

`GameServerChallengeDecoder.TryGetFrameLength` returns false only until
15 decrypted prefix bytes are available, then returns a bounded total
length or throws on an impossible declaration.
`TryRead` returns false for incomplete frames, throws for malformed or
overlong frames, and outputs a fully validated challenge only on success.
The challenge result can be created only through a validating factory with a
private constructor. Both IVs are copied, and only read-only spans are exposed;
invalid keys and mutated caller arrays cannot forge its stored parameters.
The temporary decrypted frame can therefore be cleared by its future owner. The public key is
encoded in ASCII only after length, hex and group-range checks.
There is no mutable process-global crypto state, unbounded input allocation,
retry, opportunistic parsing, or speculative protocol variant.

The fixed CAST5 bootstrap key is protocol compatibility, **not**
authenticated encryption. Neither the decrypted challenge's signature nor
a successful DH exchange proves the connected server's identity. The
AccountServer 1055 redirect remains untrusted without independent endpoint
authority. The existing AUTH-001D loopback-only credential restriction is
unchanged.

## Acceptance criteria and continuation

- Fixed 207-byte decrypted reference fixture derived independently from the
  OpenConquerServer challenge builder's literal prime, group, chunks, and trailer.
  It is not claimed to be an observed live-server capture; the reference bytes
  do not depend on production decoder constants.
- Prefix and body fragmentation, malformed declared lengths, corrupted
  chunk lengths, wrong trailer, excess bytes, unexpected group/generator,
  unsafe DH public keys, missing fields and copied IV ownership.
- No changes to authenticated directory, AccountServer traffic, login UI,
  GameServer networking, native account variants, rendering or launcher.
- Locked restore, format verification, Release build, all tests, content
  verification, macOS/Windows/Linux Quality, Dependency Review, reviewer
  findings and final independent audit.

The next separate audited slice can add fixed-key CAST5-CFB64 bootstrap
decryption, followed by client DH key exchange and transport ownership.
Do not put this plain-envelope decoder on a live unauthenticated remote
connection and call it a secure login.
