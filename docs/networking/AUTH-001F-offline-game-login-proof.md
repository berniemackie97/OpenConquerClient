# AUTH-001F — Standard GameServer login proof (offline)

## Audited baseline and sources

- Client `main` `6c66162228018bc1e3041591c12ac4e71c527c41` following merged AUTH-001E, PR #119. PRs #120–122 additionally changed launcher and documentation; those changes remain untouched.
- `20_notes/Network/13_connection_lifecycle.md`, especially `CConnectionFlow__SendGameSessionBootstrap` (`0x6DF1D0`) and native `CMsgLoginBootstrap1052__BuildGameLoginProof` (`0x75443E`).
- `20_notes/Network/14_session_derived_legacy_cipher_mask.md`, `0x75443E` and `0x7545FC`: the post-DH game proof forwards the AccountServer UID and authentication key **unchanged**, without the separately derived legacy mask.
- OpenConquerServer `GameLoginProof.TryParse` and `GameConnectionAuthenticator`: the first secured GameServer frame must be a valid 1052 proof and its UID/key must redeem a single-use ticket.

## Verified ordinary native 5517 packet

On the standard (non-startup-movie/Facebook) path, **only after GameServer Diffie–Hellman completes**, the client sends this **28-byte** packet to the GameServer:

| Offset | Size | Field |
| --- | --- | --- |
| 0 | 2 | Little-endian frame length 28 |
| 2 | 2 | Little-endian packet type 1052 |
| 4 | 4 | The 1055 session UID, unmodified |
| 8 | 4 | The 1055 authentication key, unmodified |
| 12 | 2 | Native mode `0x007C` |
| 14 | 2 | Opaque locale tag, obtained from the native data-set lookup |
| 16 | 6 | Six hardware-address bytes, or zero when unavailable |
| 22 | 2 | Reserved zero bytes (from the cleared native packet buffer) |
| 24 | 4 | Signed `res.dat` resource version |

**Do not confuse this packet with the 28-byte AccountServer resource-version
report (also type 1052).** They have different layouts and occur on different
connections. The client’s other native 96-byte packet 1203 belongs to a distinct
startup variant and is excluded here.

## Type ownership and lifecycle

`GameLoginProof1052` is a pure serializer with exact-length and hardware-byte
validation before mutation. An empty hardware span produces six zero bytes.
Locale tag and resource version are explicit caller-owned input values; no
machine identifiers, localization data or file content are discovered by this
codec. Zero is a valid authentication-key byte pattern; no undocumented
nonzero-key requirement is introduced.

The existing `LoginAccountExchangeResult` owns the AccountServer success outcome.
It now records report completion only **after** its existing native 1100/1052
continuation finishes successfully, including closing the AccountServer
socket. `WriteGameLoginProof` can then compose the next proof **exactly once**.
It fails before reports, after disposal, on an account failure, and on
repeated invocation. A bad output buffer does not consume the proof issuance.
The narrow lifetime lock prevents a pending report completion from authorizing
proof composition after the result was disposed.

The 28-byte output is sensitive: it contains a redeemable UID/key ticket.
It remains caller-owned and must be zeroed when eventually sent or discarded.
The returned fields must not be logged, serialized into diagnostic messages,
cached, or exposed to UI layers. There is no new method that opens a GameServer
socket, bypasses signed realm authority, or considers the player logged in.

## Security and subsequent dependency

The GameServer response IP and port in 1055 originated on the existing
legacy-obfuscated, unauthenticated AccountServer transport. The signed realm
catalog authenticates account endpoint *metadata*, not the GameServer peer
or 1055 redirect. This proof serializer **does not authorize connecting to
that redirect** and does not make the legacy transport secure.

Before any real GameServer handoff, independently audit and implement:
- authenticated endpoint trust/selection policy and bounded connection lifetime;
- server-initiated DH challenge format, validation, and client response;
- native fixed CAST5 bootstrap and per-session cipher transition;
- proof transmission only once over the correctly established game channel;
- ticket-redeemed admission and all failure, cancellation and disposal paths.

The native `0x7C` mode constant is **not** the transport encryption mode.
No speculative GameServer networking, retry infrastructure, OS MAC discovery,
UI login action, or startup variant logic is included in this slice.

## Verification

The independent OpenConquerServer parser fixture validates the exact serialized
hex and all offsets. Added tests cover the zero-hardware fallback, signed
resource-version field, invalid input atomicity, the real AUTH-001E TCP
post-report lifecycle, disposal, account failure and single issuance.
Existing AUTH-001A through AUTH-001E vectors and cross-platform networking
tests must remain green.

Acceptance requires locked restore, format verification, Release build and
tests, content-set verification, Quality/macOS/Windows, Dependency Review,
reviewer findings and explicit human merge acceptance.
