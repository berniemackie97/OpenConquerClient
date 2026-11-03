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
state is retained but no visual weather component is claimed. Other
world packet types are diagnosed once per type/action and are not yet
applied. In particular, remote spawn/movement, item and status updates,
world transfers, and the local movement acknowledgement path remain
unimplemented. The subsequent movement slice must establish a separate
serialized secured-write lane while preserving the single-reader contract.

The ingress task is canceled and observed before its session is released
on world exit or application shutdown. Bootstrap timeout policies do not
become a world-idle disconnect timer.
