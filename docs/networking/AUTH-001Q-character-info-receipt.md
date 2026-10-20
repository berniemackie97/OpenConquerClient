# AUTH-001Q — Exclusive post-prelude secured character-information receipt

## Audited heads and evidence

Client base: `95e9b654f20eda1f1e482ba4b0cfc08b26ed5d94`,
merged AUTH-001P #134. Server main:
`bda9f0e76108872b648a9cfa5bb245c82589023e`.

Native evidence in the updated `20_notes.zip`:
- `20_notes/Network/MsgUserInfo1006-semantic-closure-2026-09-17.md`
- `20_notes/Network/Native-auth-string-validation-2026-10-18.md`
- `20_notes/Network/13_connection_lifecycle.md`, specifically
  `CMsgUserInfo__Process` 0x764276, builder 0x764873,
  send 0x7648A3, and post-1006 EnterMap action 74.

The companion server's
`ExistingCharacterBootstrapProcessor.ProcessAsync` sends
`1004 ANSWER_OK → 2078 → 2079 → 1006`.
That exact order is a **verified server implementation fact**,
not a native universal requirement.

## Scope

The already independently audited `GameCharacterInfo1006.Read` parses
one **plaintext** 1006 into an owned fixed-field/raw-name snapshot.
This slice connects that parser to
`GameServerHandshakeSession.ReadCharacterInfoAsync`, consuming one
secured server-to-client frame on the same live connection after the
existing typed 1004 and typed 2078/2079 convenience flow.

- The typed reader is one-shot and must be claimed before an ordinary
  secured raw read takes the post-prelude frame.
- Claim and publication are protected by the existing session lifetime
  gate. In-flight typed receipt excludes all competing raw reads, typed
  prelude reads and duplicate 1006 reads.
- The existing decoder owns the socket, directional CAST5 state, secured
  eight-byte server signature and pooled frame lifetime. A decoded
  `GameCharacterInfo1006` contains owned primitive fields and name
  bytes, not a borrowed frame, span or socket handle.
- The operation uses the existing positive, max-30-second timeout and
  lifetime cancellation. EOF, malformed 1006, wrong type, timeout,
  cancellation or disposal during the read leave the session terminal.
  Pre-cancellation and invalid timeout are checked *before* claiming
  the one-shot operation.
- Success leaves the connection alive for a future, separately audited
  world-entry request stage. It does not infer world admission.

## Native fidelity versus intentional restrictions

The native client does **not** require packets 2078 or 2079 before
processing 1006. This scoped typed API requires them because the
current companion server always emits them before 1006; an ordinary
`ReadFrameAsync` remains available for independent native-order
experimentation but forfeits retrospective typed ownership. Do not
promote this API sequencing into a global retail protocol claim.

Native processing hydrates hero state and attempts to send action74.
Native `EntityId=0` can partially mutate hero state before the
failed send, with no rollback. This slice deliberately does **not**
apply any mutable gameplay state or send action 74; it preserves
`EntityId=0` and empty/raw name bytes as observations.

No UI or world session has access to the raw TCP stream; no non-loopback
connect, DNS, creation path, remote authentication, action74 builder,
hero mutation or inbound world dispatch is implemented.

## Verification

Tests use a loopback AccountServer + GameServer with real DH,
the originating one-shot 1052 proof, independent encrypted
1004/2078/2079/1006 fixtures, a separately encrypted continuation
packet, raw-read bypass, out-of-order packet, silent timeout, EOF,
repeated read and premature phase rejection. Wire fixtures are
source-derived **not remote retail captures**. Full locked restore,
format, Release build/tests, content verification, Linux/macOS/Windows
CI, dependency review, CodeQL, review and final PR diff audit
are required. User merges only.
