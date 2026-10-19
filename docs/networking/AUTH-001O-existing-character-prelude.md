# AUTH-001O — Existing-character GameServer account-history and state prelude

## Audited base and source provenance

Client main: `ecca07f601eb3e701d07a7a3b3548414652f7560` (AUTH-001N merged, PR #132).
Companion server main: `bda9f0e76108872b648a9cfa5bb245c82589023e`.

Verified native source: `20_notes/Network/10_extended_packets.md` (2078/2079
semantic closure, 2026-09-15); `18_post_auth_server_comparison.md`;
`01_core_packets.md`; `13_connection_lifecycle.md`. Native functions:
`CMsgAccountLoginAlert2078__ValidateAndCopy` 0x75B006, `Process` 0x75B029,
`CHero_SetAccountLoginAlertData` 0x6577BF, `CMsgServerState2079__ValidateAndCopy`
0x75AF53, and `CMsgServerState__Process` 0x75AF76.

Companion-server sources: `ExistingCharacterBootstrapProcessor`,
`ExistingCharacterBootstrapPacketFactory`, `GameLoginHistoryPacket` and
`GameServerStatePacket`. The **current server** sends
`1004 ANSWER_OK → 2078 → 2079 → 1006`. These values/order are verified
server implementation, **not a captured retail sequence**.

## Wire and semantic contract

Both packets are secured using the existing GameServer directional cipher and
eight-byte server signature; packet-length header excludes that signature.
The existing session decoder caps plaintext packets at 1024 bytes and remains
the sole owner of TCP, frame buffers and cipher state.

- **2078**, minimum 10 bytes: `[u16 length][u16 type=2078]
  [u32 lastLoginTimestamp][u8 locationWarningFlag][ANSI location][NUL]`.
  The timestamp and byte are retained without server-policy guesswork. The
  first NUL terminates the location; any remaining bounded data is ignored,
  consistent with the native parser and padded reference packets. Undefined
  Windows-1252 byte values in the actual location are rejected; 0x80 and
  other assigned extended characters decode normally. A missing terminator,
  corrupted header/type or oversized frame terminates typed bootstrap.
  This is account-login history, **not a title or synthetic crypto seed**.
- **2079**, minimum 8 bytes: `[u16 length][u16 type=2079][u32 state]`.
  The native consumer reads only the DWORD at +4, ignoring bounded trailing
  bytes. State 0 selects UI global 2000, state 1 selects 2001, and **all other
  values preserve the previous native UI global**. The reader retains the
  raw value and returns `null` for unknown selections; it never substitutes
  2000 or turns an unknown state into a login rejection.

## Exclusive ownership and terminal behavior

`GameServerHandshakeSession.ReadExistingCharacterPreludeAsync` is a
**one-shot, current-companion-server-specific convenience operation** allowed
only after successful `ReadLoginControlAsync` returned ExistingCharacter,
with no intervening `ReadFrameAsync`. It claims the two-frame window under
the same lifetime lock. Another caller cannot interleave a raw read between
2078 and 2079, relabel an already consumed frame or repeat the prelude.
The existing secured reader preserves continuous CAST5 state. All decrypted
frame owners are disposed before returning immutable owned results; no
borrowed spans, stream, or socket reach the UI. The supplied timeout is a
**single total deadline** for both frames, not a renewed per-packet budget.
EOF, wrong type/order, invalid text/framing, timeout and cancellation are
terminal and close the socket. A refused/creation login control cannot
start this operation. Existing `ReadFrameAsync` remains usable afterward;
the next 1006 is deliberately not parsed or interpreted in this slice.

**Important native boundary:** native 5517 does **not** wait for 2078 and
2079 before its 1006 handler builds and sends action 74 (EnterMap). This
specific prelude operation describes the existing companion server's
documented sequence; it is not a universal protocol-level readiness gate.
No timestamp, warning flag, location or state is treated as evidence of
peer identity, world admission, hero hydration or gameplay permission.

## Deliberate exclusions

No remote host/DNS, no UI wiring or account-alert dialog, no server edits,
no character creation, no 1006 hero parser, no EnterMap (74) send,
no world-session or gameplay dispatch, and no claimed authenticated
transport. Legacy DH+CAST5 with the legacy signature remains unauthenticated;
the loopback-only boundary remains intact.

## Verification

Focused tests assert compact and padded 2078, ANSI extension/invalid-byte
behavior, 2079 state 0/1/unknown semantics and optional trailing bytes,
malformed type/length/NUL, total frame bounds, real loopback DH/proof,
encrypted 1004→2078→2079 with a subsequent retained cipher frame,
one-shot ownership, raw-read bypass, out-of-order rejection and timeout.
Synthetic fixtures are source-derived, not native traffic captures.
Required gates: locked restore, format, Release build and full tests,
retail content audit, Linux/macOS/Windows CI, Dependency Review, CodeQL,
review comments, final re-audit. Only the user merges.
