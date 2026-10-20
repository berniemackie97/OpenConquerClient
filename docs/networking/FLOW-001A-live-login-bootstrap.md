# FLOW-001A — Live local-realm login bootstrap

## Audited baseline

Client main `2a71792d9661e8eb8989b6ebd702baaace9cfc34` (PR #136 merged).
Companion server main `bda9f0e76108872b648a9cfa5bb245c82589023e`.
Native sources: `20_notes/Architecture/03_login_flash_startup_command_contract.md`,
`20_notes/Architecture/11_login_ui_assets_and_interactions.md` and
`20_notes/Network/13_connection_lifecycle.md`.

The login screen already had native artwork, input, selection and credential
editor but its Enter click never submitted credentials. Network components
existed independently without an actual desktop composition path.

## Runtime connection path

1. A pointer Enter release or an unmodified, nonrepeating Enter command
   activates the same login request. The UI never opens a socket itself.
2. The desktop event thread validates the **current embedded-authority-signed
   catalog** and a selected **literal loopback** AccountServer endpoint.
   No raw Server.dat, DNS, remote proxy or arbitrary user-supplied peer is used.
3. The credential editor snapshots account bytes and password characters in
   separate owned arrays and immediately zeroes the original editor password.
   The attempt zeroes its buffers immediately after account 1060 composition
   and again in a guaranteed finally path.
4. The existing signed-catalog account exchange sends 1060 and interprets
   the 1055 response with the original one-use server authority. On success
   the same AccountServer socket sends 1100 and simple-string 1052.
5. The AccountServer socket closes; the GameServer receives the single-use
   redirected ticket over a loopback DH/CAST5 session, then the standard
   post-DH 1052 proof, typed 1004 and the companion server's typed
   2078/2079/1006 path.
6. The still-live GameServer session is owned by the coordinator after
   confirmed 1006 receipt. A retry disposes it first. Client shutdown cancels
   the active attempt, drains it and disposes retained sessions.

Native 5517's successful 1006 processing proceeds toward EnterMap action 74.
This slice **does not** pretend that native hero/world mutation, action74,
character creation, or world rendering has been implemented or validated.
On 1006 receipt, the visible diagnostic explicitly says WORLD PENDING.
The server's new-character route remains disabled, so NEW_ROLE cannot yet
open the native creation screen.

## Native-specific inputs

- English dataset first two locale bytes: `En` (`0x6E45` little-endian).
- Hardware identity: native NetBIOS failure fallback is zero-filled; no
  unrelated hardware identifier or fingerprint is invented on macOS.
- `res.dat` integer is used when an existing local file contains a bounded
  integer; native defaults to zero when absent/invalid.
- Native `CMD_ENTERGAME`'s 10-second monotonic attempt guard runs before
  password validation; a repeated attempt is refused without sending bytes.
- Native English password acceptance is capped at 14 characters despite
  the text field's 15-character edit limit.

No exception messages, unknown peer payloads or credential bytes appear in
the login status. The diagnostic notice is **not** represented as a faithful
port of the native Flash loading/error movie: that separate visual and
timeline parity closure is still required before claiming complete retail UI.
The status is bounded, locally rasterized, and works without expanding the
curated content manifest.

## Reproducible local smoke test

Use the existing **publisher-signed local realm catalog**, not an unsigned
test shortcut. Verify `~/.config/openconquer/client.local.props` exists
without printing its key/path. The signed local selection is region
`local`, server `local-realm`, protocol server name `LocalRealm`,
AccountServer `127.0.0.1:9958`, GameServer `127.0.0.1:5816`.

With the local realm provisioned, Accounts schema v6, Game schema v13,
a known existing-character account and valid runtime secrets, launch
AccountServer and GameServer in separate development terminals. Confirm
their actual TCP listeners with `lsof`. Do not rerun database migrations
or reset any credential keys during smoke testing.

Launch the client in Release configuration. Select the signed LocalRealm,
enter the account and password, and press Enter. Verify progression through
CONNECTING TO ACCOUNT SERVER, CONNECTING TO GAME SERVER, LOADING CHARACTER
DATA, and CHARACTER RECEIVED WORLD PENDING. Verify no action74 send, no
world admission, no character creation, and clean shutdown/disposal.

Negative tests: wrong password, unavailable AccountServer, wrong/expired
catalog, unavailable GameServer, timeout, stale realm selection and repeat
Enter during an active request. No changes to database identity are permitted
for these negative tests.

## Quality gates

Locked restore, `dotnet format --verify-no-changes`, Release build/full
tests, content closure verification, Linux/macOS/Windows CI, dependency
review, CodeQL and review audit. Exact local desktop/server smoke is a
separate manual acceptance gate and **must not** be marked passed by CI.
