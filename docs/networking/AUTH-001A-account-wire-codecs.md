# AUTH-001A: AccountServer wire codecs

## Scope

AUTH-001A adds the AccountServer stream cipher, a frame parser for decrypted bytes and the 1055
response parser. It builds on the existing `LoginSeed1059`, `LoginSeedKey`, `CredentialRc5` and
`StandardAccountRequest1060` types. The login UI does not submit credentials. On the server side,
OpenConquerServer's `OpenConquer.Protocol/Login` implements the matching legacy AccountServer
cipher, seed 1059, response 1055 and request 1060.

These types are protocol codecs only. They do no network I/O and do not read passwords. They do not
submit credentials or accept a GameServer address, and the login page looks and behaves the same as
before.

## Sources

- `20_notes/Architecture/03_login_flash_startup_command_contract.md`, section "Legacy socket
  transport cipher contract" (`CGameSocketLegacyCipher_InitKeySchedule` at `0x6E2344`, transform at
  `0x6E23A5`). In the retail client, mode 1 appends no `TQClient` trailer.
- Same document, section "1055 field layout consumed by native process path" (`0x75492C`).
- `20_notes/Network/00_protocol_overview.md`: the 5517 four-byte little-endian frame header and the
  maximum packet size of 1,024 bytes.
- OpenConquerServer `src/OpenConquer.Protocol/Login/Cryptography/LoginStreamCipher.cs` and
  `tests/OpenConquer.Protocol.Tests/Login/Cryptography/LoginStreamCipherTests.cs`: interop vectors
  for both directions. The server's transform is the inverse operation.
- OpenConquerServer
  `src/OpenConquer.Protocol/Login/Packets/LoginAccountAuthenticationResponsePacket.cs`: the 36-byte
  response and its IPv4-only, 16-byte NUL-padded host field.

## Types

`LoginLegacyStreamCipher` (Networking) holds the cipher state of one account connection. Each
direction has its own 16-bit byte offset. When a frame arrives split across several socket reads,
the position carries over from one read to the next. A cipher object must never be reused for a new
socket. Outbound client plaintext is transformed in the retail order `xor -> nibble swap -> 0xAB`.
Incoming server ciphertext goes through the same client-side transform, because the server applies
the inverse. Cross-platform interop test vectors:

| Input | Direction | Expected |
| --- | --- | --- |
| `0800230478563412` | Client outbound | `D48487651720B0C3` |
| `C54869128E317C0F` | Client inbound | `0800230478563412` |

`LoginDecodedFrameReader` parses frames out of bytes that are already decrypted, without copying
them. It does not read from a TCP stream. Its caller must apply the same 1,024-byte maximum while
buffering input that has not been framed yet. On incomplete data the reader leaves the caller's
sequence where it was. Invalid lengths and packet type zero are rejected as errors. Callers must not
keep its slices after advancing their pipe buffers. One sequence can hold several frames, or a
complete frame followed by the start of the next one.

`LoginAccountResponse1055` parses the 36 decrypted bytes into the retail fields. A zero session
token means the login failed: only the failure code is meaningful in that case, and there is no
GameServer endpoint. A nonzero session token does not authorize anything by itself. The candidate
endpoint must be a dotted-decimal IPv4 C-string in normalized form, with a valid TCP port. The
caller is responsible for session state and for validating any handoff separately. These types never
open connections.

## Security and performance

- The legacy account stream obfuscation has no cryptographic authentication. It must never be
  treated as a signature, a trust anchor or a substitute for the authenticated realm directory.
- Untrusted bytes are decoded under a hard framing limit. A malformed header or a desynchronized
  cipher stream ends that connection. The stream is never resynchronized by scanning for the next
  header.
- The transform allocates nothing on the heap per byte. The two static 256-byte schedules are public
  legacy constants. The per-session stream positions are private.
- Raw frames, credentials, session identifiers, tickets, auth keys and full endpoints are never
  logged.
- `LoginAccountResponse1055.TryRead` returns parsed values that include sensitive fields. Its
  `ToString()` prints only a safe status summary. Callers must not serialize or log its fields, and
  must not log raw response frames.
- `LoginDecodedFrameReader.TryRead` returns a `ReadOnlySequence<byte>` slice that borrows the
  caller's memory. The slice is valid only until the caller advances or releases the pipe buffer, or
  otherwise reuses or modifies the backing memory. Copy the bytes if you need them longer.
- `LoginDecodedFrameReader.TryRead` returns `false` only for incomplete input. Malformed lengths and
  zero packet types throw `InvalidDataException` without consuming any input. The session owner must
  then close the connection and must not try to resynchronize the stream.
- Neither parser does network I/O or starts tasks.
- The out-of-bounds reads of the retail client are not reproduced, and a 1055 packet cannot supply
  an arbitrary DNS name.
- Live realm status is never inferred from directory membership or from whether a login endpoint
  responds.

## Follow-on work

AUTH-001A covers only the codecs. The later steps are:

1. AUTH-001B: an async AccountServer session with a connect timeout and per-phase timeouts,
   cancellation, separate send and receive stream counters, buffered socket reads and a validated
   seed 1059. It must use the endpoint from the currently authenticated signed catalog and must not
   trust the contents of the historical `Server.dat`.
2. AUTH-001C: the 5517 per-account keypad transform and seeded RC5 composition, with independent
   known-answer fixtures and zeroing of secrets. Request 1060 may be sent only after this is in
   place.
3. AUTH-001D: response 1055 handling, failure presentation, rate limits, session state,
   post-authentication reports and the GameServer handoff, with strict lifetime and endpoint rules.
4. REALM-STATUS: a separately authenticated, short-lived source of realm status with a real
   producer. Status colors are never hard-coded and no realm is shown as online by assumption. This
   is not part of account login.

## Testing

- Tests cover fragmented, multi-frame and invalid framing, independent counters per direction,
  counter rollover, the interop vectors, and both the failure and success forms of 1055.
- The full gate passes: `dotnet restore ... --locked-mode`, `dotnet format --verify-no-changes`,
  Release build, the full unit test suite, retail content verification and cross-platform CI.
- AUTH-001A adds no `Socket` or `TcpClient` code, no credential handling, no live status and no GPU
  code.
