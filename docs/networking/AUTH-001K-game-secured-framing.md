# AUTH-001K — Native secured GameServer framing (offline)

## Evidence, baseline, and scope

Client base `main` `a121ac8bb5f70659cb32aaf18a46e78b2ef586bc`
includes AUTH-001A through AUTH-001J, including the pinned GameServer
DH group, offline DH response, and negotiated directional CAST5-CFB64
session cipher. This slice is **offline secured framing** only.

Authoritative companion-server implementation:

- `OpenConquerServer/src/OpenConquer.Protocol/Game/GameWireProtocol.cs`
- `OpenConquerServer/src/OpenConquer.Protocol/Game/Framing/GameSecuredFrameDecoder.cs`
- `OpenConquerServer/src/OpenConquer.Protocol/Game/Framing/GameSecuredFrameEncoder.cs`
- `OpenConquerServer/src/OpenConquer.GameServer/Connections/Framing/GameSecuredFrameReader.cs`
- `OpenConquerServer/src/OpenConquer.GameServer/Connections/Framing/GameSecuredFrameWriter.cs`
- `OpenConquerServer/src/OpenConquer.Protocol/Game/Packets/GameLoginProof.cs`

Native packet/handshake evidence is documented in the preceding
`AUTH-001F`–`AUTH-001J` records and in
`20_notes/Network/13_connection_lifecycle.md` and
`20_notes/Network/14_session_derived_legacy_cipher_mask.md`.
This work does not invent a new signature, packet length, DH variant,
or framing transport.

## Exact wire contract

**Packet**: `[length:u16LE][type:u16LE][payload]`. The unsigned
`length` includes the four-byte header and all payload bytes but
**excludes** the eight-byte signature trailer.

- Packet size: **4–1024 bytes inclusive**.
- Total encrypted wire size: **12–1032 bytes inclusive**.
- Client outbound: append ASCII `TQClient`.
- Server outbound: append ASCII `TQServer`.
- Encrypt the whole packet **and** trailer with the already-negotiated
  directional CAST5-CFB64 session stream, without resetting its IV
  or CFB position between packets.
- The first secured packet from the client after the DH exchange is
  the previously implemented standard 28-byte proof **1052**; packet
  decoding itself intentionally does not pre-authorize a packet ID.

These are *not* the fixed-key bootstrap handshake envelopes; those
have their own layouts and cipher instances. No reader may consume bytes
from the next packet when finishing the current one.

## Buffer ownership, state, and failure behavior

`GameSecuredFrameEncoder` borrows one `GameSessionCipher` and writes
a complete, already-serialized packet into caller-owned output memory,
appends the client signature, then encrypts in place. Packet framing
and destination size are fully validated before any bytes or cipher
state are modified. Source packet spans may overlap the destination;
`Span.CopyTo` handles the overlapping copy. Unexpected encryption
failures clear the staged wire bytes and terminally fault the encoder.
The encoder is synchronized and never disposes the borrowed session.

`GameSecuredFrameDecoder` borrows the same session's inbound direction
and consumes **at most one server frame** from an input span.
The `BytesConsumed` result precisely identifies the bytes consumed;
any remainder belongs to subsequent frames and must be fed again.
It decrypts at most four header bytes before validating the bounded
packet length, then only enough bytes to complete that packet and
its eight-byte trailer. Partial headers, packet bodies, and trailers
are retained across calls without resetting CFB state.
Invalid length or wrong `TQServer` signature terminates the decoder;
reusing it after failure throws.

On successful decode, the reader detaches its bounded rented array
and transfers ownership to `GameSecuredInboundFrame`. The frame
exposes the exact packet (without trailer), packet ID and payload.
The caller must **dispose the frame** when finished; disposal returns
the pooled array and clears its entirety. Packet/Payload views are
borrowed: retaining them after disposal is forbidden. The decoder
can continue with another rented buffer even while the previous
result is owned by the caller. Disposal of decoder, encoder, and
session is independently controlled by the owner; each is idempotent.

The buffer ceiling is fixed at 1032 bytes per in-progress decode.
No unbounded frame growth, unauthenticated packet allocation from a
wire-declared size, retry, background pump, network connection,
or speculative frame resynchronization is introduced. The eventual
transport owner must enforce sequential reads/writes, terminate
the connection on incomplete EOF, timeout, invalid signatures,
cancellation after partial decryption, or uncertain output writes,
and correctly dispose every frame.

## Independent wire fixtures and regressions

Independent Python `cryptography` CAST5/CFB-64 fixtures use the
deterministic AUTH-001I secret `02 71` followed by 62 zero bytes
(first 16 bytes are the actual CAST5 key), inbound IV
`0102030405060708` and outbound IV `15161718191A1B1C`.

The plaintext proof (server-compatible 1052) is:

`1C001C0444332211EFCDAB897C00456E001122AABBCC00008D150000`

With appended `TQClient`, the full 36-byte encrypted client wire is:

`96064FAD594BD0011BE0CDCA35D411CEDB3F8900FCA7AE811A0ECFEC2A8ABDCAF384E99E`

With appended `TQServer` and the server-to-client IV,
the full 36-byte encrypted server wire is:

`5F6D0A696917876D91A9D89F37B1411E4845CE4144C1A5E9091FAAA561682915AD9377F4`

Tests pin both vectors and independent back-to-back-frame ciphertext,
one-byte fragmentation, header-only rejection, wrong signature,
exact lower/upper packet bounds, partial input, extra bytes,
destination validation atomicity, in-place encoding, frame ownership,
post-disposal behavior, and terminal cipher failure behavior.
Fixtures are independent **computed vectors**, not claimed captures
of real network traffic. The existing login-proof parser and session
cipher tests remain unmodified.

## Trust boundary and subsequent work

Native 512-bit DH and CAST5-CFB64 do not authenticate the peer,
cannot ensure message integrity, and are unsuitable as new secure
application crypto. A correct `TQServer` trailer is a framing marker,
**not a cryptographic signature**.

This slice does not create or bind a TCP connection, trust an
AccountServer-supplied GameServer redirect, send the 1052 login proof,
redeem a login ticket, alter UI, or authorize world admission.
The existing signed catalog and loopback-only AccountServer credential
policy remain unchanged.

Before a live GameServer handshake can use these codecs, its transport
must separately establish endpoint authority, bound connect/challenge
and response deadlines, enforce one-use session ownership, prevent
read-ahead across crypto phase transitions, handle partial writes and
EOF fail-closed, and explicitly clear sensitive buffers on teardown.

## Acceptance

Locked restore, formatting, Release build, full tests, retail content
verification, Linux Quality, macOS, Windows, Dependency Review,
independent review resolution, and explicit user merge acceptance.
