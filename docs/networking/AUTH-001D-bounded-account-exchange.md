# AUTH-001D: AccountServer authentication exchange

AUTH-001D sends the credential request 1060 over the seed session and reads the 1055 response.
Sends that carry a password are limited to IPv4 loopback.

## Sources

- The existing client types `LoginAccountSession`, `LoginLegacyStreamCipher`,
  `LoginAccountResponse1055`, `StandardAccountCredentialComposer` and
  `LoginSignedAccountSession`, and the signed selection.
- OpenConquerServer `LoginConnectionSession`, `LoginHandshakeProcessor`,
  `LoginAccountAuthenticationResponsePacket`, `LoginPostAuthenticationReportReader`.
- Research on the retail binary:
  `20_notes/Architecture/03_login_flash_startup_command_contract.md`, especially
  `0x754909`/`0x75492C`, `0x767DA1` and `0x7543CA`, and
  `20_notes/Network/5517-server-selection-and-realm-handoff.md`.

## Design

`LoginSignedAccountExchange.AuthenticateAsync` is the only entry point for application code.
Before connecting, it checks the current authenticated catalog and the selected realm. The
selection carries a monotonic revision number, so switching to another realm and back while the
seed handshake runs is also rejected. The same trusted realm identity is checked again after
1059 arrives, right before the first socket write that carries credentials.

The exchange reuses the 1059 session, without reconnecting, together with its independent
stream positions. The client composes one standard 276-byte packet 1060 with the existing
keypad and RC5 codecs, encrypts it once and writes it with `NetworkStream.WriteAsync`. There is
no application-level retry after a partial or uncertain write.

The response phase reads the four-byte decoded header, then the remaining 32 bytes of the
36-byte type 1055 packet, and nothing more. It never reads ahead and never tries to recover
from desynchronization. Incomplete frames, EOF, a wrong type, an invalid size or a malformed
success endpoint abort the session. A separate response deadline (5 seconds by default, at most
30 seconds) covers both writing the request and receiving the response. Cancellation by the
caller surfaces as cancellation, while an internal timeout raises `TimeoutException`. The
stream remains owned by `LoginAccountSession`.

In the retail flow, a nonzero session token is an intermediate AccountServer success. It does
not mean the player is logged in to the world. `LoginAccountExchangeResult.Success` keeps one
account session open for the following 1100 / simple-string 1052 phase. The owner must dispose
it, and it cannot be mistaken for a GameServer connection. A failed 1055 response exposes the
numeric failure code and closes the socket. Unknown codes are passed through as numbers, with
no invented UI messages. Both the 1055 parser and the exchange result redact `ToString()`.

## Trust and deployment limits

The legacy nibble/XOR cipher and the RC5 credential transform exist for interoperability with
the 5517 client and are not authenticated encryption. The signed catalog authenticates the
directory metadata but cannot prove who is listening on a live remote TCP port.

**Password-bearing sends are refused for any address other than IPv4 loopback.** The
application-facing gateway rejects catalog endpoints other than `127.0.0.1` before connecting,
and the lower-level session also checks the address of the peer it actually connected to. This
is a production security gate: it does not rely on signed metadata to protect an
unauthenticated transport. No UI path submits credentials.

Remote login will need a separately reviewed deployment design that authenticates the peer or
the transport while keeping the legacy 5517 application protocol. Do not remove this gate just
to make internet login appear to work.

The caller owns the original credential buffers. The temporary 276-byte request is overwritten
and zeroed in a `finally`, and the existing composer zeroes the RC5 intermediate plaintext, key
and ciphertext on its own. No password is copied into a string, persisted, traced or included
in exception text. Bytes already handed to the OS TCP stack or delivered to a loopback peer
cannot be erased.

## Session states

- Open: one seed received, no authentication attempt used yet.
- Composing: the single attempt is claimed atomically before any credential is touched.
- Sending and receiving: no second attempt, no reconnect and no frame resynchronization.
- Failure, timeout, cancellation or disposal: the socket is aborted and the packet buffer is
  zeroed.
- Valid failure response: only the numeric code is returned, and the socket is closed.
- Valid success response: the account socket stays open, owned by the disposable result.
- Nothing continues automatically. The 1100/1052 reporting that the retail client sends next is
  covered separately in AUTH-001E.

Concurrent attempts never share a stream cipher position. Disposing the socket interrupts a
blocked read or write. Any terminal I/O or validation error closes the session.

## Testing

Loopback tests cover real TCP fragmentation, decryption by an independent server-side cipher,
the 5517 seed, 276-byte 1060 composition and the RC5 fixture, fixed 1055 success and failure
vectors built with the 5517 stream schedule, EOF, a bad header, the deadline and cancellation,
single use, disposal and redacted results. The signed-gateway tests use a real P-256 signed
catalog and check that remote endpoints, stale catalogs, and selecting away and back during
1059 are all rejected.

The change passes the repository gate: locked restore, formatting, Release build, Release
tests, retail content-set verification, and the macOS, Windows, Quality and dependency CI jobs.
The network trust limit described above is intentional and must stay documented until live
integration is in place.
