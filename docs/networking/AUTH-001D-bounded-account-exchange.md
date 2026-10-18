# AUTH-001D — Bounded AccountServer authentication exchange

## Verified baseline and evidence

- Client `main` `fea61e735b0c375c591d3e0429c5af973aceb02d`, after merged AUTH-001C PR #117.
- Existing client `LoginAccountSession`, `LoginLegacyStreamCipher`, `LoginAccountResponse1055`,
  `StandardAccountCredentialComposer`, `LoginSignedAccountSession`, and signed selection.
- OpenConquerServer `LoginConnectionSession`, `LoginHandshakeProcessor`,
  `LoginAccountAuthenticationResponsePacket`, `LoginPostAuthenticationReportReader`.
- Native binary evidence: `20_notes/Architecture/03_login_flash_startup_command_contract.md`,
  especially `0x754909`/`0x75492C`, `0x767DA1`, `0x7543CA`; and
  `20_notes/Network/5517-server-selection-and-realm-handoff.md`.

## Scope and ownership

`LoginSignedAccountExchange.AuthenticateAsync` is the only application-facing entry point for
this slice. It checks the current authenticated catalog and selected realm before connection.
The selection's monotonically changing revision also rejects changing away and back while the
seed handshake is in progress. The same trusted realm identity is rechecked **after** receiving
1059 and immediately before the first credential-bearing socket write.

The exchange uses the existing 1059 session (no reconnect) and its independent stream positions.
The client composes exactly one standard 276-byte packet 1060 with the already-audited keypad
and RC5 codecs, encrypts it exactly once, then writes it through `NetworkStream.WriteAsync`.
There is no application-level retry after any partial or uncertain write.

The response phase reads **exactly** the four-byte decoded header and then the remaining
32 bytes of the verified 36-byte type 1055 packet. It neither reads ahead nor attempts
desynchronization recovery. Incomplete frames, EOF, wrong types, invalid size or malformed
success endpoints abort the session. A separate bounded response deadline includes outbound
writing and receiving the response. Caller cancellation is propagated; an internal timeout
produces `TimeoutException`. The stream's lifetime remains owned by `LoginAccountSession`.

A nonzero session token is a native **intermediate AccountServer success**, not completed
world login. `LoginAccountExchangeResult.Success` explicitly retains one account session
for the subsequent **1100 / simple-string 1052** phase. The owner must dispose it; it cannot
be mistaken for a GameServer connection. Failed 1055 responses expose the numeric failure
code and close the socket. Unknown codes are preserved without invented UI messages.
Both the 1055 parser and exchange result redact `ToString()`.

## Trust and deployment limitation

The legacy nibble/XOR cipher and RC5 credential transform are native interoperability
mechanisms, **not authenticated encryption**. The signed catalog authenticates directory
metadata but cannot prove the identity of a live remote TCP listener.

**AUTH-001D deliberately refuses password-bearing sends outside IPv4 loopback.** The
application-facing gateway rejects non-`127.0.0.1` catalog ingress before connecting;
the lower-level session independently checks the actual connected peer address.
This is an explicit production security gate, not an assumption that signed metadata
protects an unauthenticated transport. There is no UI credential submission path.

Enabling remote login requires a separately reviewed, authenticated peer/transport
deployment design that preserves the legacy 5517 application protocol. Do not remove
this gate merely to make internet login appear functional.

The caller owns the original credential buffers. An exactly sized 276-byte temporary
request is overwritten and zeroed in a `finally`; RC5's intermediate plaintext, key
and ciphertext are separately zeroed by the existing composer. No password is copied
to a string, persisted, traced, or included in exception text. This does not erase bytes
already delivered to the OS TCP stack or a loopback peer.

## Fail-closed state contract

- Open: one seed received, no authentication attempt used.
- Composing: one atomic attempt is claimed before touching credentials.
- Sending/receiving: no second attempt, reconnect or frame resynchronization.
- Failure/timeout/cancellation/disposal: socket aborted, packet buffer zeroed.
- Valid failure response: numeric code only; socket closed.
- Valid success response: account socket retained under disposable result ownership.
- No continuation in this slice: later native 1100/1052 reporting remains a separate audit.

Concurrent attempts never share a stream cipher position. Socket disposal interrupts a
blocked read or write. Any terminal I/O or validation error closes the session.

## Verification targets

Loopback tests cover real TCP fragmentation, independent server-side cipher inversion,
native seed, 276-byte 1060 composition and RC5 fixture, fixed 1055 success/failure
vectors from the native stream schedule, EOF, bad header, deadline and cancellation,
single-use semantics, disposal and redacted results. The signed-gateway tests use a
real P-256 signed catalog and validate rejection of remote endpoints, stale catalogs,
and select-away-then-back during 1059.

The complete repository gate remains locked restore, formatting, Release build,
Release tests, retail content-set verification, macOS/Windows/Quality and dependency
CI, reviewer audit, and explicit human merge acceptance. The network trust limitation
is deliberate and must remain documented before the later live-integration slice.
