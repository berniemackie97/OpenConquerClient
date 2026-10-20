# AUTH-001O: Existing-character 2078/2079 prelude

Sources: `20_notes/Network/10_extended_packets.md` and
`20_notes/Network/Native-auth-string-validation-2026-10-18.md`. The 2078
and 2079 parsers follow the 5517 client as described there.

The companion server currently sends
`1004 ANSWER_OK → 2078 → 2079 → 1006`. The 5517 client does not need
2078 or 2079 before it processes 1006.
`ReadExistingCharacterPreludeAsync` reads the sequence the companion
server sends. It runs once, and nothing else can read from the session
while it does. The retail dispatcher has no such prerequisite, and this
read cannot grant world entry.

## 2078

The packet starts with
`[u16 length][u16 type=2078][u32 timestamp][u8 flag]`, followed by the
account location bytes. The retail `CHero_SetAccountLoginAlertData` at
`0x6577BF` copies the bytes up to the first NUL without checking them as
Windows-1252. Text display later uses its own code-page path. This
client keeps its own copy of the location bytes
(`LastLoginLocationBytes`) instead of a CP1252 string. The five byte
values often described as undefined in Windows-1252 are valid raw values
here. Valid multibyte text is not decoded while the packet is parsed.

Padding after a NUL is ignored, within the packet. For short packets,
the retail client's constructor-zeroed packet storage acts as an implied
terminator, so a size-9 history with no location is accepted. A
maximum-size 1024-byte packet with no terminator is rejected, so the
client does not repeat the retail client's unlimited `strlen`. The
timestamp and flag fields and the packet header and type have fixed
sizes, and the maximum packet size still applies.

## 2079

The state is a single u32 at offset +4. In the retail client, state 0
selects UI global 2000, state 1 selects 2001, and any other value
changes nothing. Trailing bytes within the packet are ignored. The 2079
reader already matched the 5517 client and did not change.

## Ownership, safety and limits

The session owns the socket, the pooled decrypted frames, the continuous
CAST5 cipher and the terminal failure state. One deadline covers both
frames. The prelude reader does not change hero state, admit the player
to the world, consume the 1006 or send EnterMap action 74. The required
frame types and order reflect what the companion server sends today; the
5517 client does not require them.

## Testing

Tests cover raw CP1252 and CP936 bytes kept without early Unicode
conversion, short zero-padded packets and packets with a NUL, extreme
packet lengths, rejection of a full-length packet with no terminator,
the 2079 state values, and lifetime and concurrency behavior over
encrypted loopback. CI runs on the full solution.
