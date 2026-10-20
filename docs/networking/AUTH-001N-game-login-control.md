# AUTH-001N — First post-proof GameServer login control (native compatibility correction)

Original implementation: PR #132, client main `fa3dcd81c419e9a048cfebff562eef013c4dcb75`.
Correction evidence: `20_notes/Network/Native-auth-string-validation-2026-10-18.md`,
including isolated instruction replay and the corroborated 1004 dispatch.

## Verified native / companion distinction

The companion server sends a secured 1004 on login channel `0x835`
with four length-prefixed byte strings and known ASCII controls
`ANSWER_OK` and `NEW_ROLE`. The native 5517 parser validates packet
header/type and delegates strings to `MsgStringList_TryGetEntry`.
The native 1004 handler ignores lookup failures; it does **not** require
count exactly four, bounded 15-byte earlier string lengths, or complete
consumption of the entire declared string list.

The fourth string's **C-string prefix** is compared via native
`_stricmp` at `0x5DCC93` / `0x5DCD13`. Consequently, ASCII letter
case and bytes after its first NUL do not affect the control decision.
The first three entries may have larger length prefixes; their lengths
still determine the fourth string's position. Recipient/sender strings
can affect other native filtering or logging; they do not act as a
hard validation gate for the login-control branch.

The correction reads the required prefix without reinterpreting native
byte text as obligatory Windows-1252. Refusals retain **owned raw bytes**;
the eventual presentation layer must select an explicit code page.
The native 1004 filter uses system CP_ACP rather than a fixed CP1252
decoder. The clean retail `slient.dat` substitution file is empty;
arbitrary consumer-specific substitutions are not claimed here.

## Intentional safe managed divergences

Native string iteration can use constructor-zeroed memory outside a
short received packet; for extreme 1004 lengths it can even read past
the native allocation. **We will not emulate unsafe out-of-bounds
reads or memory corruption.** A selected field whose declared bytes
exceed the received packet is rejected and the secured session closes.
The secured frame decoder retains the 1024-byte cap, length consistency
and direction-specific signature checks; these signatures do not
authenticate remote peers.

Missing fourth entry produces an empty refusal, whereas a fourth entry
with a malformed length running beyond bounded input is a protocol error.
Bounded bytes after the fourth entry or extra unrequested entries are
ignored. No native exception is used to grant character or world authority.

Successful controls establish a character-login route only. The socket
remains loopback-only. A refusal terminates; no later secured frame
may be relabeled as the first control. The session owns all cipher and
buffer lifetimes. No UI, creation implementation or world admission
is introduced.

## Verification

Native-derived regression cases cover case-insensitive controls,
NUL-prefix matching, count<4, extra count/trailing data, oversized
ignored entries, raw legacy bytes (including 0x81) and bounded selected
length failures. Real encrypted peer tests verify one-time control
ownership and terminal refusal. Format, Release build/test, retail
content verification, Linux/macOS/Windows CI, dependency review and
CodeQL remain required before merge.
