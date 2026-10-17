# AUTH-001A — AccountServer wire codec foundation

## Dependency and scope audit

Baseline: OpenConquerClient `main` at `6bc525e4116ee77751deea7a2e48f17208de8589` (post STARTUP-001Z merge).
The client already has `LoginSeed1059`, `LoginSeedKey`, `CredentialRc5`, and
`StandardAccountRequest1060`. The modern login UI intentionally does not submit credentials.
OpenConquerServer's current `OpenConquer.Protocol/Login` implementation supports the matching
legacy AccountServer cipher, seed 1059, response 1055, and 1060 request.

**This change is protocol plumbing only.** It does not open any socket, read passwords,
submit credentials, authorize a GameServer address, call the server, or change UI behavior.

## Source authority

- `20_notes/Architecture/03_login_flash_startup_command_contract.md`, **Legacy socket transport cipher contract**
  (`CGameSocketLegacyCipher_InitKeySchedule` at `0x6E2344`, transform at `0x6E23A5`);
  native mode 1 has no appended `TQClient` trailer.
- Same document, **1055 field layout consumed by native process path** (`0x75492C`).
- `20_notes/Network/00_protocol_overview.md`: native four-byte little-endian frame
  header and maximum 1,024-byte packet.
- OpenConquerServer `src/OpenConquer.Protocol/Login/Cryptography/LoginStreamCipher.cs` and
  `tests/OpenConquer.Protocol.Tests/Login/Cryptography/LoginStreamCipherTests.cs`:
  interop vectors for both directions. **Server transform uses the inverse operation**.
- OpenConquerServer `src/OpenConquer.Protocol/Login/Packets/LoginAccountAuthenticationResponsePacket.cs`:
  36-byte response and IPv4-only 16-byte NUL-padded host field.

## Owned contracts

`LoginLegacyStreamCipher` (Networking) is per-account-connection state. Each direction has an
independent 16-bit byte offset, and a frame split across multiple socket reads must carry its
position across reads. Reusing a cipher object for a new socket is forbidden. Outbound client
plaintext is transformed with native `xor -> nibble swap -> 0xAB` ordering; incoming server
ciphertext is transformed in the *same* client direction. Cross-platform interop test vectors:

| Input | Direction | Expected |
| --- | --- | --- |
| `0800230478563412` | Client outbound | `D48487651720B0C3` |
| `C54869128E317C0F` | Client inbound | `0800230478563412` |

`LoginDecodedFrameReader` is a **zero-copy parser for already-decrypted bytes**, not a TCP
stream reader. It requires its caller to enforce the same 1,024-byte maximum while buffering
unframed input. On incomplete data it does not advance the caller's sequence. Invalid lengths
or packet type zero fail closed. Consumers must not retain its slices after their pipe buffers
are advanced. One sequence can contain multiple frames or a complete frame plus a partial tail.

`LoginAccountResponse1055` parses exactly 36 decrypted bytes into native fields. A zero session
token is a **failure**; only the failure code is meaningful in that branch and there is no
GameServer endpoint. A nonzero session token is *not* authorization by itself. The candidate
endpoint must be a canonical IPv4 C-string and a valid TCP port. The caller must establish
session state and independently validate any handoff; these types never initiate connections.

## Security and performance requirements

- Legacy account stream obfuscation has **no cryptographic authentication**. It must never be
  treated as a signature, trust anchor, or substitute for the authenticated realm directory.
- Decode untrusted bytes under a hard framing limit. On malformed headers or cipher-stream
  desynchronization, terminate that connection; do not resynchronize by scanning for headers.
- No per-byte heap allocations during transformation. The two static 256-byte schedules are
  non-secret legacy constants; the per-session stream positions are private.
- Do not log raw frames, credentials, session identifiers, tickets, auth keys, or full endpoints.
- `TryRead` returns immutable value data; it never performs network I/O or spawns tasks.
- Do not reproduce native out-of-bounds reads or permit arbitrary DNS names from a 1055 packet.
- Do not infer live realm status from directory membership or login endpoint liveness.

## Follow-on dependencies — not included in AUTH-001A

1. **AUTH-001B:** bounded async AccountServer session with connection and per-phase timeouts,
   cancellation, independent send/receive stream counters, buffered socket reads and verified
   seed 1059. It must use the currently authenticated signed-catalog endpoint **without**
   trusting the contents of historical `Server.dat`.
2. **AUTH-001C:** native per-account keypad transform and seeded RC5 composition, with
   independent known-answer fixtures and secret zeroization; only then may request 1060 be sent.
3. **AUTH-001D:** response 1055 handling, failure presentation, rate limits, session state,
   post-auth reports and GameServer handoff with strict lifetime and endpoint policies.
4. **REALM-STATUS:** separately authenticated short-lived status authority and a real producer,
   never hard-coded status colors or implicit online claims. This is not part of account login.

## Required acceptance

- `dotnet restore ... --locked-mode`, `dotnet format --verify-no-changes`, Release build,
  full unit test suite, retail content verification, and cross-platform CI all pass.
- Fragmented/multi-frame/invalid framing tests, independent directional counters, rollover,
  interop vectors and 1055 failure-vs-success cases pass.
- No `Socket`, `TcpClient`, credentials, live status, or GPU code is added in this slice.
- Keep AUTH-001A unmerged until runtime and CI checks succeed; there are no login-page visual changes.
