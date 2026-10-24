# FLOW-001A — Login bootstrap and native error presentation

The login composition submits credentials through the signed local realm catalog
and presents failures with retail `start.dat` errorMC (sprite274). The temporary
authentication status banner has been removed. Internal progress states remain
available to the coordinator; they are not invented player-facing messages.

## Native authority

The existing 5517 Binary Ninja database and the original SWF/resource files are
authoritative. Evidence is maintained in the actual client-5517 notes folder:

- `20_notes/Architecture/03_login_flash_startup_command_contract.md`
- `20_notes/Architecture/11_login_ui_assets_and_interactions.md`
- `20_notes/Architecture/FLOW-001A-login-error-ui-2026-10-20.md`
- `20_notes/Network/13_connection_lifecycle.md`

The focused note records instruction addresses, raw disassembly captures, SWF
symbol/depth details and remaining proof boundaries. Extracted asset hashes and
reproduction details are in [FLOW-001A-error-assets.json](FLOW-001A-error-assets.json).

## Error behavior

| Trigger | Presentation |
| --- | --- |
| Empty account | SWF constant `Please Enter Your User Name.` |
| Empty password | SWF constant `Please Enter Your Password.` |
| Missing/currently unusable selected realm | StrRes10012 |
| Repeated nonempty attempt within ten seconds | StrRes49995 |
| English password exceeds fourteen characters | StrRes11400 |
| AccountServer1055 refusal | Cn_Res `STR_CONNECT_ERROR_MSG_TYPE_%03u`, UNKNOWN fallback, `STR_LOGIN_FAIL` formatting |
| AccountServer1055 code24 | Preserve code; native `KICKOUT_BANACCOUNT` has no handler in the clean SWF, so no invented errorMC dispatch |
| GameServer1004 refusal | Owned original refusal bytes, NUL prefix and native `ERROR\|text` bridge delimiter boundary |
| AccountServer initial connect failure/timeout | StrRes10397 |
| GameServer initial connect failure/timeout | StrRes100100 |
| AccountServer established socket failure | StrRes100116 |
| GameServer established socket failure | StrRes10249 |

Empty fields are checked before the native attempt guard. The guard is stamped
before the English password-length check. An active attempt cannot be submitted
again. Failure notifications carry immutable owned evidence and are applied once;
a later identical failure is a new notification and restarts the movie.

1055 formatting operates on narrow resource bytes, substitutes the unsigned
resource key and signed 32-bit `%d` value, and truncates to 255 bytes before text
conversion. Inserted text is never reinterpreted as a format string or HTML.
The Windows presentation bridge selects the system ACP. Other hosts explicitly
use the English retail profile's Windows-1252 encoding. This does not relax
protocol validation or change packet decoding contracts.

## Movie, input and resources

errorMC renders at root depth179, translated(-1,0), below enterMC and the border.
The 30fps timeline begins at frame2, stops at6, and returns through frames7–10.
Only the mask fades; artwork is held at6. The mask covers the authored y90–660
band. The held image is at(360,259), the OK hit bounds are(461,415,96,26), and
field273 retains its authored Arial14, red, multiline plain-text parameters.
Text is clipped to the field and wraps without splitting Unicode surrogate pairs.

The complete Up/Over/Down button compositions come from the original SWF.
They are not layered over an already baked-in Up state, which would blend
translucent edges twice. Show/hide, hover and press use existing retail audio
cues. Pointer capture, drag-out and focus-loss behavior are retained. The modal
blocks credential editing, clipboard access, underlying buttons and Enter.
OK dismisses on pointer release; Enter does not activate it.

Escape opens idle quit sprite287 at depth205. During a pending login it opens
sprite396 at depth294, one pixel right and above enterMC. Cancel returns to the
underlying dialog; OK requests application exit only at frame10. Exit still
uses application shutdown to cancel and drain the coordinator.

All textures and font resources belong to the graphics context and are released
on initialization failure or disposal. The conformance runner recreates and
double-disposes the presentation, checks every error timeline frame and all
error/quit button states, and exercises dynamic text upload and clipping.
Full-stage fixtures are independent of runtime crop placement. Comparison
accounts for the renderer's RGB565/RGB555 target precision.

## Transport and credential ownership

Only a current authority-signed catalog and literal loopback AccountServer are
accepted. The editor moves credentials into separate owned buffers and clears
the displayed password immediately. Both buffers are zeroed after account1060
composition and again on every completion path. No exception text is displayed.
No new credential logs, persistence, trust bypasses or wire formats are introduced.

After a successful1055, the same account socket sends native1100 and account1052
before closing. GameServer DH/CAST5, proof1052, control1004 and existing-character
2078/2079/1006 handling retain their existing ownership and cancellation rules.
Catalog expiry/replacement or selection revision revokes pending completion.
A stale completion cannot reopen an error or retain an accepted game socket.
Shutdown cancels pending work, drains it, and disposes the retained game session.

Account connect retains its ten-second ceiling. The signed loopback flow has no
legacy BGP fallback and does not reproduce the native post-failure blocking
`Sleep(5000)` at `0x66D477`; exact prompt latency for that branch is not claimed. Game connect now uses the native
thirty-second argument proven at `0x754DFB`. Existing five-second bounded seed,
DH and packet-read deadlines remain a managed transport policy. When those
expire the transport closes and the corresponding native socket-loss resource
is displayed. These deadlines are **not claimed as native timeout parity**.
Malformed protocol/local validation failures also close the attempt and use its
phase's loss resource; there is no proven native counterpart for every stricter
managed validation failure.

Successful1006 remains the existing world-pending boundary. This correction does
not implement EnterMap action74, world mutation/rendering, or the NEW_ROLE screen.

## Text metrics

Field273 text uses the advances and line metrics measured from the retail Flash 7
control on Windows with the original `start.dat` (see
[FLOW-001A-native-text-metrics.json](FLOW-001A-native-text-metrics.json)):
per-byte Arial 14 advances for the Windows-1252 range, an 18-pixel baseline
advance (not 14 + leading 2), GDI ascent 13, and no non-additive pairs across
all 50,176 byte pairs. Flash keeps CRLF as two line breaks and treats a lone CR
or LF as one; the layout follows that. Glyph shapes come from the existing
cross-platform font renderer, so edge coverage is not pixel-identical to GDI.

## Verification and open acceptance gates

The normal gates are locked restore, formatting verification, Release build,
the full test suite, content verification and the OpenGL conformance runner
against the clean retail content directory. All pass locally and in CI on the
PR head.

Still open: glyph edge coverage versus Windows GDI, and a live desktop run of
the login against a local AccountServer and GameServer.
