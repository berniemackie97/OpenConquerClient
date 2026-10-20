# AUTH-001M: Loopback GameServer 1052 proof and secured inbound I/O

## Sources

Builds on AUTH-001L. The client already has the 5517 AccountServer
exchange (1059/1060/1055, then the 1100 and resource 1052 reports), a
loopback-only GameServer TCP connection, the DH challenge and response,
the negotiated directional CAST5-CFB64 cipher, and the offline secured
frame encoder and decoder.

Protocol sources:
- Server `GameConnectionSession`, `GameTransportHandshakeProcessor`,
  `GameSecuredFrameReader`, `GameSecuredFrameWriter`.
- Server `GameLoginConnectionProcessor`, `GameConnectionAuthenticator`,
  `GameLoginProof` and `GameLoginProofTests`.
- 5517 network notes in `20_notes/Network/13_connection_lifecycle.md`,
  `14_session_derived_legacy_cipher_mask.md` and AUTH-001F to AUTH-001L.

## The 1052 login proof

The GameServer gets the standard 28-byte 1052 login proof after the raw
DH handshake. It is a secured packet,
`[u16 length = 28][u16 type = 1052][24-byte payload][TQClient (8)]`. The
whole 36-byte wire frame is encrypted with the negotiated
client-outbound CAST5-CFB64 stream, not the bootstrap cipher. The
session UID and key come only from the successful AccountServer 1055
that started this connection. The resource version comes from the
AccountServer resource report sent earlier. This GameServer 1052 is a
different packet from the AccountServer's 1052 report.

## Sending the proof once

`GameServerHandshakeSession` owns the socket and negotiated cipher from
AUTH-001L. AUTH-001M adds an encoder and a decoder that borrow that
cipher. The session keeps the same AccountServer result instance that
allowed its GameServer handshake, so another account's ticket cannot be
swapped in on the connected socket. That result must not be disposed
before the proof is built.
`LoginAccountExchangeResult.WriteGameLoginProof` still checks the 5517
field sizes and can build the proof successfully only once.

Sending the proof is a single state transition, from handshake-complete
to proof-writing to secured-reading, and a failure along the way is
final. If the write is invalid, fails, is canceled, is partial or has an
unknown outcome, the client aborts the TCP connection and the session
ends. The client never resends the proof and never reconnects on its
own. A completed write does not mean the GameServer redeemed the ticket,
authenticated the client or admitted it. A session sends at most one
proof in its lifetime.

The proof packet buffer and the encrypted wire buffer are each zeroed on
every path. The frame encoder does not own the negotiated cipher.
Disposing the session closes the socket, cancels I/O and waits for
active operations, then disposes the frame codecs, the cipher and the
cancellation source. No callbacks run while the operation lifetime lock
is held. A disposed or failed session cannot start new operations.

## Reading secured frames

After the proof is sent, the session reads one server-to-client frame at
a time with the AUTH-001K decoder. It first reads up to the four
encrypted header bytes. Once the packet length is checked against the 4
to 1024 range, it gives the number of bytes still to read, including the
eight-byte `TQServer` trailer. The decoder reports how many bytes its
next step needs, so a frame takes at most 1032 bytes. The session never
reads the socket one byte at a time and never consumes bytes from the
following frame.

The CFB64 feedback carries across partial TCP reads and consecutive
frames. The IV is never reset and no cipher state is lost. Each returned
frame owns a pooled buffer with the decrypted packet and must be
disposed. The next decode does not depend on whether the caller still
holds the previous frame. The session does not dispatch packets, does
not infer authentication from any packet, and does not move on to world
entry. The server may send any packet ID: the decoder checks the
framing, not gameplay meaning.

A malformed length, an invalid trailer, EOF inside a frame, a timeout,
or cancellation after part of a frame arrived ends the session. A clean
EOF at a frame boundary returns `null` and closes the session.
Concurrent reads, reads before the proof is sent, attempts to send the
proof again and calls after disposal are rejected.

The connect and handshake deadlines are the ones from AUTH-001L. The new
proof deadline and per-frame deadline must each be positive and at most
30 seconds. Each covers its whole phase and does not restart when a
fragment arrives. The normal path has no unbounded queue, background
read pump, retry loop or per-byte socket call.

## Testing

The loopback tests run the real client AccountServer exchange, then send
the fixed encrypted GameServer challenge from AUTH-001H, which was
generated independently. The mock server parses the encrypted 5517 DH
response, computes the matching DH secret on its own and sets up its own
session cipher stream for each direction. It checks the decrypted client
1052 proof and the `TQClient` trailer byte for byte, then sends two
secured frames in a single write. Each client read must consume one
frame without touching the next frame's ciphertext or resetting the CFB
state.

Other tests cover the rule that the proof is sent before any read, using
the proof once, an invalid signature, EOF inside a frame, ending the
session on a deadline, socket closure, disposing the AccountServer
result that started the session, and staying failed after an error. The
earlier handshake, crypto and framing tests still run. The fixtures are
derived from source and run over loopback; they are not captures from an
authenticated remote server.

CI runs locked restore, the format check, Release build and tests,
retail content verification, the Linux Quality, macOS and Windows jobs,
and Dependency Review.

## Security limits

The only GameServer endpoint allowed is still the literal IPv4 address
127.0.0.1, taken from a successful AccountServer result after the
1100/1052 reports. The 1055 redirect is not authenticated and does not
prove the identity of the remote peer. The 5517 512-bit DH and CAST5
CFB-64 are neither modern authenticated key agreement nor authenticated
encryption. A valid `TQServer` suffix is a legacy framing marker, not a
cryptographic signature.

AUTH-001M does not allow world entry, handle the server accepting or
refusing the ticket, send gameplay packets, accept remote endpoints,
enable the UI login button or change server-side admission. Each of
those needs its own reviewed state transition.
