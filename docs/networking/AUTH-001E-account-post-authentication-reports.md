# AUTH-001E — Native AccountServer post-authentication reports

## Source of truth

Client base: `ad7224768e3a9e742f77e6699ff5e5dca71bd589` (AUTH-001D, PR #118 merged).
Native research: `20_notes/Architecture/03_login_flash_startup_command_contract.md`,
`CMsgAccountAuthResponse__Process` `0x75492C`, `CMsgAccountMacAddress1100__Build`
`0x767DA1`, `CMsgMapInfo1052__BuildSimpleString` `0x7543CA`.
Corroboration: OpenConquerServer `LoginPostAuthenticationReportReader`,
`LoginAccountMacAddressReportPacket`, `LoginAccountResourceVersionReportPacket`.

## Wire sequence

A successful AccountServer 1055 leaves the same legacy encrypted account socket open.
The original client then, in order:

1. Builds and sends **1100**, exactly 52 bytes: `[length:2][type:2][sessionUid:4]
   [macText:40][unknownTrailing:4]`. Native MAC text is 12 uppercase hexadecimal
   ASCII characters, or a zero-filled field when unavailable. The last four bytes
   have no established semantic meaning and are zero initialized here.
2. Builds and sends the **AccountServer form of 1052**, exactly 28 bytes:
   `[length:2][type:2][sessionUid:4][resDatVersion:i32][resDat:16]`.
   The resource name is the exact ASCII `res.dat`, padded with zero bytes.
3. Closes the AccountServer connection, before any GameServer connection or DH logic.

These packets use the **existing client-to-account stream cipher state** after the
276-byte type 1060 credential packet. The server applies its inverse transform.
No additional seed, cipher reset, reconnect, trailer, packet response or queue is
inferred. Packet 1052 is context-sensitive: the account report above is NOT the
GameServer login proof, even though both can be 28 bytes.

## Type and lifetime ownership

`LoginAccountMacAddressReport1100` and `LoginAccountResourceVersionReport1052`
are pure serializers. They never perform I/O or retain references. They validate
the full destination and caller input before any mutation and NUL-pad unused fields.

`LoginAccountExchangeResult` remains the sole owner of the successful 1055
AccountServer connection. It now exposes `CompletePostAuthenticationAsync`,
not direct socket access. The method checks that the session has the matching
successful 1055 token and atomically claims one report attempt. Validation of
**both** reports precedes any write. Both writes share a positive, at-most-30-second
deadline and caller cancellation. A timeout, cancellation, malformed input,
unrecoverable send exception, and normal completion all close the socket and
zero the transient frames. A second attempt is forbidden; no retry on an
uncertain write. A failed 1055 cannot send any reports. The exchange result
itself is idempotently disposable.

The source hardware text and resource version are supplied **explicitly by the
caller**; this slice does not gather MAC addresses, read arbitrary local files,
or require OS-specific APIs. An empty MAC is accepted as native failure-to-discover
behavior. A nonempty MAC must be exactly 12 uppercase ASCII hexadecimal bytes.
Resource version is a signed 32-bit integer from trusted local content metadata;
this slice does not invent or automatically parse `res.dat` from arbitrary paths.
These fields are untrusted telemetry, not authentication proof. None of these
reports are grounds for permitting world admission.

`NetworkStream.WriteAsync` completion means a frame was handed to the local
transport, **not** that the remote server processed it. World admission remains
a separate GameServer handshake and ticket-consumption operation.

## Security

The native 5517 AccountServer XOR/nibble stream cipher and password RC5 transform
are not authenticated encryption. The AUTH-001D loopback-only credential gate
remains unchanged. This change does not enable public network credential
submission, arbitrary endpoint redirects, login UI integration, MAC-based
authentication, or GameServer connection.

No packet, session token, authentication key, or hardware address is logged.
Temporary packet byte arrays are zeroed on every terminal path, but bytes
already copied into the OS network stack cannot be recalled. No automatic
retry, background tasks, queues, mutable global cipher state, or resource
path discovery is introduced.

## Acceptance

Regression tests verify the two unencrypted packet layouts and padding,
exact native-compatible ciphertext at outbound offsets 276 and 328, report
ordering, one socket for 1059->1060->1055->1100->1052, EOF/reset on close,
MAC omission, invalid MAC fail-closed without partial writes, cancellation,
one-use behavior and disposal. Existing signed-catalog and failed-1055 tests
retain their fail-closed contracts. All new work must pass locked restore,
format verification, Release build and tests, content verification,
macOS/Windows/Quality, Dependency Review, independent review and human merge
approval.
