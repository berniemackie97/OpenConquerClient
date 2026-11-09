# CP3.2 — Active packet routes and evidence coverage

[Coverage task #316](https://github.com/berniemackie97/OpenConquerClient/issues/316) |
[Corpus task #248](https://github.com/berniemackie97/OpenConquerClient/issues/248) |
[Named-codec inventory](wire-evidence-inventory.md)

## Revision and boundary

- Client: `d0647996830c057a42c52400f2ffe30872f9227b`.
- Server: `4eee1677d34330e3db0cc67499dd54f2db24ee6b`.
- The account route audit is retained at `20_notes/Network/evidence/cp32-bootstrap-paths-2026-11-08/README.md` (#301).
- The world route audit is retained at `20_notes/Network/evidence/cp32-world-paths-2026-11-08/README.md` (#302). This review rechecks active paths and changes since those revisions; it does not reproduce their instruction-level analysis.
- Original-client logical frames are retained under `20_notes/Network/evidence/cp32-world-entry-2026-11-08/` and `cp32-correction-2026-11-08/`. A controlled peer's input or echo is not an original-server emission.
- The client account write owner is `LoginAccountSession`; its login receive boundary is `LoginDecodedFrameReader`. The server's account write/read owners are `LoginFrameWriter` and `LoginRequestReader`/`LoginPostAuthenticationReportReader`. Game traffic uses the client's `GameServerHandshakeSession.WriteSecuredPacketAsync` / `ReadFrameCoreAsync` and the server's `GameSecuredFrameWriter` / `GameConnectionWorkerPool`; the DH exchange has a separate bootstrap boundary.

**Evidence key:** **O** = exact observed original-client outgoing logical frame; **P** = controlled-peer input or echo observed at the running-client boundary; **M** = metadata retained without secret-bearing bytes; **D** = source/static-derived evidence; **X** = project-specified extension; **S** = production source path only. **Shared** means a manifest-pinned fixture in both test suites, not merely a passing local synthetic test. All secured-game logical frame references exclude the eight-byte transport signature.

## Account and entry routes

| Wire identity and direction | Active client route | Active server route | Evidence / shared fixture | Remaining gate |
| --- | --- | --- | --- | --- |
| Account 1059 S→C; 1060 C→S | `LoginAccountSession.ReadSeedAsync`, `StandardAccountCredentialComposer.Write` | `LoginConnectionSession.SendInitialSeedAsync`; `LoginRequestReader` → `LoginAuthenticationHandler` | M; no shared 5517 frame | Sensitive credential bytes were not retained; no independent public byte fixture |
| Account 1055 S→C | `LoginAccountSession` authenticates then reads response | `LoginAuthenticationHandler` and `LoginHandshakeProcessor` success/refusal | M success; D refusal; none | No observed refusal family or public original-response fixture |
| Account 1100 then account 1052 C→S | `LoginAccountSession.SendPostAuthenticationReportsAsync` | `LoginPostAuthenticationReportReader` after accepted login | M; none | Device/session identifiers withheld; independent safe vectors needed |
| Account F001 C→S / F002 S→C | `LoginRegistrationCoordinator` → `LoginAccountSession.RegisterAsync` | `LoginRequestReader` → `AccountRegistrationHandler`; invalid request in `LoginHandshakeProcessor` | X; **Shared** `F001-F002-registration.project-defined.json` | No original-client frame is possible for these project extensions; keep the specification classification |
| Account 1084 / 1098 C→S (unsupported) | No producer | `LoginHandshakeProcessor` emits 1055 refusal | S; none | Unsupported ingress must not be counted as accepted client behavior |
| Game DH challenge/response, no packet ID | `GameServerHandshakeSession.OpenAsync` → `GameClientDhExchange` | `GameTransportHandshakeProcessor` and `GameClientKeyExchangeResponseReader` | M; no keys recorded | Crypto/framing agreement tests are not a substitute for safe executable evidence |
| Game 1052 C→S (initial and repeated) | `SendLoginProofAsync`; `RoleCreationExchange` → `SendRepeatedLoginProofAsync` | `GameConnectionAuthenticator`; `GameCharacterCreationLoginProcessor` | M initial; S repeated; none | Repeated-proof path and secret-free test evidence outstanding |
| Game 1004 S→C, channel 2101 | `ReadLoginControlAsync` handles success, NEW_ROLE and refusal | `GameLoginConnectionProcessor`, `ExistingCharacterBootstrapProcessor`, `GameCharacterCreationControlPackets` | P success; S variants; none | Character-creation/refusal variants not observed in retained session |
| Game 1001 C→S; 1004 S→C, channel 2100 | `RoleCreationExchange` create/cancel/read result | `GameCharacterCreationLoginProcessor` validates request and retries; `GameCharacterCreationControlPackets` replies | S; local tests, no shared frame | Exact create, cancel and refusal captures are missing; session/device fields must not be published |
| Game 2078 → 2079 → 1006 S→C | `ReadExistingCharacterPreludeAsync` then `ReadCharacterInfoAsync` in both login routes | `ExistingCharacterBootstrapProcessor` / `ExistingCharacterBootstrapPacketFactory` | P; blank 2078 disclosed; no shared fixture | Populated variants and original-server bytes unverified |
| Game 1134/3 C→S | `LoginAttemptCoordinator` and `RoleCreationExchange` → `SendMissionCatalogAsync` after accepted 2101; catalog from `MissionCatalog.Load` | `LoginActionRequest` → `ExistingCharacterMissionCatalogReport` | **O; Shared** `1134-mission-catalog.observed.json`, three ordered frames | Connected admission and creation smoke: [#306](https://github.com/berniemackie97/OpenConquerClient/issues/306); other installed catalogs unobserved |
| Game 10010/74 C→S; 1110, 1016, 10010/74 S→C | `GameWorldEntryBootstrap.RunAsync` sends/receives map-entry sequence | `ExistingCharacterEnterMapProcessor` | O outgoing /74 **Shared**; P responses; D 1016 **Shared** static-derived fixture | Original-server frames not captured; other map conditions unobserved |
| Game 1151/2 C→S; 10010/114 C→S | Conditional body-digit flower-rank and booth-close in `GameWorldEntryBootstrap` | `LoginActionRequest` skips supported non-ladder actions | O both; **Shared** two 1151/2 captures and a 10010/114 action | Other 1151 subtypes remain unverified |
| Game 10010/75, 76, 77, 78, 97, 251, 132 C→S | `GameWorldEntryBootstrap.TryAnswerAsync` and `GameSilentInfoReport` | `LoginActionRequest`; item, friend, skill, magic, syndicate, report and statistics processors | O all in entry traces; 10010 layout **Shared** static-derived only | Ten captured outgoing action variants are **Shared**; replay/shortened peer ACKs do not establish original-server behavior |
| Game 10010/96 C→S; 10010/408 C→S | Bootstrap PK mode 3 and map-flag-38 conditional in `GameWorldEntryBootstrap` | `LoginActionRequest` non-ladder tolerance; world 408 in `GameConnectionWorkerPool` | **O; Shared** /96 and exact /408 in `10010-client-state.observed.json` | /408's flag-zero control is exercised in the admitted client tests; source-server emission remains unobserved |
| Game 1008/1, /3 and 1009/46 S→C | `GameWorldEntryBootstrap` holds hydration, then `WorldInboundState` applies item state | `ExistingCharacterItemSetProcessor` sends item and equipment snapshots | S; local tests, no shared frame | Populated equipment and mutation variants unobserved |
| Game 1019, 1025, 1103, 1104, 1106 S→C | Bootstrap hydration retains frames; `GameWorldPacketDispatcher` does not have typed handlers for these IDs | Friend, skill, magic and syndicate processors | S; none | Populated bootstraps and typed world presentation remain outside verified client behavior |
| Game 1009/27 C→S / S→C | `WorldFrameEgress` → `SendWorldLatencyTickAsync`; `WorldInboundState` updates latency on echo | `LoginActionRequest` and `GameConnectionWorkerPool` echo valid packet bytes | **O; Shared** all 30 captured 1009/27 requests in `1009-27-heartbeat.observed.json`; **P** controlled-peer echoes | Other 1009 variants and original-server echoes remain unverified |

## World runtime routes

The active client receive fan-out is `WorldFrameIngress` → `ClientWorld.ApplyFrame` → `WorldInboundState` → `GameWorldPacketDispatcher`. A message is not considered presented merely because it is decrypted or retained in bounded hydration storage. Server ingress is `GameConnectionWorkerPool.RunSessionAsync` → `WorldSessionGameplay` or `WorldSession.ProcessPortalAsync`; visibility uses `WorldVisibilityService` and the session's serialized send path.

| Wire identity and direction | Active client route | Active server route | Evidence / shared fixture | Remaining gate |
| --- | --- | --- | --- | --- |
| 10005 selectors 0 and 1 C→S / S→C | `WorldInboundState.HandleWalk` applies own hero; `SendWorldWalkAsync` exists but has **no production caller** | `ProcessWalkAsync` commits and echoes/relays through `WorldVisibilityService` | **O; Shared** all 23 original selector-0/1 requests in `10005-walk.observed.json`; **P** controlled-peer echoes | No observed mounted selector 9; local input sender and connected motion remain with [#250](https://github.com/berniemackie97/OpenConquerClient/issues/250) |
| 10005 selector 9 C→S / S→C | `GameWalk10005` parses selector; no mounted sender/presentation | `GameWalkPacket` validates and relays accepted movement | D; none | No mounted original-client frame; do not derive a capture from selector 0/1 |
| 10010/108 S→C; /146 C→S | `HandleAction` applies hero correction; `WorldFrameEgress` queues and sends one /146 per applied correction | `WorldPositionReconciler` and `WorldSessionGameplay.ProcessPositionReportAsync` | P controlled /108; O three /146 replies; **Shared** exact replies; local replay tested | Original /146 output is pinned; connected correction smoke [#263](https://github.com/berniemackie97/OpenConquerClient/issues/263) |
| 10010/100 C→S (idle report) | No active world producer identified | `GamePositionReport` accepts and reconciles; no echo | S; none | Determine producer and exact fields before claiming client support |
| 10010/137 C→S / S→C | No active local jump or remote action presentation | `ProcessJumpAsync` validates, publishes acknowledgement/visibility | **O; Shared** twelve ground requests in `10010-ground-jump.observed.json`; server's exact echo is a managed serializer comparison | Mounted charge and original-server acknowledgement remain unobserved; [#251](https://github.com/berniemackie97/OpenConquerClient/issues/251) owns gameplay |
| 10010/79, /81 C→S / S→C | World action dispatcher reports these as unhandled | `ProcessAvatarActionAsync` publishes accepted facing/pose | S; none | [#252](https://github.com/berniemackie97/OpenConquerClient/issues/252), gated by earlier CP4 work |
| 10010/85 C→S; /86 and 1110 S→C | No active portal sender; bootstrap 1110 parser does not handle subsequent world transfers | `WorldSession.ProcessPortalAsync`, `WorldConnectedPortalTransferCoordinator`, `WorldVisibilityService` | S; none | [#253](https://github.com/berniemackie97/OpenConquerClient/issues/253) transfer and state ownership |
| 10010/102 C→S; 10014 and 10010/135 S→C | No active query or remote-player handlers | `GameConnectionWorkerPool` handles query; `WorldVisibilityService` queues spawn/removal | S; none | [#249](https://github.com/berniemackie97/OpenConquerClient/issues/249) before later peer movement |
| 10010/145 C→S; 10017/11, /25 S→C | No active /145 sender; `HandleAttributes` processes supported local status and leaves look variants unhandled | `ProcessGhostTransitionAsync` emits status/look through `WorldVisibilityService` | O /145 **Shared** exact frame; P /11 and /25 (peer only) | Full local/remote death/ghost presentation [#254](https://github.com/berniemackie97/OpenConquerClient/issues/254) |
| 10010/408 C→S; 1056 C→S | /408 only from entry map flag; no active world /408 or 1056 writer | World-worker 408 validation; `WorldSession.ProcessTradeQuit` accepts authenticated mode | **O; Shared** exact /408 and 1056/2 in `10010-client-state.observed.json` and `1056-trade-quit.observed.json` | Other trade modes, original-server output and post-death client sender remain unverified |
| 1004 channels 2007, 2000, 2002, 2104, 2102 | No active post-activation chat send or typed world receive | `ProcessChatAsync`, `WorldChatPacketFactory`, `WorldVisibilityService` | S; none; 1004/2101 capture is **not** chat coverage | [#255](https://github.com/berniemackie97/OpenConquerClient/issues/255) after CP4 prerequisites |
| 1008 and 1009/3, /5, /6, /17, /46 S→C | `WorldHeroEquipment` receives item and equipment variants | Item/equipment bootstrap writer; no active later mutation writer identified | S; none | Populated lifecycle tests and observed frames; later item ownership [#262](https://github.com/berniemackie97/OpenConquerClient/issues/262), [#264](https://github.com/berniemackie97/OpenConquerClient/issues/264) |
| 10017 S→C (hero fields, including 11/25) | `WorldHeroAttributes` handles supported hero fields; unsupported targets/attributes remain unhandled | World status/look and admission corpse-status writers | P controlled status inputs; D 10017 sentinel in retained research; none shared | Other attributes, high-status word and remote actors unverified |
| 1016 S→C | `WorldInboundState.HandleWeather` accepts supported tuples | `ExistingCharacterEnterMapProcessor` sends clear weather | P valid/invalid controlled inputs; **Shared** static-derived 1016 fixture only | Observed peer weather is not original-server output; broader weather variants unverified |
| 1037 C→S | `WorldFrameEgress` sends after 30-minute/address gate | World worker ignores unsupported report | S; synthetic local tests, none shared | No observed source frame; address material must not enter a public capture |
| 1115 S→C | No active world-specific kickout handler | `WorldSession` can send OtherLogin notice | S; none | PasswordChanged is an unexercised enum branch; owner transition and client close behavior require evidence |

## Coverage reconciliation and next actions

1. The earlier route census described mission-catalog send as missing; the client now loads it on accepted game login for both admission paths. [#306](https://github.com/berniemackie97/OpenConquerClient/issues/306) retains connected admission and post-creation acceptance, not another serializer task.
2. The earlier world audit described correction replies as missing; the client now uses the bounded `WorldFrameEgress` lane for /146. Three original reply frames exist outside the shared corpus. [#263](https://github.com/berniemackie97/OpenConquerClient/issues/263) retains connected smoke, while [#248](https://github.com/berniemackie97/OpenConquerClient/issues/248) owns manifest integration.
3. The earlier heartbeat timer mismatch is fixed, and [client #317](https://github.com/berniemackie97/OpenConquerClient/pull/317) / [server #223](https://github.com/berniemackie97/OpenConquerServer/pull/223) pinned the exact 1009/27 frame. Do not duplicate the closed [#299](https://github.com/berniemackie97/OpenConquerClient/issues/299) fix.
4. The captured 10005 selector-0/1 outbound frames, ten 10010 admission actions and three correction /146 replies are included in the shared test corpus. Original-server output and unobserved selector 9, /408 and /100 remain unverified; no constructed packets were relabeled as captures.
5. Account credentials, login proofs, tokens, device identifiers and local addresses were intentionally withheld. Only safe independently specified/derived vectors may be checked in, explicitly labeled non-observed. Missing original-server evidence is a separate gap from client-output capture coverage.
6. The client has no active production world-walk sender, remote-entity registry, chat producer or portal sender. Their presence in a codec or on the server does not make the client story complete. [CP4.1–CP4.7](https://github.com/berniemackie97/OpenConquerClient/issues/285) remain parked behind [#248](https://github.com/berniemackie97/OpenConquerClient/issues/248).
7. The shared corpus currently has five cases: two static-derived (10010/1016), two observed client outputs (1009/27 and 1134/3), and one project-defined registration extension. There are no independently observed original-server frames in it. Treat all missing message variants as unverified, not implicitly supported.

## Completion boundary

This matrix accounts for the active manually selected action variants, conditional send paths, receive fan-out, and ignored/unsupported branches in the audited account, admission and world flows. Its classifications are evidence bookkeeping, not runtime capture claims. It does not close #248, verify connected gameplay, or authorize promotion of parked releases. Review new packet-producing call sites against this matrix as those features are admitted.

## Conditional entry and ghost-request capture checkpoint

The shared evidence corpus includes `1151-flower-rank.observed.json`
(capture 1 record 21, capture 2 record 20) and
`10010-ghost-transition.observed.json` (capture 2 record 45). These
frames preserve the original-client direction, exact logical payload and
source SHA-256. Server login ignores 1151 by design; the 10010/145 parser
validates the captured client request. They do not establish any source-client
implementation of post-death /145 emission or original-server status output.
The 10010/137 jump and 1056 trade-quit payloads were withheld from the
research capture; their recorded presence is metadata, not a golden vector.

## Post-admission acknowledgement replay

[Protocol bug #321](https://github.com/berniemackie97/OpenConquerClient/issues/321)
covers the authenticated world reader after its admission bootstrap has completed.
The archived world-entry session recorded two separate 10010/75 stimuli after
world activation: capture 1 record 111 was a deliberately shortened, properly
framed 22-byte peer request and record 132 was a complete 38-byte peer request.
The resulting emitted 76, 96, 77, 78, 97, 251 and 132 actions are original-client
observations at records 112–123 and 133–144 respectively. Later heartbeats
continued. The deliberately modified peer frame is not a captured original-client
emission, nor does this establish an original-server replay contract.

The admitted world now uses the same acknowledgement-to-response mapping as
initial bootstrap, with its session's silent-info checksum/version. The
world-state handler accepts both framed lengths via the existing bounded,
zero-filled dispatcher, checks the authenticated hero ID, then queues responses
on the single capped outbound lane alongside correction replies and heartbeats.
It does not restart admission or attach another reader. Failed writes propagate
to the existing connection-loss owner; shutdown drains the active writer.

The source-capture assertions and deterministic world/send-lane tests cover
truncated and full 75 replies, response order, foreign actor rejection,
invalid header rejection, queue limits and send failures. These are managed
regressions, not a new live connected-device acceptance. CP3.2 still requires
missing original-client packet variants and independently established
original-server output where required; connected-client gates #263 and #306
remain separate.

## Pinned replay-response emission windows

The original client's post-admission replies to controlled-peer action 75 are
now a shared observed-output fixture, `10010-world-replay.observed.json`.
Capture 1 emitted seven action frames after each of the shortened and full
peer stimuli (records 111 and 132 respectively). The fixture pins 14 exact
client-to-server responses, their record numbers and independent hashes.
Client and server verify byte-level round trips, and client tests compare
the two response sequences against the shared admission/world mapping.
The 22-byte stimulus remains classified as derived, while the 38-byte
stimulus is controlled-peer input; neither is original-server output.
