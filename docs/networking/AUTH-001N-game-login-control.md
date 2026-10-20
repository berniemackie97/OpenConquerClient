# AUTH-001N: First GameServer login control after the proof

The first secured packet the client reads after the 1052 proof is a 1004
login control. The parser follows the 5517 client as described in
`20_notes/Network/Native-auth-string-validation-2026-10-18.md`, which
includes an isolated instruction replay and confirms the 1004 dispatch.

## 5517 client and companion server

The companion server sends a secured 1004 on login channel `0x835`. It
carries four length-prefixed byte strings, and the control words are the
ASCII `ANSWER_OK` and `NEW_ROLE`. The 5517 parser checks the packet
header and type and reads the strings through
`MsgStringList_TryGetEntry`. The 5517 1004 handler ignores lookup
failures. It does not require a count of four, does not limit the
earlier strings to 15 bytes, and does not require the whole declared
string list to be consumed.

The retail client compares the C-string prefix of the fourth string with
`_stricmp` at `0x5DCC93` and `0x5DCD13`. ASCII letter case and any bytes
after the first NUL therefore do not change the result. The first three
entries may have longer length prefixes, and their lengths still decide
where the fourth string starts. The recipient and sender strings can
affect other filtering or logging in the retail client, but they do not
gate the login-control branch.

The parser reads the required prefix as bytes and does not force a
Windows-1252 decode. A refusal keeps its own copy of the raw bytes, and
the code that eventually displays it must choose a code page. The retail
1004 filter uses the system code page (CP_ACP), not a fixed CP1252
decoder. The clean retail `slient.dat` substitution file is empty. This
note makes no claim about substitutions a particular consumer might
apply.

## Differences from the retail client

The retail string iteration can read constructor-zeroed memory beyond
the end of a short packet. With extreme 1004 lengths it can even read
past its own allocation. This client does not reproduce those
out-of-bounds reads or the memory corruption they can cause. If a
selected field declares more bytes than the packet holds, the packet is
rejected and the secured session closes. The secured frame decoder still
enforces the 1024-byte cap, a consistent length and the per-direction
signature check. Those signatures do not authenticate the remote peer.

A missing fourth entry gives an empty refusal. A fourth entry whose
malformed length runs past the end of the input is a protocol error.
Bytes after the fourth entry, and extra entries nobody asked for, are
ignored as long as they stay within the packet. None of these retail
quirks is used to grant character or world access.

A successful control only selects the character-login route. The socket
is still loopback-only. A refusal ends the session, and no later secured
frame can be treated as the first control. The session owns the
lifetimes of the cipher and all buffers. There is no UI, character
creation or world entry yet.

## Verification

Regression cases derived from the 5517 client cover case-insensitive
controls, matching on the prefix before a NUL, a count below 4, extra
entries and trailing data, oversized ignored entries, raw legacy bytes
(including 0x81) and a selected length that runs past the input. Tests
with a real encrypted peer check that the control can be read only once
and that a refusal ends the session. CI runs the format check, Release
build and tests, retail content verification, Linux, macOS and Windows
builds, dependency review and CodeQL.
