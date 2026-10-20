# AUTH-001F: Standard GameServer login proof (offline)

AUTH-001F serializes the 28-byte login proof (type 1052) that the client sends
as its first secured packet to the GameServer. It only builds the packet.
Nothing in it connects to a GameServer.

## Sources

- `20_notes/Network/13_connection_lifecycle.md`, especially
  `CConnectionFlow__SendGameSessionBootstrap` (`0x6DF1D0`) and the retail
  `CMsgLoginBootstrap1052__BuildGameLoginProof` (`0x75443E`).
- `20_notes/Network/14_session_derived_legacy_cipher_mask.md`, `0x75443E` and
  `0x7545FC`: the game proof sent after DH forwards the AccountServer UID and
  authentication key unchanged, without the separately derived legacy mask.
- OpenConquerServer `GameLoginProof.TryParse` and `GameConnectionAuthenticator`:
  the first secured GameServer frame must be a valid 1052 proof, and its UID and
  key must redeem a single-use ticket.

## The 5517 login proof packet

On the standard path (not the startup-movie/Facebook variant), the client sends
this 28-byte packet to the GameServer, and only after the GameServer
Diffie-Hellman exchange has completed:

| Offset | Size | Field |
| --- | --- | --- |
| 0 | 2 | Little-endian frame length 28 |
| 2 | 2 | Little-endian packet type 1052 |
| 4 | 4 | The 1055 session UID, unmodified |
| 8 | 4 | The 1055 authentication key, unmodified |
| 12 | 2 | Mode `0x007C` (retail constant) |
| 14 | 2 | Opaque locale tag, from the retail client's data-set lookup |
| 16 | 6 | Six hardware-address bytes, or zero when unavailable |
| 22 | 2 | Reserved zero bytes (the retail client clears the packet buffer) |
| 24 | 4 | Signed `res.dat` resource version |

The 28-byte AccountServer resource-version report from AUTH-001E is also
type 1052. The two packets have different layouts and travel on different
connections. The retail client's 96-byte packet 1203 belongs to a different
startup variant and is not covered here.

## Types and lifetime

`GameLoginProof1052` is a pure serializer. It checks the output length and the
hardware bytes before writing anything. An empty hardware span produces six zero
bytes. The caller passes in the locale tag and the resource version, and the
codec does not discover machine identifiers, localization data or file content.
The six-byte hardware staging area is zeroed before copying, which keeps the
empty-hardware fallback correct and makes overlapping spans safe. An
authentication key of all zero bytes is valid, and the serializer does not
reject it.

The existing `LoginAccountExchangeResult` owns the successful AccountServer
outcome. It marks reporting as complete only after its 1100/1052 continuation
has finished successfully, including closing the AccountServer socket. The
result keeps the resource version it reported to the AccountServer and reuses it
in the GameServer proof, so callers cannot pass a second, different version.
`WriteGameLoginProof` can then compose the proof once. It fails before the
reports are done, after disposal, after an account failure and on a second call.
A bad output buffer does not use up the single issuance. A narrow lifetime lock
stops a report completion that is still pending from enabling proof composition
after the result has been disposed.

The 28-byte output is sensitive because it contains a redeemable UID/key ticket.
It belongs to the caller, who must zero it once it has been sent or discarded.
Its fields must not be logged, written into diagnostic messages, cached or
passed to UI layers. No new method opens a GameServer socket, bypasses the
signed realm catalog or treats the player as logged in.

## Security and remaining work

The GameServer IP and port in 1055 arrive over the existing AccountServer
transport, which is legacy-obfuscated and unauthenticated. The signed realm
catalog authenticates the account endpoint metadata, but not the GameServer peer
or the 1055 redirect. This serializer does not make it safe to connect to that
redirect, and it does not make the legacy transport secure.

Before any real GameServer handoff, each of these needs its own research and
implementation:

- an authenticated policy for trusting and selecting endpoints, and a connection
  lifetime with time limits
- the format of the server-initiated DH challenge, its validation and the client
  response
- the 5517 fixed-key CAST5 bootstrap and the switch to the per-session cipher
- sending the proof only once, over a correctly established game channel
- admission after ticket redemption, and all failure, cancellation and disposal
  paths

The retail mode constant `0x7C` has nothing to do with the transport encryption
mode. AUTH-001F adds no GameServer networking, retry logic, OS MAC discovery, UI
login action or startup-variant logic.

## Testing

The independent OpenConquerServer parser fixture checks the serialized hex and
every offset. Tests cover the zero-hardware fallback, the signed
resource-version field, atomic rejection of invalid input, the real AUTH-001E
TCP lifecycle after the reports, disposal, account failure and single issuance.
The existing AUTH-001A through AUTH-001E vectors and the cross-platform
networking tests still pass.

The change passes locked restore, format verification, Release build and tests,
content-set verification, and the Quality, macOS, Windows and Dependency Review
jobs.
