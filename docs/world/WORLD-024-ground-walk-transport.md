# WORLD-024: Ground-walk request and acknowledgement transport

The admitted GameServer connection has one secured inbound reader and one
outbound writer. Both use the same socket and independently negotiated
directional cipher states. The world-write lane accepts one in-flight write;
a second concurrent write is rejected instead of building an unbounded
outbound queue. A failed or canceled attempted write terminates the session.
Shutdown waits for active reads and writes before releasing the connection
cipher.

An ordinary ground-walk request is packet 10005, 24 bytes. The sender
provides the authenticated entity ID, a direction index from 0 to 7,
a movement tick, and the map-state value. The request uses selector zero.
The direction order is S, SW, W, NW, N, NE, E, SE. Selector nine
identifies the separate 24-direction movement table and is not emitted by
this path.

An accepted local walk echo advances the client's known tile by one delta
using the same direction table, before map projection. Refused movement
has no walk echo; position correction action 108 remains authoritative.
Remote-player walks and mounted movement are identified but not yet applied.
The initial camera uses the committed local tile, without claiming motion
interpolation or avatar action transitions.

Movement-command generation, per-action pacing, pointer-to-world selection,
collision-aware path traversal, local animation/prediction, and remote role
presentation remain outside this slice. Their integration must preserve
the source command-staging lifecycle rather than issuing packets directly
from pointer callbacks. The writer exists for that verified command stage;
it is not wired to user input in this change.
