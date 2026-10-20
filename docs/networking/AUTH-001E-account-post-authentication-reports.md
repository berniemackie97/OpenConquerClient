# AUTH-001E: AccountServer post-authentication reports

After a successful login response 1055, the retail client sends two reports on
the same AccountServer connection and then closes it. AUTH-001E implements those
two packets and the step that sends them.

## Sources

- Retail research:
  `20_notes/Architecture/03_login_flash_startup_command_contract.md`,
  `CMsgAccountAuthResponse__Process` `0x75492C`,
  `CMsgAccountMacAddress1100__Build` `0x767DA1`,
  `CMsgMapInfo1052__BuildSimpleString` `0x7543CA`.
- Confirmed by OpenConquerServer `LoginPostAuthenticationReportReader`,
  `LoginAccountMacAddressReportPacket` and
  `LoginAccountResourceVersionReportPacket`.

## Wire sequence

A successful AccountServer 1055 leaves the same legacy-encrypted account socket
open. The original client then, in order:

1. Builds and sends 1100, 52 bytes:
   `[length:2][type:2][sessionUid:4][macText:40][unknownTrailing:4]`. The retail
   client writes the MAC as 12 uppercase hexadecimal ASCII characters, or leaves
   the field zero-filled when no MAC is available. The meaning of the last four
   bytes is unknown, and this client sets them to zero.
2. Builds and sends the AccountServer form of 1052, 28 bytes:
   `[length:2][type:2][sessionUid:4][resDatVersion:i32][resDat:16]`. The
   resource name is the ASCII string `res.dat`, padded with zero bytes.
3. Closes the AccountServer connection, before any GameServer connection or DH
   logic.

Both packets continue the existing client-to-account stream cipher state,
following the 276-byte type 1060 credential packet. The server applies its
inverse transform. There is no extra seed, cipher reset, reconnect, trailer,
response packet or queue. Packet 1052 depends on context: the account report
above is not the GameServer login proof, even though both can be 28 bytes.

## Types and lifetime

`LoginAccountMacAddressReport1100` and `LoginAccountResourceVersionReport1052`
are pure serializers. They never do I/O or keep references. They validate the
whole destination and the caller's input before writing anything, and pad unused
fields with NUL.

`LoginAccountExchangeResult` is still the only owner of the AccountServer
connection after a successful 1055. It exposes `CompletePostAuthenticationAsync`
instead of direct socket access. The method checks that the session holds the
matching successful 1055 token and atomically claims the single report attempt.
Both reports are validated before anything is written. The two writes share one
deadline, which must be positive and at most 30 seconds, and the caller's
cancellation. A timeout, cancellation, malformed input, an unrecoverable send
exception and normal completion all close the socket and zero the temporary
frames. A second attempt is refused, and an uncertain write is never retried.
After a failed 1055, no reports can be sent. Disposing the exchange result is
idempotent.

The caller supplies the hardware text and the resource version. This code does
not look up MAC addresses, read arbitrary local files or call OS-specific APIs.
An empty MAC is accepted, matching the retail client's behavior when it cannot
find one. A nonempty MAC must be 12 uppercase ASCII hexadecimal bytes. The
resource version is a signed 32-bit integer from trusted local content metadata.
This code never makes one up and does not parse `res.dat` from arbitrary paths.
These fields are untrusted telemetry and prove nothing about the player. None of
these reports is a reason to admit anyone to the world.

When `NetworkStream.WriteAsync` completes, the frame has been handed to the
local transport. That does not mean the server has processed it. World admission
is a separate step: the GameServer handshake and redemption of the ticket.

## Security

The 5517 AccountServer XOR/nibble stream cipher and the password RC5 transform
are not authenticated encryption. The loopback-only credential gate from
AUTH-001D is unchanged. AUTH-001E does not enable credential submission over
public networks, redirects to arbitrary endpoints, login UI integration,
MAC-based authentication or a GameServer connection.

No packet, session token, authentication key or hardware address is logged.
Temporary packet byte arrays are zeroed on every exit path, but bytes already
copied into the OS network stack cannot be recalled. There are no automatic
retries, background tasks, queues, global mutable cipher state or resource path
discovery.

## Testing

Regression tests check both unencrypted packet layouts and their padding, the
5517-compatible ciphertext at outbound offsets 276 and 328, the report order,
and a single socket carrying 1059, 1060, 1055, 1100 and 1052 in that order. They
also cover EOF/reset on close, an omitted MAC, rejection of an invalid MAC with
no partial write, cancellation, single use and disposal. The existing
signed-catalog and failed-1055 tests still show that those paths reject bad
input. The change passes locked restore, format verification, Release build and
tests, content verification, and the macOS, Windows, Quality and Dependency
Review jobs.
