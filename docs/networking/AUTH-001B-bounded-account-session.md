# AUTH-001B: AccountServer seed session

AUTH-001B opens the AccountServer connection and receives the seed packet 1059.
It builds on the AUTH-001A codecs and sends nothing.

## Sources

- `20_notes/Architecture/03_login_flash_startup_command_contract.md`: in the
  retail client, AccountServer legacy mode 1 uses directional stream counters
  and no `TQClient` trailer.
- `20_notes/Network/5517-server-selection-and-realm-handoff.md`: the retail
  AccountServer connect timeout is 10 seconds. The legacy Server.dat/BGP
  fallback is not a trusted source of endpoints for this client.
- `OpenConquerServer` `LoginConnectionSession.OpenAsync`: sends encrypted packet
  1059 as soon as the connection opens.
- The existing `LoginSeed1059`, `LoginDecodedFrameReader` and
  `LoginLegacyStreamCipher` define the seed format, the handling of malformed
  frames and the cipher transform.

## Design

`LoginSignedAccountSession.OpenAsync` is the client's entry point, and it is
where trust is checked. It requires a currently valid `SignedLoginCatalog`, a
presentation selection whose identity matches that catalog, and an endpoint
taken from the verified catalog. It never accepts a host from Server.dat, from
the UI label or from a 1055 response.

`LoginAccountSession.OpenAsync` is a lower-level internal transport primitive.
It does not authenticate the endpoints callers pass to it, so trusted code must
go through the signed selection gateway.

One session owns one IPv4 TCP connection and one client-side legacy stream
cipher. Opening has two phases with separate time limits: connecting (10 seconds
by default) and receiving the seed (5 seconds by default). Each limit must be
positive and at most 30 seconds. Both phases honor caller cancellation. Opening
consumes the one 1059 frame that the server sends first, and returns its seed
together with the still-open socket. It never writes to the socket.

The seed reader owns a 64-byte buffer. It reads the decoded four-byte header,
checks the frame bounds, then reads the advertised body length and nothing more.
Each TCP read advances only the receive cipher position, by the number of bytes
actually read. The reader cannot read ahead into the next frame. Fragments are
collected only up to the validated 1059 length, and the longest valid seed
packet is 64 bytes. On a malformed frame or type, EOF, a connect failure, a
timeout or cancellation, the connection is disposed before the error propagates.
Dispose is idempotent.

The cipher's client-outbound counter starts at zero and is not touched. The
inbound counter advances by the bytes consumed and stays with the session for
the later credential phase. The session starts no background tasks or channels,
never retries or reconnects on its own, and owns every socket it uses.

## Security and scope

The signed catalog authenticates the endpoint metadata. It says nothing about
the live TCP peer. The legacy XOR/nibble obfuscation provides no
confidentiality, no integrity and no server authentication. Receiving a valid
seed does not mean that authentication succeeded.

The session has no public or internal method for sending credentials, and it
cannot produce or interpret 1055 authentication results. AUTH-001B adds no UI
behavior, realm status, GameServer endpoint handling or background polling.
Before AUTH-001C sends a request, the calling application must check again that
the catalog is still fresh, that the identity still matches and that the session
is in the right state. Holding a seed does not by itself permit sending
credentials.

## Testing

Tests run against a real loopback socket. They cover fragmented 1059 delivery,
rejection of malformed lengths and types, premature EOF, timeout, caller
cancellation, socket disposal, argument validation, rejection of unsigned,
expired and mismatched selections, and successful routing from a catalog fixture
that carries a real ECDSA signature. The AUTH-001A tests continue to cover the
5517 cipher vectors and 16-bit rollover.

The change passes the standard gate: locked restore, formatting, Release build,
the full test suite, retail content verification, and the macOS, Windows,
Quality and Dependency CI jobs.
