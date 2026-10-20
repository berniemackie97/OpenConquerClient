# AUTH-001K: Secured GameServer framing (offline)

## Sources and scope

Builds on AUTH-001A through AUTH-001J, which provide the pinned
GameServer DH group, the offline DH response and the negotiated
directional CAST5-CFB64 session cipher. AUTH-001K adds secured packet
framing on top of that cipher. It works on in-memory buffers only.

Reference implementation in the companion server:

- `OpenConquerServer/src/OpenConquer.Protocol/Game/GameWireProtocol.cs`
- `OpenConquerServer/src/OpenConquer.Protocol/Game/Framing/GameSecuredFrameDecoder.cs`
- `OpenConquerServer/src/OpenConquer.Protocol/Game/Framing/GameSecuredFrameEncoder.cs`
- `OpenConquerServer/src/OpenConquer.GameServer/Connections/Framing/GameSecuredFrameReader.cs`
- `OpenConquerServer/src/OpenConquer.GameServer/Connections/Framing/GameSecuredFrameWriter.cs`
- `OpenConquerServer/src/OpenConquer.Protocol/Game/Packets/GameLoginProof.cs`

The 5517 packet and handshake sources are described in the earlier
`AUTH-001F` to `AUTH-001J` notes and in
`20_notes/Network/13_connection_lifecycle.md` and
`20_notes/Network/14_session_derived_legacy_cipher_mask.md`. The
signature, packet length rules, DH variant and framing all come from
those sources.

## Wire format

A secured packet is `[length:u16LE][type:u16LE][payload]`. The unsigned
`length` counts the four-byte header and all payload bytes. It does not
count the eight-byte signature trailer.

- Packet size: 4 to 1024 bytes inclusive.
- Encrypted size on the wire: 12 to 1032 bytes inclusive.
- The client appends ASCII `TQClient` to the packets it sends.
- The server appends ASCII `TQServer` to the packets it sends.
- The packet and its trailer are encrypted together with the negotiated
  directional CAST5-CFB64 session stream. The IV and CFB position carry
  over from one packet to the next and are never reset.
- After the DH exchange, the client's first secured packet is the
  standard 28-byte 1052 proof implemented earlier. The decoder itself
  does not restrict which packet ID arrives first.

These frames are separate from the fixed-key bootstrap handshake
envelopes, which have their own layouts and cipher instances. A reader
never consumes bytes of the next packet while finishing the current one.

## Buffer ownership, state and errors

`GameSecuredFrameEncoder` borrows one `GameSessionCipher`. It copies a
complete, serialized packet into output memory owned by the caller,
appends the client signature, and encrypts the result in place. It
checks the packet framing and the destination size before it changes any
bytes or cipher state. The source packet may overlap the destination;
`Span.CopyTo` handles the overlapping copy. If encryption fails
unexpectedly, the encoder clears the staged wire bytes and stays failed
from then on. The encoder is synchronized and never disposes the
borrowed session.

`GameSecuredFrameDecoder` borrows the inbound direction of the same
session and consumes at most one server frame from an input span. The
`BytesConsumed` result says how many bytes were used. Any remaining
bytes belong to later frames and must be passed in again. The decoder
decrypts at most four header bytes and checks the packet length against
the limit. Then it decrypts only enough bytes to complete that packet
and its eight-byte trailer. Partial headers, packet bodies and trailers
are kept across calls without resetting the CFB state. An invalid length
or a wrong `TQServer` signature stops the decoder, and any later call
throws.

On a successful decode, the decoder detaches its rented array (at most
1032 bytes) and hands it to a `GameSecuredInboundFrame`. The frame
exposes the packet (without the trailer), the packet ID and the payload.
The caller must dispose the frame when it is done. Disposal clears the
whole pooled array and returns it to the pool. The Packet and Payload
views are borrowed and must not be kept after disposal. The decoder can
rent another buffer and continue while the caller still holds the
previous frame. The owner disposes the decoder, the encoder and the
session separately, and each disposal is idempotent.

Each decode in progress uses at most a 1032-byte buffer. Frames never
grow past that, and nothing is allocated from a length read off the wire
before it is checked. There is no retry, background pump, network
connection or attempt to resynchronize after a bad frame.

The transport that eventually owns these codecs is responsible for:

- Running reads and writes one at a time.
- Closing the connection on EOF inside a frame, a timeout, an invalid
  signature, cancellation after partial decryption, or a write whose
  outcome is unknown.
- Disposing every frame.

## Testing

The test vectors were computed independently with the Python
`cryptography` package's CAST5/CFB-64. They use the fixed AUTH-001I
secret, `02 71` followed by 62 zero bytes (the first 16 bytes are the
CAST5 key), inbound IV `0102030405060708` and outbound IV
`15161718191A1B1C`.

The plaintext proof (the 1052 the server accepts) is:

`1C001C0444332211EFCDAB897C00456E001122AABBCC00008D150000`

With `TQClient` appended, the full 36-byte encrypted client wire is:

`96064FAD594BD0011BE0CDCA35D411CEDB3F8900FCA7AE811A0ECFEC2A8ABDCAF384E99E`

With `TQServer` appended and the server-to-client IV, the full 36-byte
encrypted server wire is:

`5F6D0A696917876D91A9D89F37B1411E4845CE4144C1A5E9091FAAA561682915AD9377F4`

Tests pin both vectors. They also cover independently computed
ciphertext for back-to-back frames, input split into one-byte fragments,
rejection of a bad length after only the header, a wrong signature, the
lower and upper packet size limits, partial input, extra trailing bytes,
destination checks that leave state untouched on failure, in-place
encoding, frame ownership, behavior after disposal and terminal cipher
failure. The fixtures are computed vectors; they are not captures of
real network traffic. The existing login-proof parser and session cipher
tests are unchanged.

CI runs locked restore, the format check, a Release build, the full test
suite, retail content verification, and the Linux Quality, macOS,
Windows and Dependency Review jobs.

## Security limits

The 5517 handshake's 512-bit DH and CAST5-CFB64 do not authenticate the
peer and cannot protect message integrity. They are not suitable as
crypto for new applications. A correct `TQServer` trailer only marks the
frame; it is not a cryptographic signature.

The codecs have no connection handling of their own. They do not create
or bind a TCP connection, trust a GameServer redirect from the
AccountServer, send the 1052 login proof, redeem a login ticket or allow
world entry. The UI, the signed catalog and the loopback-only
AccountServer credential policy are unchanged.

Before a live GameServer handshake uses these codecs, its transport must
also:

- Decide which endpoint it trusts.
- Put time limits on the connect, the challenge and the response.
- Use each session once, with a single owner.
- Avoid reading ahead across a change of cipher phase.
- Treat a partial write or EOF as an error.
- Clear sensitive buffers on teardown.
