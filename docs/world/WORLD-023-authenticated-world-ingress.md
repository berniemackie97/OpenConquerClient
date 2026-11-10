# WORLD-023: Authenticated world ingress

## Evidence and scope

Packet identities and dispatch are documented in `20_notes/Network/00_protocol_overview.md`, `typed-world-dispatch-cp23.md` and the CP2.4 ingress notes. Bootstrap, tolerance and off-map behavior have separate traces. Queue bounds, cancellation and diagnostics are application decisions; unsupported handlers remain out of scope.

World entry completes its ordered request/acknowledgement exchange before
the client switches the secured connection into world-receive mode. The
authenticated socket and cipher remain owned by the existing session; no
new socket, decoder, or independently advancing cipher is introduced.

The world frame owns one pending secured read. A completed frame is applied
on the render thread before map preparation and presentation, with a
32-frame per-tick processing limit. No background queue accumulates inbound
frames. The pending read has no arbitrary idle timeout; cancellation and
connection closure terminate it. Frames are disposed after application.

The initial application boundary handles position correction (10010 action
108) for the authenticated hero and stores subsequent weather updates
(1016). Position corrections update the map camera after the world scene
is present, including corrections received during map loading. Weather
state is retained but no visual weather component is claimed. Other packet types are diagnosed once per type/action, up to 64 distinct pairs per world. Local attribute 10017, ground-walk acknowledgement 10005, local item
create/update 1008 and equipment remove/equip/unequip/durability/snapshot
1009 are consumed in the authenticated world. Other item mutation variants, combat and world transfers remain unimplemented.
Remote spawn, movement and removal update a bounded peer registry; admitted
frame draining applies those changes to the map scene independently of graphics
preparation. Movement input must preserve the existing serialized secured-write
lane and single-reader contract.

The EnterMap acknowledgement seeds the local hero's eight-way direction
from its packed direction field, independently of the tile coordinates.
Ordinary local walk acknowledgements advance both the tile and the facing.
The scene's character transform uses `(-1 - directionIndex) * 45` degrees;
remote walk packets do not update the authenticated hero. World position
corrections update the tile without replacing the current facing. Initial
facing and accepted updates are retained while the map is being prepared,
then applied before the first world draw. This does not implement outbound movement requests or local input selection.

## Ground-jump acknowledgements — CP4.3a

World action 10010/137 with flag 0 carries the absolute destination in the
low/high words of parameter +8; +24/+26 identify the request origin.
An action addressed to the authenticated hero updates its authoritative tile
without replacing facing. A known foreign player moves to the destination
in the bounded remote-role registry, retaining appearance, identity and
facing; the scene revision updates before graphics preparation. Existing
ground-walk playback is stopped rather than drawn at the landing tile.
An unknown eligible player triggers the existing deduplicated 10010/102
identity query.

The twelve hash-checked `10010-ground-jump.observed.json` frames are
recorded **client-to-server** requests. They test the receive path only
as controlled inputs; they are not evidence of original-server output.
This boundary does not add local jump commands, input targeting, collision
or timing policy, trajectory/pose playback, mounted charge or end-to-end
visual acceptance. Those remain under #251 and the separate evidence gates.

## Ground-jump request writer — CP4.3b

The action-specific 10010/137 ground writer sets the packed absolute target,
origin tile, map identity, packet tick and fixed all-ones field, with the
ground flag zero. All twelve retained source-client ground requests match
the writer byte-for-byte, including the distinct origin and target pairs.
An admitted session can send that frame through its existing secured world
write guard. A controlled loopback exercises the same connection across a
walk, jump and heartbeat/report and verifies the exact decrypted jump
layout with a synthetic hero identity.

This does not provide a world input caller, click/hold semantics, packet
cadence, independent original-server output, or proof of a live jump
interaction. The command producer remains gated by #341. There is no
second send queue, socket, cipher or movement timing policy.

The ingress task is canceled and observed before its session is released
on world exit or application shutdown. Bootstrap timeout policies do not
become a world-idle disconnect timer.

## Message tolerance — CP2.4a

Each world message is loaded into a zeroed 0x400-byte buffer before decoding,
as native `CNetMsg` objects are (`CNetMsg__ResetPacketBuffer` 0x6DF290). None
of the native parsers for 10005, 10010, 1008, 1009, 1016 or 10017 checks
length, so a short message decodes with zero fields past its end and extra
bytes are ignored. A message native dispatch drops before Parse (size above
0x400, size word not equal to the byte count, type 0) is skipped without
closing the world; native skips it and continues (0x6DEC3A). Walk entity 0 is
decoded and handled as a non-hero walk.

Variable counts follow native: 10017 uses the signed count at +8 and 1009
subtype 5 the unsigned count at +20, with entries past the message read as
zero. Native reads past its buffer for counts above 50 (10017) and 235
(1009 list); the client stops at the buffer edge.

Transport framing (4..0x400 bytes) is unchanged. Signed/off-map tiles are handled
by CP2.4c below; the world-entry bootstrap is CP2.4d below. Evidence:
`20_notes/Network/inbound-tolerance-cp24.md`.

## Handler tolerance — CP2.4b

Action 108 targeting a non-hero is ignored. For the hero it reads X/Y at
packet +24/+26 and leaves facing unchanged; other action fields are ignored
(`CMsgAction__Process`, `0x6FFCBF..0x6FFCF3`). These are not generic reserved
fields. The static-disassembly-derived all-fields sentinel frame now exercises
the actual hero tile behavior. The admitted client now queues a 38-byte
action-146 acknowledgement on the single secured world write lane for each
applied hero correction, including same-tile and repeated 108s. Foreign
targets are ignored. The reply uses the corrected tile, current hero facing,
map ID and a fresh client millisecond tick; a bounded FIFO coordinates
corrections with periodic heartbeats. The original-client probe is recorded
in [#304](https://github.com/berniemackie97/OpenConquerClient/issues/304),
while [#263](https://github.com/berniemackie97/OpenConquerClient/issues/263)
retains the real-device integration and owner merge gates.

Weather inputs native rejects are ignored without changing prior weather or
stopping subsequent packets. The gate is signed kind 1..11 except 6,
intensity 0..1022, direction 0..359 (`0x7606CD..0x760711`); kind 6's table
entry targets rejection `0x7607A9`. Accepted values remain the original wire
tuple here. Native rendering-parameter normalization and weather effects are
not implemented by this state-retention boundary.

Invalid item identities, missing item-table rows, duplicate inventory adds and
ordinary inventory capacity refusals likewise skip the item rather than closing
the world. Item ownership, update and arrow rules are detailed in WORLD-026.

SDK 10.0.403 validation: locked restore, Release build with zero warnings/errors,
clean formatting, and 2,919 focused tests (Client 1,584, Content 750, Rendering 499,
Platform 86). Thirty-seven client cases are new. Tests include independent static
golden inputs, skipped-packet continuation, missing-table updates and item-instance
references. They do not establish running-client or native-runtime comparison.

## Signed and off-map tiles — CP2.4c

World state keeps signed int32 tile coordinates. Ordinary walk acknowledgements
add the native DWORD direction deltas with wrapping arithmetic (`0x7406DB/6DD`),
including negative tiles and values above 65535. Correction X/Y remain unsigned
wire words; a later correction replaces the signed model coordinates normally.
Remote-role walking is tracked separately, and hero facing behavior is unchanged.

Neither ClientWorld nor WorldMapScene rejects a hero solely for leaving the loaded
cell grid. The actual hero anchor remains unclamped; the camera retains its existing
ground-extents clamp. Native cell lookup returns NULL outside the grid (`0x61D9C8`),
but this is not an admission rule for a role. Native role registration instead uses
inclusive projected-screen bounds with margin2 (`0x67274A..0x672772`, `0x61E00D`).

Visible off-map roles enter the ordinary scenery/terrain depth ordering. Far
off-screen roles have no draw entry, while scenery preparation/drawing continues.
Projection and depth arithmetic preserve native int32 wrapping; WORLD-020 records
the rendering and height boundary. These changes introduce no network lane, worker,
content I/O or resource lifetime. World-exit ownership and bootstrap behavior stay
unchanged. Evidence: `20_notes/Network/off-map-tiles-cp24c.md`.

CP2.4c local validation on SDK10.0.403: locked restore, zero-warning/error Release
build, format verification, and 2,933 focused tests (Client1,598, +14; Content750;
Rendering499; Platform86). The 27 native-derived golden inputs cover signed
projection, visibility and depth. Tests also exercise u16 crossings, subsequent
corrections, off-map scene anchors and return to an ordinary tile. Running-client
smoke and native-runtime comparisons remain unverified.

## World-entry bootstrap decoders — CP2.4d1

World entry decodes 1110 map information (`GameMapInfo1110`), 1016 weather and
10010 acknowledgements through the same zeroed 0x400-byte buffer as world
ingress. The native parsers check only the header size, nonzero type and the
type word: 1110 `CMsgMapInfo1110__Parse` 0x732AED (buffer reset in the
constructor at 0x732AA9), 1016 0x7607DE and 10010 0x6FEB93. A short frame
reads zero past its end, extra bytes are ignored, and a nonzero +37 string
count no longer rejects an acknowledgement. 1110 fields are +4 map identity,
+8 map document and +12/+16 flags (`CMsgMapInfo1110__Process`
0x732B6F/0x732B84/0x732B99).

The bootstrap's ordering, entity, parameter, map-identity and weather-kind
checks, hydration budgets, deadlines and activation point are unchanged. Native
has no rung-order gate; those checks are CP2.4d2. Evidence:
`20_notes/Network/bootstrap-tolerance-cp24d.md`.

## World-entry outbound frames — CP2.4d2

The bootstrap sends what the native client sends around EnterMap, proven
statically and in a 2026-11-06 retail-client probe against OCS:

- After the 74 request, `1151` subtype 2 (80 bytes, parameters zero) when the
  hero's body digit (appearance / 1000 % 10) is 2 (`CMsgUserInfo__Process`
  0x7648B4..0x7648E6).
- Action 408 only when the 1110 map flags have bit 38. Native sends it from
  `CMsgMapInfo1110__Process` when bit 38 changes (0x732CFD/0x732D4C), and map
  flags start at zero (`CGameMap` constructor 0x61B875). Each OCC world entry
  starts from zero; a native process that re-logs keeps the previous map's
  flags, which OCC does not model.
- Action 114 for the hero after the 74 reply and before 75: native map
  creation always closes the booth (`CGameMap_CreateFromDataId` 0x61DB1E →
  `CBoothManager_Close` 0x70AC3D).

The previous unconditional 408 after the 74 reply was not native and is
removed. Evidence: `20_notes/Network/bootstrap-tolerance-cp24d.md`.

## World-entry ladder without order checks — CP2.4d2b

The 5517 client answers each server acknowledgement as it arrives; nothing
else orders the ladder. The bootstrap now does the same:

- `CMsgAction__Process` reads only the entity and action of an
  acknowledgement (common entity gate 0x6FF15D..0x6FF18A). An acknowledgement
  for another entity is ignored, its parameter is not read, and each accepted
  acknowledgement sends the next request again if repeated.
- The 74 reply's entity is replaced by the hero (0x6FEE14) and its +8 is the
  map key the client loads (0x6FF478), independent of 1110. 1110 and 1016 are
  applied whenever they arrive; an unsupported weather value is a no-op.
- After the 76 request the client sends PK mode 96 with mode 3 (75-ack
  handler posts main message 0x401, 0x6FF3CD → 0x4BDD30).
- Requests built by `CMsgAction__Create` carry `timeGetTime` at +16
  (0x6FE99C), taken from the injected `TimeProvider`. 408 and the 251 report
  keep their own fields.
- Any other frame is kept, in arrival order and within the hydration budget,
  and the world state replays all of them through its normal handlers. Frames
  that arrived before the 74 reply apply before its position.

The world still needs a 1110 before activation; that is this client's
requirement, not a native one.

## Heartbeat, latency report and silent-info report — CP2.4d2b

- **Heartbeat.** The 74 reply starts a 10-second timer (0x6FF57A..0x6FF587).
  Each frame, `CHero_TickHeartbeatAndPeriodicSessionSync` (0x661ADD) checks
  it with `CTimerMS_ToNextTime` (0x6B9D8B): the timer is active while its
  start is nonzero, times out when the millisecond counter reaches start +
  10000 (unsigned against the wrapped sum, 0x6B9CAA) and restarts from the
  check time, so a late check does not catch up. On each timeout the client
  sends 1009 subtype 27: 88 bytes, +4 hero, +16 counter XOR hero
  (`CMsgTick__Build_SubType` 0x6EAC51). `WorldHeartbeat` holds this rule and
  `WorldFrameEgress` writes it on the session's single outbound lane; a due
  timer waits while an earlier write is still in flight, and a failed write
  closes the world as a lost connection.
- **Latency.** A server 1009 subtype 27 sets the latency to counter − its
  +16, as `CMsgItem__Process` does for `hero+0x2CF4`.
- **1037 report.** On a heartbeat at least 30 minutes after the client
  started (hero construction, 0x655BAD) or after the last report window, the
  client adds a 1037: 20 bytes, +8 local IPv4, +12 latency, +16 counter
  (0x743FED). The window restarts either way, and no 1037 is sent while the
  address is 0. The address is resolved like native `sub_66df97` when the
  account server is contacted: the host name's last IPv4 address, as its raw
  in_addr dword; a failed lookup keeps the previous value. This sends the
  machine's LAN address to the server, as the native client does (product
  decision 2026-11-07).
- **251 report.** +8 is the checksum of `ini/Slient.dat` (native spelling)
  under the client content root: `r = ((r << 8) | b) ^ T[r >> 24]` with the
  reflected CRC-32 table (0x6BB1E2, table 0x9432D8). +16 is `atoi` of the
  first 128 bytes of `ini/SlientVersion.ini`. A file that cannot be opened
  keeps native's default, checksum 0 and version 1, which is also what the
  clean install produces (empty `Slient.dat`, no version file).

The millisecond counter is the client clock (`GameClientClock`) from the
injected `TimeProvider`; native uses `timeGetTime`. Evidence:
`20_notes/Network/bootstrap-tolerance-cp24d.md` (d2b slice 2).

