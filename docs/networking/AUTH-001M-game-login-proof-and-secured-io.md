# AUTH-001M — Loopback GameServer proof 1052 and secured inbound I/O

## Audited baseline

OpenConquerClient `main` at `73a78759649e6d02209697301c1dbd75d4d21874`,
after merged AUTH-001L (#130), already supports the native AccountServer
1059/1060/1055 and 1100/resource-1052 exchange, a loopback-only GameServer
TCP connection, exact DH challenge/response, the negotiated directional
CAST5-CFB64 cipher, and the offline secured frame encoder/decoder.

Protocol authority:
- Server `GameConnectionSession`, `GameTransportHandshakeProcessor`,
  `GameSecuredFrameReader`, `GameSecuredFrameWriter`.
- Server `GameLoginConnectionProcessor`, `GameConnectionAuthenticator`,
  `GameLoginProof` and `GameLoginProofTests`.
- Native 5517 network evidence in `20_notes/Network/13_connection_lifecycle.md`,
  `14_session_derived_legacy_cipher_mask.md` and AUTH-001F–001L.

The GameServer receives its standard **28-byte 1052 login proof** only
*after* the raw DH handshake. It is a secured packet:
`[u16 length = 28][u16 type = 1052][24-byte payload][TQClient (8)]`
and the entire 36-byte wire frame is encrypted using the negotiated
client-outbound CAST5-CFB64 stream, **not** the bootstrap cipher.
Its session UID/key comes only from the exact originating AccountServer
1055 success; its resource version is taken from the previously completed
AccountServer resource report. This is not the AccountServer's 1052 report.

## Lifetime and exact one-use proof

`GameServerHandshakeSession` owns the socket and negotiated cipher from
AUTH-001L. AUTH-001M adds an encoder and decoder borrowing that cipher.
The session retains the **exact AccountServer result instance** that
authorized its GameServer handshake, preventing substitution of another
account's ticket on the connected socket. The original result must remain
undisposed until proof composition. The existing
`LoginAccountExchangeResult.WriteGameLoginProof` still validates native
field sizes and permits exactly one successful composition.

Proof transmission is a single terminally guarded state transition:
handshake-complete -> proof-writing -> secured-reading.
An invalid, failed, canceled, partial or uncertain write aborts TCP and
terminates the session. The client never retries the same proof or
automatically reconnects, and a write completion does **not** establish
GameServer ticket redemption, authentication, or admission.
Only one proof may be sent in the entire session lifetime.

The proof packet and encrypted wire buffers are separately and
explicitly zeroed on all paths. The frame encoder does not own the
negotiated cipher. The session's disposal closes the socket, cancels I/O
and awaits active operations before disposing frame codecs, cipher and
cancellation source. No callbacks run while the operation lifetime
lock is held. A disposed/terminal session cannot restart operations.

## Incremental bounded secured reads

After proof transmission, the session reads *one* server-to-client frame
at a time using the AUTH-001K decoder. It first reads at most the four
encrypted header bytes; the validated 4–1024 packet-length then determines
the **exact** remaining bytes, including the eight-byte `TQServer`
trailer. The decoder exposes the number of bytes needed for its next
step, enabling at most 1032 bytes per frame without byte-at-a-time socket
reads or speculative consumption from following frames.

CFB64 feedback is continuous across partial TCP reads and consecutive
frames, with no IV reset and no cipher state loss. The returned frame
owns a pooled decrypted packet buffer and must be disposed; the next
decode is independent of the caller's ownership. No packet dispatch,
automatic authentication inference, or world-admission transition
is performed. It is legal for a server to send different packet IDs:
the decoder validates framing, not gameplay semantics.

Malformed length, invalid trailer, truncated EOF, timeout or cancellation
after partial receipt is terminal. Clean EOF at a frame boundary
returns `null` and closes the session. Concurrent reads, reads before
proof, attempts to resend proof, and calls after disposal fail closed.

Connect and handshake deadlines remain those in AUTH-001L. New proof
and per-frame deadlines must be positive and no more than 30 seconds,
and cover the *entire phase* rather than resetting on each fragment.
There is no unbounded queue, background read pump, retry loop, or
per-byte socket syscall in the normal path.

## Regression and interoperability tests

The loopback tests perform the real client AccountServer exchange,
then use the independent AUTH-001H fixed encrypted GameServer challenge.
The mock server parses the native encrypted DH response, computes its
own matching DH secret and initializes independently directed session
cipher streams. It verifies the exact decrypted client 1052 proof
and `TQClient` trailer, then emits **two secured frames in one write**.
Client reads must consume precisely one frame each without eating the
next frame's ciphertext or resetting CFB state.

Further tests cover proof-before-read ordering, one-time proof use,
invalid signature, partial-frame EOF, deadline termination, socket
closure, disposal of the originating AccountServer result, and
post-failure terminal behavior. Previous handshake, crypto and
framing regressions remain enforced. These are source-derived
loopback test fixtures, not captures of an authenticated remote peer.

## Trust boundary and exclusions

The only admitted GameServer endpoint remains **literal IPv4
127.0.0.1**, derived from a successful AccountServer result after
native 1100/1052 reports. The unauthenticated legacy 1055 redirect
is not proof of remote peer identity. Native 512-bit DH and CAST5
CFB-64 are neither contemporary authenticated key agreement nor
authenticated encryption. A valid `TQServer` suffix is a legacy
framing marker, not a cryptographic signature.

This slice does **not** authorize world entry, process a ticket
acceptance/refusal, send gameplay packets, accept remote endpoints,
enable the UI login button, or change server-side admission logic.
Those require separate reviewed state transitions.

Acceptance: locked restore, formatting, Release build/tests,
retail content verification, Linux Quality/macOS/Windows,
Dependency Review, review findings and explicit user merge approval.
