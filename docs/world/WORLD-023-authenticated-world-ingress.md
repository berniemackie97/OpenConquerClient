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

Transport framing (4..0x400 bytes) is unchanged. Handler-level rejections
(position-correction field rules, walk destination range, weather ranges,
item identity and capacity, map bounds, missing item-table rows) and the
world-entry bootstrap sequence are scheduled for CP2.4b/c. Evidence:
`20_notes/Network/inbound-tolerance-cp24.md`.

