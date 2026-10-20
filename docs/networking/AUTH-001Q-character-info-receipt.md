# AUTH-001Q: Reading character information (1006) after the prelude

## Sources

Builds on AUTH-001P. The 5517 sources, in `20_notes.zip`:
- `20_notes/Network/MsgUserInfo1006-semantic-closure-2026-09-17.md`
- `20_notes/Network/Native-auth-string-validation-2026-10-18.md`
- `20_notes/Network/13_connection_lifecycle.md`, in particular
  `CMsgUserInfo__Process` at 0x764276, the builder at 0x764873, the send
  at 0x7648A3, and EnterMap action 74 after the 1006.

The companion server's
`ExistingCharacterBootstrapProcessor.ProcessAsync` sends
`1004 ANSWER_OK → 2078 → 2079 → 1006`. That order is confirmed in the
server code. It is not a general requirement of the 5517 client.

## How it works

`GameCharacterInfo1006.Read`, reviewed separately in AUTH-001P, parses
one plaintext 1006 into a snapshot that owns its fixed fields and raw
name bytes. `GameServerHandshakeSession.ReadCharacterInfoAsync` connects
that parser to the live session. It consumes one secured
server-to-client frame on the same connection, after the typed 1004 read
and the typed 2078/2079 reads.

- The typed reader can be used once. It must be claimed before an
  ordinary raw secured read takes the frame that follows the prelude.
- The claim and the result are both protected by the session's existing
  lifetime gate. While the typed read is in progress, no raw read, typed
  prelude read or second 1006 read can run.
- The existing decoder handles the socket, the directional CAST5 state,
  the eight-byte secured server signature and the pooled frame lifetime.
  A decoded `GameCharacterInfo1006` holds its own primitive fields and
  name bytes. It holds no borrowed frame, span or socket handle.
- The read uses the existing timeout (positive, at most 30 seconds) and
  the session's lifetime cancellation. EOF, a malformed 1006, a wrong
  packet type, a timeout, cancellation or disposal during the read all
  end the session. An already-canceled token and an invalid timeout are
  checked before the one-time read is claimed.
- After a successful read the connection stays open for a later
  world-entry request step, which will be reviewed on its own. A
  successful read does not mean the character is in the world.

## Differences from the retail client

The retail client does not require packets 2078 or 2079 before it
processes 1006. This typed API requires them because the companion
server always sends them before 1006. The ordinary `ReadFrameAsync` is
still available for experimenting with the retail packet order, but a
frame read that way can no longer be handled by the typed readers. The
order this API expects is not a rule of the retail protocol.

The retail client fills in hero state and tries to send action 74. With
`EntityId=0` it can update part of the hero state before the send fails,
and nothing is rolled back. This client applies no gameplay state and
does not send action 74. It keeps `EntityId=0` and empty or raw name
bytes as decoded values.

No UI or world session can reach the raw TCP stream. Non-loopback
connections, DNS, character creation, remote authentication, a builder
for action 74, hero updates and inbound world dispatch are not
implemented.

## Verification

Tests run a loopback AccountServer and GameServer with real DH and the
one-time 1052 proof from the originating account result. They use
independently encrypted 1004/2078/2079/1006 fixtures and a separately
encrypted packet that follows them. They cover a raw read that bypasses
the typed reader, a packet out of order, a timeout with no data, EOF, a
repeated read and a read in the wrong phase. The wire fixtures are
derived from source; they are not captures from a remote retail server.
CI runs locked restore, the format check, Release build and tests,
content verification, Linux, macOS and Windows builds, dependency review
and CodeQL.
