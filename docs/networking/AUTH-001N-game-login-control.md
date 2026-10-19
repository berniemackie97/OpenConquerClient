# AUTH-001N — First post-proof GameServer login control

## Verified base and scope

Client base: `49887bb052823d4422b1f5db733badd67218ffb0` (merged AUTH-001M, PR #131).
Companion server: `111c641c08305917e1144a940453ca0c5a6eafea`.

The native Conquer 5517 `CMsgTalk__Process` dispatches type 1004 on login channel
`0x835` to `CMyShellDlg_HandleLoginStatusSystemTalk`. Verified native evidence:
`20_notes/Network/01_core_packets.md` (world-entry controls and string lists),
`20_notes/Network/13_connection_lifecycle.md`, and
`20_notes/Architecture/03_login_flash_startup_command_contract.md`.
The current server's `GameConnectionAuthenticator` redeems the first secured
1052 proof without issuing a separate "ticket accepted" acknowledgement.
`GameLoginCapacityGate` / `ExistingCharacterBootstrapProcessor` can refuse
with a 1004 login-channel text before closing. Successful existing-character
bootstrap begins with `1004 ANSWER_OK`, followed by `2078`, `2079`,
`1006`. A character-creation route corresponds to `1004 NEW_ROLE`, but
the current GameServer intentionally does **not** implement that route.

## Bounded control contract

The first GameServer packet after transmitting proof 1052 must be a secured
1004 on channel `0x835`. Its plaintext wire layout is:

```text
[u16 packetLength including header][u16 type = 1004]
[u32 colour][u16 channel = 0x835][u16 style]
[u32 identity][u32 reserved][u32 reserved]
[u8 count = 4]
[u8 sender length][0..15 bytes]
[u8 recipient length][0..15 bytes]
[u8 suffix length][0..15 bytes]
[u8 message length][0..255 bytes]
```

All numeric fields are little-endian. Total packet size must match its header,
be at least 29 and no more than 1024 bytes; every string length is bounded and
the final byte must belong to the fourth string. The first three strings
are ignored for login control, matching native consumption. The fourth string
is Windows-1252, matching the companion server `WireTextEncodingResolver`.
Unrepresentable ANSI bytes, embedded NUL, malformed lists, unexpected packet
types and wrong channels are rejected rather than interpreted as authentication.

Exact `ANSWER_OK` means **existing-character bootstrap has begun**, not world
admission. Exact `NEW_ROLE` means **character-creation route required**, not
character creation completed. Any other 1004 login-channel text is an untrusted
refusal message: the socket is terminated and no subsequent frame may be
processed. Authentication rejection without any reply yields a terminal EOF,
not a success result.

## Ownership and concurrency

`GameServerHandshakeSession.ReadLoginControlAsync` claims exactly one first
control read after proof transmission. It uses the existing bounded secured
frame reader, per-phase timeout, continuous CAST5 state and pooled frame
ownership. It disposes the decrypted frame before returning. No buffer or
socket ownership is transferred to a presentation layer.

An ordinary `ReadFrameAsync` call issued before this control read deliberately
marks the first-frame boundary as bypassed. Subsequent attempts to infer an
initial login outcome then fail: an unrelated packet cannot be interpreted as
the original 1004. Concurrent and repeated control reads are rejected without
creating waiting tasks. Once a successful control route is recorded, the
existing secured frame reader remains usable for future separately audited
character bootstrap or creation. No new writer is introduced.

Missing, malformed, canceled or timed-out control reads close the connection
and leave the secured session terminal. The refusal path closes immediately.
The existing one-shot proof, loopback-only trust boundary, disposal and
shutdown behavior are unchanged.

## Security and exclusions

Legacy DH + CAST5 framing is not authenticated transport. The accepted route
does not cryptographically authenticate the peer, establish ticket redemption
from the client's perspective, prove complete character bootstrap or grant
world authority. It is evidence only of the GameServer's observed first
login-channel control word on the loopback socket.

This slice does not enable remote endpoints, UI login, character registration,
repeat 1052, inbound 2078/2079/1006 processing, map entry or gameplay.
Full character creation is blocked by the current companion server; do not
invent a successful creation path.

## Regression gates

Tests use independently specified plaintext 1004 layouts (including the
server's literal refusal text), malformed packet cases, exact control words,
Windows-1252 refusal text and same-session loopback proof/secured-I/O tests.
Acceptance additionally requires locked restore, formatting, Release build,
full tests, retail content verification, Linux/macOS/Windows CI, Dependency
Review and all substantive PR review findings. User merge remains explicit.
