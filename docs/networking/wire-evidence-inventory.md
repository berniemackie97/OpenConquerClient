# CP3.2 — Wire evidence inventory

[Issue #248](https://github.com/berniemackie97/OpenConquerClient/issues/248) |
[Ownership decision](wire-contract-ownership.md)

## Scope and evidence grades

This is the first-pass inventory of named packet codecs in `OpenConquer.Networking`
and `OpenConquer.Protocol` at the start of CP3.2. It includes client-only
and server-only implemented messages; a missing implementation on one side
does not mean the message is unsupported by the original protocol. Code that
constructs frames directly outside the named packet types still needs an
additional call-site audit before the inventory can be considered exhaustive.

**Live capture** means original 5517 executable output or input observed
during a controlled running-client session, with exact direction, framing,
build hash, extraction point and immutable packet bytes retained. **Static-derived**
means independently constructed bytes based on inspected instructions or
data; **synthetic** means a deliberately constructed test. Neither counts
toward the live-capture acceptance requirement.

The supplied `20_notes` snapshot contains
`Network/evidence/typed-world-dispatch-2026-11-06/wire-layout-golden.json`.
It explicitly marks its 10010, 1016 and 10017 sentinel frames as
static-derived, explicitly recording that no running-client observation occurred. The additional
packet-dispatch, off-map and credential evidence examined here does not
establish a complete corpus of runtime packet captures. No case in this
initial inventory is claimed as a qualifying live capture.

## Named codec and message inventory

The phase and subtype are part of a fixture's identity. Packet IDs alone
are not unique across account/game stages or message variants.

| Phase | ID | Named client support | Named server support | Runtime vector gate |
| --- | --- | --- | --- | --- |
| Account | 1059 | Seed | Seed | Missing |
| Account | 1060 | Credential request | Credential request | Missing; credentials must be sanitized at collection |
| Account | 1055 | Authentication response | Authentication response | Missing; ticket/address material must be sanitized |
| Account | 1100 | Hardware report | Hardware report | Missing; personal identifiers must not enter the corpus |
| Account | 1052 | Resource version report | Resource version report | Missing |
| Account | 0xF001 | Registration request | Registration request | Project-defined extension; not eligible for 5517 capture |
| Account | 0xF002 | Registration response | Registration response | Project-defined extension; not eligible for 5517 capture |
| Game | 1052 | Login proof | Login proof | Missing; secret/session values must not enter the corpus |
| Game | 1001 | Character creation | Character creation | Missing |
| Game | 1004 | Login control / talk | Talk | Missing; distinguish message variants |
| Game | 1006 | Character information | Character information | Missing |
| Game | 1008 | Item records | Item snapshots | Missing |
| Game | 1009 | Items, equipment, latency | Equipment, latency | Missing; distinguish subtype |
| Game | 10005 | Walk | Walk | Missing; distinguish movement selector |
| Game | 10010 | Action / world entry | Actions, correction, jump, portal | Missing; static-derived layout exists |
| Game | 10014 | Not yet a typed client reader | Player spawn | Missing |
| Game | 10017 | Attribute updates | Attribute updates | Missing; static-derived layout exists |
| Game | 1016 | Weather | Weather | Missing; static-derived canary added below |
| Game | 1019 | Not yet a typed client reader | Social relations | Missing |
| Game | 1025 | Not yet a typed client reader | Weapon skills | Missing |
| Game | 1037 | Latency report | Not a dedicated server packet codec | Missing |
| Game | 1056 | Not yet a typed client reader | Trade quit | Missing |
| Game | 1103 | Not yet a typed client reader | Magic | Missing |
| Game | 1104 | Not yet a typed client reader | Skill experience | Missing |
| Game | 1106 | Not yet a typed client reader | Syndicate attributes | Missing |
| Game | 1110 | Map information | Map information | Missing |
| Game | 1115 | Not yet a typed client reader | Kickout | Missing |
| Game | 1134 | Mission catalog (world entry) | Mission catalog | Missing |
| Game | 1151 | Flower-rank request | Not a dedicated packet codec | Missing |
| Game | 2078 | Login history | Login history | Missing |
| Game | 2079 | Server state | Server state | Missing |

The table describes named codec boundaries rather than a claim that the
entire gameplay feature is implemented. In particular, server spawn, social
and skill encoders do not imply client presentation. Some world-entry
subtypes are emitted by the application rather than exposed as separate
`OpenConquer.Networking` types. These remain subject to the call-site
inventory gate.

## First shared fixture: weather 1016

Both repositories check in the identical test-only
`tests/Fixtures/WireGolden/1016-weather.static-derived.json`. It contains
20 unencrypted packet bytes, source-image hash, packet SHA-256, direction,
phase and **`runtimeCaptured: false`**. The sentinel fields come from the
separate `20_notes` layout evidence, not from a running executable.
Client tests decode the pinned bytes; server tests must emit those bytes
independently. This test can reveal cross-repository drift but does not
satisfy a single live-capture requirement.

## Second shared fixture: action 10010

The checked-in `10010-action.static-derived.json` uses the 38-byte,
all-field sentinel recorded in the same `20_notes` evidence set as the weather
fixture. The client verifies the 11 decoded fields, and the server verifies
that its packet serializer writes exactly the pinned bytes. The test also
checks the exact frame size and identifier; the fixture's hash is
`4b56d70dcb3e59d3ed0a1aa316c2d56603e6eb0dd0acb2dfe426ff0cf7d30a03`.

Action 108 is used only to exercise every byte in this layout. The
nonzero sentinel fields do not represent an admissible position-correction
message and do not change the server's input validation. This is
static-derived test evidence, not a running-client packet capture.

## Confirmed packet-writing boundaries

The client's `LoginAccountSession` writes account requests 1060 and 0xF001,
then post-authentication reports 1100 and account-phase 1052. Its
`GameServerHandshakeSession` writes game-phase 1052, character-creation 1001,
walking 10005, latency 1009/1037, action 10010 and flower-rank 1151
through typed packet writers. `GameWorldEntryBootstrap` sequences action
requests through that same game session; `WorldFrameEgress` delegates
heartbeat/report writes rather than defining another packet layout.

The server's `LoginFrameWriter` serializes account `IPacket` values through
`WireFrameEncoder`; `GameSecuredFrameWriter` serializes game `IPacket`
values through `GameWireFrameEncoder`, then adds the secured envelope.
Bootstrap and world factories select packet instances rather than
adding another framing format.

This identifies the central outbound paths. It does not close the
full-site audit: all packet-producing callers, packet variants and
non-typed data paths need a separate source review before a complete
runtime capture checklist can be frozen.

## Capture and completion gates

A running-client capture requires user-controlled Windows VM tooling and
a byte-preserving trace at a documented pre-encryption or post-decryption
boundary. The collection record must identify the executable SHA-256,
packet direction, session phase, ID and subtype, capture point, full
packet length, exact bytes, packet SHA-256 and an evidence reference.
Remove secrets and identifying data at the source; replacing payload
bytes after capture produces a synthetic derivative and must be labeled
accordingly. Do not commit keys, credentials, tickets or personal data.

Before claiming CP3.2 complete, audit manually composed packet writes,
obtain one qualifying running-client vector for each supported 5517-defined
message/variant, mirror source-pinned fixtures in both test suites, verify
negative header/signature/sequence cases and independently run each
repository's CI. `0xF001` and `0xF002` require project-defined vectors
instead. The absence of a qualifying capture is a blocker, not a reason to
fabricate one. Acceptance and project-board status remain open on #248.
