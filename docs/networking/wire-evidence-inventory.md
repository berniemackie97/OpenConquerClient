# CP3.2 — Wire evidence inventory

[Issue #248](https://github.com/berniemackie97/OpenConquerClient/issues/248) |
[Ownership decision](wire-contract-ownership.md)
[Route coverage](wire-route-coverage.md) | [Route task #316](https://github.com/berniemackie97/OpenConquerClient/issues/316)

## Scope and evidence grades

This is the first-pass inventory of named packet codecs in `OpenConquer.Networking`
and `OpenConquer.Protocol` at the start of CP3.2. It includes client-only
and server-only implemented messages; a missing implementation on one side
does not mean the message is unsupported by the original protocol. Active conditional send paths, manual action routing and world ingress fan-out
are reconciled against both repositories in the [route coverage matrix](wire-route-coverage.md),
with a separate evidence grade for each supported variant. The complete running-client
capture corpus remains outstanding.

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
| Game | 1134 | Content-backed admission mission catalog 1134/3 | Mission catalog | Three independently captured original-client frames; connected-world smoke pending |
| Game | 1151 | Flower-rank request | Not a dedicated packet codec | Missing |
| Game | 2078 | Login history | Login history | Missing |
| Game | 2079 | Server state | Server state | Missing |

The table describes named codec boundaries rather than a claim that the
entire gameplay feature is implemented. In particular, server spawn, social
and skill encoders do not imply client presentation. Some world-entry
subtypes are emitted by the application rather than exposed as separate
`OpenConquer.Networking` types. These remain subject to the call-site
inventory gate.

## Verified heartbeat 1009/27 send bytes

The source-client world-entry capture includes 30 complete 88-byte
1009/27 frames: 24 from capture 1 and six from capture 2. Each frame
is retained in the existing `1009-27-heartbeat.observed.json` fixture,
with its capture sequence, exact logical bytes and SHA-256. The source
executable, direction and decryption boundary are pinned with the fixture.

The client writer transmits the unmodified millisecond timer at +16.
The earlier message builder computes an XOR intermediate, but the
send path replaces that field with the current timer before transmission.
Client tests write all thirty captured frames through the production
encoder. Server tests independently parse all thirty and verify
byte-preserving echoes; those managed echoes do not constitute
original-server output.

These captures cover the observed 1009/27 send family, not equipment
or other 1009 subtypes. [Bug #299](https://github.com/berniemackie97/OpenConquerClient/issues/299)
tracks the accepted timer fix. [#248](https://github.com/berniemackie97/OpenConquerClient/issues/248)
retains the missing packet-variant and original-server-output gates.

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

## Frozen test corpus manifest

The test-only `tests/Fixtures/WireGolden/manifest.json` pins the canonical
client source revision `e13bf5724975ec825e64a3609719afedd9b6af41`,
the SHA-256 of each included fixture file, and an explicit list of every
registered case. Both test suites require the same manifest SHA-256
`60ca8ca1abe38bece22d57a7f329de60952efc3139008940919029f48c392361`,
verify every listed file and reject unlisted fixture JSON files. The corpus contains two static-derived cases, eight observed client-message groups
(1009/27, 1134/3, 10005 selectors 0/1, 10010 admission actions, 10010/146,
1151/2, 10010/145 and 10010 replay responses), and one project-defined
registration case. Six later groups pin 32 individual frames from separately
retained running-client captures.
These observations cover only their documented variants and directions.
A corpus revision requires coordinated review of the source evidence,
fixture bytes, manifest and pinned hash in both repositories. No runtime
package or build-time cross-repository dependency is introduced.

## Account registration F001/F002 project vectors

The corpus includes `F001-F002-registration.project-defined.json`, based on the
in-client registration specification, not on the original 5517 executable.
It pins a 260-byte request with a synthetic account and seeded credential
block and the five valid eight-byte result frames (codes 0–4). Client tests
serialize F001 and parse all F002 results; server tests independently decode
F001 and serialize all F002 results. Each frame has its own SHA-256, and the
manifest pins the fixture-file SHA-256. This is specification-based evidence
and cannot satisfy the running-client capture requirement for other messages.

## Mission catalog 1134/3 admission

`MissionCatalog.Load` reads `ini/Questinfo.ini` from loose installed
content, validates the declared numbered sections, requires positive IDs,
deduplicates and sorts them, and enforces a file-size, row and 240-distinct-ID
limit before sending. Invalid or absent installed data is a local admission
failure. The separate executable investigation documented omission for an
empty catalog; this application deliberately requires a valid catalog.

After an accepted login-channel 2101 result, the session owns one immutable
ID snapshot. `GameServerHandshakeSession.SendMissionCatalogAsync` encodes up
to 80 IDs in each 1134/3 packet, retaining a single secured writer and its
directional cipher state across the contiguous burst. The initial
existing-character route sends before outbound world-entry action 74.
Creation retains that same snapshot through the repeated login proof and
sends after the subsequent accepted 2101. The server accepts this observed
early ordering. Failed or cancelled writes terminate the session.

The 2026-11-08 original-client capture in
`20_notes/Network/evidence/cp32-world-entry-2026-11-08` contains the
following independently observed logical frames, taken after decryption and
trailer validation:

| Sequence | Records | Bytes | SHA-256 |
| --- | ---: | ---: | --- |
| 13 | 80 | 968 | `45d3167ae09ed03bc9f4d41790a3fdbe4587469b596e5f442ce686521b717001` |
| 14 | 80 | 968 | `3b9c6170ceed1f07d9bde4c8bd74de175709d698c5fb3edbb451850190f66b80` |
| 15 | 64 | 776 | `49cd8b7c3c96dc92dde25c837a7f1002f58c5c1ac05e23dcdf69b41a9eb24a49` |

These hashes apply to one selected source-asset sample (224 distinct IDs),
not to arbitrary installed content. [Issue #306](https://github.com/berniemackie97/OpenConquerClient/issues/306)
retains managed client/server integration and on-device admission acceptance.
The wider [CP3.2 corpus #248](https://github.com/berniemackie97/OpenConquerClient/issues/248)
remains incomplete.

## Action 108 to 146 correction acknowledgement

[Correction probe #304](https://github.com/berniemackie97/OpenConquerClient/issues/304)
observed three original-client 10010/146 outgoing replies for changed, same-tile and
repeated hero-targeted 108 corrections, and no reply for two foreign-entity inputs.
The recorded reply has the corrected tile pair at +8/+10, a fresh client tick
at +16, action 146 at +20, the current facing at +22, zero at +24/+26,
the active map ID at +28 and `0xFFFFFFFF` at +32.

`GameWorldAction10010.WriteCorrectionReply` constructs this 38-byte message from
the current hero state. `WorldInboundState` signals every applied hero 108,
including equal-position and repeat cases; `WorldFrameEgress` bounds the queue to the world-entry hydration frame
maximum (4096) and serializes replies on the existing secured world write
lane. It checks periodic heartbeat deadlines between consecutive correction
writes so sustained correction traffic cannot starve the heartbeat. Packet construction and local scheduling tests are not
substitutes for the complete [CP3.2 corpus #248](https://github.com/berniemackie97/OpenConquerClient/issues/248)
or a real-device session smoke. [Implementation #263](https://github.com/berniemackie97/OpenConquerClient/issues/263)
tracks remaining acceptance.

## Captured movement and world-action vectors

The shared `10005-walk.observed.json` fixture now includes all 23
disclosed original-client 10005 frames in the retained existing-character
sessions: twelve selector-1 frames in capture 1 and eleven selector-0
frames in capture 2. The complete packet field and SHA-256 for each
record come from
`20_notes/Network/evidence/cp32-world-entry-2026-11-08/capture.json`.
Client tests verify the complete packed direction, selector, identity,
map and movement clock through the existing reader and reproduce every
one of the 23 original-client frames with the ordinary walk/run writer.
Server tests independently parse and byte-preservingly serialize all 23.
The writer stores the packed direction and selector as a byte each in
zeroed fields. The secured sender accepts these caller-supplied values;
it does not generate input commands, source entropy or mounted moves.
Original-client movement emissions and independently recorded server
traffic remain distinct.

The action fixtures retain the same ten source-client entry actions in
`10010-world-entry.observed.json` and three independently captured
correction replies in `10010-correction.observed.json`. Each has its
own ordered record references and frame digests.

The client walk writer reproduces the observed packed direction byte
and selector-0/1 requests, but still has no active world input caller.
Completing its packet evidence does not implement local movement or validate playback on connected
clients. The server's byte-preserving output is a managed serializer
check, not an original-server emission.

## Captured conditional world-entry and ghost-transition requests

The independently observed `1151-flower-rank.observed.json` records both
80-byte subtype-2 requests from the original client, in capture 1 record 21
and capture 2 record 20. `GameFlowerRankRequest1151.Write` reproduces both
exact frames when its already-implemented body-digit condition is met. The
companion server deliberately ignores this unsupported request while waiting
for its authenticated admission action ladder; it does not need a competing
1151 codec for that behavior.

The `10010-ghost-transition.observed.json` fixture pins capture 2 record 45,
a 38-byte 10010/145 message with its original timestamp and entity ID. The
client generic action parser retains its fields; the server's
`GameGhostTransitionRequest` validates the incoming subtype and identity.
The client has no active post-death /145 producer, so these tests do **not**
complete ghost transition gameplay. The original-client jump action 137 and
trade-quit 1056 observations in the same capture have withheld payload bytes,
not qualifying packet vectors. No synthetic replacement is included.

## Captured replay response windows

The exact 14 client-emitted 10010 frames in
`10010-world-replay.observed.json` cover capture 1 response records
112, 114–115, 117, 119, 121, 123 and 133, 135–136, 138, 140, 142, 144
in `20_notes/Network/evidence/cp32-world-entry-2026-11-08/capture.json`.
The source capture file SHA-256 is
`177e7e97fb434090a1716905f6c2fe2ca0752aa61d313a3daaf757e748c6cd0f`.
Each window emits actions 76, 96, 77, 78, 97, 251 and 132 in that order;
action 251 carries the session's version value rather than the frame clock.

The triggering 10010/75 frames at records 111 and 132 are controlled-peer
inputs, including one deliberately shortened stimulus, and are not classified
as original-client output. The managed client and independent server tests
verify exact source bytes and wire fields. These checks do not establish
original-server behavior or real connected-client interoperability.


## Conditional world-entry and gameplay capture

The retained [November 8 conditional-world capture](https://github.com/berniemackie97/20_notes/tree/66e999d84c5f3ead82284e6bdf6e012044696dc7/Network/evidence/cp32-conditional-world-2026-11-08)
identifies executable SHA-256
`b6ba5ab8573ec073ea271f57b16fa14de1c67bfb44efd5aa7cfbfc0d3b488a62`.
Fourteen complete logical frames were observed on the client-to-peer path after
game decryption and trailer validation:

- 10010/408, one 38-byte frame after the controlled map-information packet changed flag bit 38; the flags-zero control does not emit 408.
- 10010/137, twelve 38-byte ground-jump requests carrying target and origin tiles, map identity, and ground flags.
- 1056/2, one 12-byte trade-quit request after a controlled actor-status transition.

The test-only corpus stores these as
`10010-client-state.observed.json`,
`10010-ground-jump.observed.json`, and
`1056-trade-quit.observed.json`. Each fixture names the capture and pins
every frame digest. The client checks its existing 408 writer against the
captured frame, decodes the twelve 10010 jump requests, and checks the 1056
wire layout without claiming a local jump or trade sender. Its existing
created-character admission test covers both the map-bit-38 transition and
the flags-zero control. The server independently parses 408 and 1056, and
parses and byte-preservingly re-encodes all twelve 137 requests through
the jump-acknowledgement writer.

The shared manifest SHA-256 is
`2200507e70de4cbc3aae720ef21be849627ff4871b336ada3db056353bf0790b`.
Controlled peer 1110 and 10017 frames are inputs, not client output;
managed re-encoded jump acknowledgements are not observed original-server
emissions. These cases do not establish all implemented packet variants,
original-server output, or the protected-content CI gate. Those remain
acceptance requirements of [#248](https://github.com/berniemackie97/OpenConquerClient/issues/248).

## Connected managed peer traffic

`20_notes/Network/evidence/cp42-managed-peer-2026-11-09/managed-ocs.json`
records four logical server-to-client frames from two connected managed
sessions: a 233-byte 10014 spawn, two 24-byte 10005 walking/running frames,
and a 38-byte 10010/135 removal. The original JSON SHA-256 is
`ce20afe2bcbbb0e7219567754d856e23e4cd3e68c2fd954481db20054889697b`.
It is copied without changing its bytes to
`tests/Fixtures/ManagedWorld/peer-movement.observed.json` and validated
through the existing world dispatcher, not the original-client golden
manifest. Each record's provenance is `managed-ocs` and each frame's
SHA-256 is checked before replay.

A separate controlled graphics replay demonstrates changed movement frames
and cleared peer resources on departure. The connected sessions were headless,
so the evidence cannot establish interactive appearance and pose equivalence.
The emitting server was our own implementation; it is not independently
observed original-server traffic for [#248](https://github.com/berniemackie97/OpenConquerClient/issues/248).
