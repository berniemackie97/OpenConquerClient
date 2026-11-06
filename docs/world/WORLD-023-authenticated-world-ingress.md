# WORLD-023: Authenticated world ingress

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
state is retained but no visual weather component is claimed. Other packet types are diagnosed once per type/action. Local attribute 10017, ground-walk acknowledgement 10005, local item
create/update 1008 and equipment remove/equip/unequip/durability/snapshot
1009 are consumed in the authenticated world. Remote entity spawn/movement,
other item mutation variants, combat and world transfers remain unimplemented. Movement input must preserve the existing serialized
secured-write lane and single-reader contract.

The EnterMap acknowledgement seeds the local hero's eight-way direction
from its packed direction field, independently of the tile coordinates.
Ordinary local walk acknowledgements advance both the tile and the facing.
The scene's character transform uses `(-1 - directionIndex) * 45` degrees;
remote walk packets do not update the authenticated hero. World position
corrections update the tile without replacing the current facing. Initial
facing and accepted updates are retained while the map is being prepared,
then applied before the first world draw. This does not implement outbound
movement requests, motion transitions or remote character presentation.

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

Transport framing (4..0x400 bytes) is unchanged. Walk destination range and
map bounds remain CP2.4c; the world-entry bootstrap sequence remains CP2.4d. Evidence:
`20_notes/Network/inbound-tolerance-cp24.md`.

## Handler tolerance — CP2.4b

Action 108 targeting a non-hero is ignored. For the hero it reads X/Y at
packet +24/+26 and leaves facing unchanged; other action fields are ignored
(`CMsgAction__Process`, `0x6FFCBF..0x6FFCF3`). These are not generic reserved
fields. The static-disassembly-derived all-fields sentinel frame now exercises
the actual hero tile behavior. Native additionally sends reply 146 at
`0x6FFD80`; that missing outbound feature is tracked in issue #263.

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

