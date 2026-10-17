# AUTH-001B — Bounded AccountServer seed session

## Source of truth

- Main baseline: `ed4edc7264d7aaee9e43c0c5100948d33ec7b9d0`, after AUTH-001A PR #114.
- `20_notes/Architecture/03_login_flash_startup_command_contract.md`: native
  AccountServer legacy mode 1 uses directional stream counters without a `TQClient` trailer.
- `20_notes/Network/5517-server-selection-and-realm-handoff.md`: native AccountServer connect
  timeout is 10 seconds; legacy Server.dat/BGP fallback is not trusted modern endpoint authority.
- `OpenConquerServer` `LoginConnectionSession.OpenAsync`: sends encrypted packet 1059 on connection open.
- Existing `LoginSeed1059`, `LoginDecodedFrameReader`, and `LoginLegacyStreamCipher`
  define seed format, malformed framing, and the cipher transformation.

## Ownership and behavior

`LoginSignedAccountSession.OpenAsync` is the trust-boundary entry point for the
client. It requires a currently valid `SignedLoginCatalog`, identity-matching
presentation selection, and an endpoint from that verified catalog. It does not
accept a host from Server.dat, the UI label, or a 1055 response.

`LoginAccountSession.OpenAsync` is a lower-level internal transport primitive.
It does not authenticate arbitrary caller-provided endpoints; trusted consumer
code must go through the signed selection gateway.

One session owns one IPv4 TCP connection and one client-side legacy stream cipher.
Open uses a separately bounded connection phase (10 seconds by default) and
a seed phase (5 seconds by default). Both accept caller cancellation. Opening
consumes exactly one server-first 1059 frame and returns its seed together with
the still-open socket. It never writes anything to the socket.

The seed reader owns a 64-byte buffer, receives the decoded four-byte header,
validates frame bounds, and then reads exactly the advertised body length. Each
TCP read advances only the receive cipher position by the bytes actually read.
It cannot read ahead into the next frame. Fragments are accumulated only within
the verified 1059 length; maximum valid seed packet length is 64 bytes.
A malformed frame/type, EOF, connect failure, timeout, or cancellation causes
connection disposal before the error propagates. Dispose is idempotent.

The cipher's client-outbound counter begins at zero and remains untouched. The
inbound counter advances by the exact bytes consumed; it is retained with the
session for the later credential phase. No background tasks, channels, sockets
outside session ownership, automatic retries, or hidden reconnections exist.

## Security and deliberate exclusions

The signed catalog authenticates endpoint *metadata*, not the live TCP peer.
Legacy XOR/nibble obfuscation does not provide confidentiality, integrity, or
server authentication. Even a valid seed is not successful authentication.

The session has **no public/internal method for sending credentials** and cannot
produce or interpret 1055 authentication results. No new UI behavior, fake realm
status, GameServer endpoint acceptance, or background polling is introduced.
Before AUTH-001C transmits a request, the calling application must revalidate
catalog freshness, identity and the session lifecycle; the current seed-only
operation grants no authorization to transmit credentials.

## Verification

Tests cover a real loopback socket, fragmented 1059 delivery, malformed
length/type rejection, premature EOF, timeout, caller cancellation, socket
disposal, argument validation, rejected unsigned/expired/mismatched selection,
and successful routing from an actual ECDSA-signed catalog fixture. The
pre-existing AUTH-001A tests continue to cover native cipher vectors and
16-bit rollover.

Gates: locked restore, formatting, Release build, full test suite, retail content
verification, macOS/Windows/Quality/Dependency CI, final review and user acceptance.
