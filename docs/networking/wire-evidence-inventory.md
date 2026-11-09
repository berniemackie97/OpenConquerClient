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

Original-client capture 1, frame 40, in
`20_notes/Network/evidence/cp32-world-entry-2026-11-08/capture.json`,
records an 88-byte outgoing heartbeat with SHA-256
`aa2d5b328c0f90b9b80eea69f1e648ee33653fc9092ff633d72e4c9e05d4c922`.
Its hero ID is 1,000,000 and its transmitted millisecond timer is 68,952,244.
Reconstructing the header, fields and zeroed remainder reproduces that digest.
The reconstructed bytes are verified against the reported capture hash, but
were not copied directly from the capture file into this repository.

The packet writer now transmits the unmodified timer at +16. The earlier
message builder computes an XOR intermediate, but the original send path
replaces that field with the current timer before queuing transmission.
An unchanged echo therefore carries the original timer; the client
computes elapsed time with unsigned subtraction, including counter wrap.
The server's existing byte-preserving echo needs no implementation change.

The independently observed 88-byte frame is retained in
`tests/Fixtures/WireGolden/1009-27-heartbeat.observed.json`. The client
verifies the encoder output against those exact logical bytes; the server
independently parses and re-encodes the mirrored frame. The capture source,
direction, collection boundary and executable digest are pinned with its
contents. This single observed message does not establish capture coverage
for other 1009 subtypes or every CP3.2 packet variant. [Bug #299](https://github.com/berniemackie97/OpenConquerClient/issues/299)
tracks the fix; [CP3.2 #248](https://github.com/berniemackie97/OpenConquerClient/issues/248)
retains the corpus and remaining capture acceptance.

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
`ad0080adb7730c1235afa411cd25dc84eeb2f47c1630d43d1cf4bf7b89a373a1`,
verify every listed file and reject unlisted fixture JSON files. The corpus contains two static-derived cases, seven observed client-message groups
(1009/27, 1134/3, 10005 selectors 0/1, 10010 admission actions, 10010/146,
1151/2 and 10010/145), and one project-defined registration case. The five
later groups pin eighteen individual frames from separately retained
running-client captures.
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

The test-only corpus includes the exact fifteen client-emitted logical frames from
`20_notes/Network/evidence/cp32-world-entry-2026-11-08/capture.json` and
`20_notes/Network/evidence/cp32-correction-2026-11-08/capture.json`:
`10005-walk.observed.json` (selector 1 and 0),
`10010-world-entry.observed.json` (ten ordered actions including 96 and 251),
and `10010-correction.observed.json` (same-tile, changed-tile and repeated
146 replies). Every sample pins its originating record, full bytes and SHA-256;
the fixture identifies the original executable, direction and decryption boundary.

The client tests its current action and reply serializers against the observed
frames and its walk reader against both captured packed directions. The walk
writer only accepts a normalized direction; it does not reproduce the
additional packed-direction bits in these records and has no active gameplay
caller. Do not use these read-side tests as proof that CP4 movement sending
is implemented. The server tests decode and byte-preserving serialization,
not an original-server emission. No runtime traffic or live session is
established by these isolated fixture checks.

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
