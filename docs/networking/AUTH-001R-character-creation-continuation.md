# AUTH-001R: Character creation on the login session

## Sources

Builds on AUTH-001N and AUTH-001Q. The 5517 sources, in `20_notes`:
- `20_notes/Architecture/12_create_role_flow.md` (create-role entry, talk channels, submit, result, back)
- `20_notes/Network/01_core_packets.md`, MsgCreatePlayer (1001)

Verified in the 5517 client:

| Function | Address | Role |
| --- | --- | --- |
| `CMsgCreatePlayer1001__Build` | `0x7200BF` | 124-byte request and cancel |
| `CRoleCreateFlashDlg_SubmitCreatePlayerRequest` | `0x51C5C3` | name gate, body and profession, send |
| `CRoleCreateFlashDlg_SendCreatePlayerCancelAndPostHostCommand481` | `0x51C54B` | cancel, then `0x481` disconnects |
| `CMsgTalk__Process` split | `0x7314D2` | channel 2100 → create-role queue, 2101 → login queue |
| `CMyShellDlg_HandleCreateRoleStatusSystemTalk` | `0x5DCDDF` | `ANSWER_OK` → `CConnectionFlow__SendGameSessionBootstrap` |
| `CConnectionFlow__SendGameSessionBootstrap` | `0x6DF1D0` | re-sends 1052 on the same socket |

The companion server's `GameCharacterCreationLoginProcessor` answers each 1001 on channel 2100, accepts
one repeated 1052 after `ANSWER_OK` without redeeming the ticket again, and then runs the
existing-character bootstrap: `1004 ANSWER_OK` (2101) → 2078 → 2079 → 1006.

## How it works

`GameServerHandshakeSession` gains a create-role continuation after a `NEW_ROLE` login control:

| Step | Method | Allowed when |
| --- | --- | --- |
| request | `SendCharacterCreationAsync(name, body, profession)` | after `NEW_ROLE`, or after a refusal |
| result | `ReadCharacterCreationResultAsync` | after a request |
| cancel | `SendCharacterCreationCancelAsync` | instead of a request; the session is closed afterwards |
| repeated proof | `SendRepeatedLoginProofAsync` | once, after `ANSWER_OK` |

- The result reader accepts only a 1004 on channel 2100. `ANSWER_OK`, compared like `_stricmp`, is
  `Created`; any other text is a `Refused` outcome carrying owned bytes, and the request step reopens on the
  same socket. A frame on another channel, any other packet, EOF or a timeout ends the session.
- While the exchange is open, nothing else can use the socket: raw reads, the prelude and 1006 readers and a
  second login control are refused until the repeated proof has been sent.
- After the repeated proof the session expects one more login control. It must be `ANSWER_OK`; a second
  `NEW_ROLE` or a refusal ends the session. The existing prelude and 1006 readers then apply unchanged.
- `LoginAccountExchangeResult.WriteRepeatedGameLoginProof` composes the second proof once, with the same
  fields as the first, and only after the first. The caller must keep the account result alive until then.
- The 1004 message reader shared by both channels is `GameControlTalk1004`, factored out of
  `GameLoginControl1004` without changing its behaviour.
- The 1001 writer checks the native builder's structural limit: 1 to 15 bytes without NUL. The create-role
  screen applies the name rules (4 to 15 bytes, glyph check) before it sends. An invalid name or an
  out-of-order call is rejected before the socket is touched and does not end the session.
- Timeouts stay positive and at most 30 seconds per step. Waiting at the create-role screen holds no read
  open, so a server that closes the idle connection is noticed at the next request.

## Differences from the retail client

The 40-byte MAC text at +80 is sent empty. The native builder sends the first NetBIOS adapter's address as
twelve hex digits, and leaves the field empty when that lookup fails; this client never sends a hardware
identifier, so it always takes the lookup-failure path. The Facebook launch variant's 1202 and 1203 packets
are not implemented.

## Verification

Tests pin the 1001 create and cancel bytes, the channel-2100 outcomes (case-insensitive `ANSWER_OK`, owned
refusal text, wrong channel, missing message), the repeated-proof guard, and the live session against a
loopback peer: a refusal followed by an accepted request, the repeated proof and the full existing-character
bootstrap; the cancel bytes and close; out-of-order use; a result on the wrong channel or a close instead of a
result; and a second `NEW_ROLE` after the repeated proof. The create-role screen and its flow are separate
changes.
