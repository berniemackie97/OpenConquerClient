# ROLE-001B: Authenticated character creation and world-entry boundary

The desktop host now transfers the authenticated GameServer connection out of
`LoginAttemptCoordinator` when login-channel `NEW_ROLE` arrives. This is the
same socket that received the single-use ticket-backed `1052` login proof;
there is no second authentication attempt for character creation.

## Established protocol path

1. AccountServer `1060 → 1055`, post-authentication `1100`, GameServer
   handshake and authenticated `1052`.
2. New accounts receive login-channel `1004/2101 NEW_ROLE`. The client
   owns the role stage and sends `1001` with the authenticated account session
   token. Invalid choices and duplicate names can be retried on the same
   connection within the server's bounded creation admission.
3. On creation-channel `1004/2100 ANSWER_OK`, the client sends the repeated
   `1052` using the existing negotiated session. The server resolves the
   newly committed character under the authenticated account and enters the
   existing-character bootstrap.
4. Both new and existing characters read the `1004` login response, the
   `2078/2079` prelude and `1006` character information, then send the
   EnterMap `10010/0x4A` request.
5. The bounded world-entry reader requires `1110` map data and `1016`
   weather followed by the matching EnterMap acknowledgement. It sends
   `0x198` and the item, friend, weapon-skill, magic, syndicate,
   silent-info and statistics requests in the server's required order.

The role screen uses the existing translated SWF timeline, not hand-drawn
screen replacements. The name is validated at edit time and at submission
using strict Windows-1252 encoding; both are needed because the native
packet uses encoded bytes, not UTF-16 character count. The native client
also tests rendered glyph ink before submission; that FreeType-based gate
is not implemented here. The companion server retains a stricter name
policy, so it may refuse names the retail client would submit (including
`ab=cd`). Do not replace the native glyph check with the server's
punctuation blacklist in the client.

## Ownership and failure handling

A revoked signed catalog or a failed login cannot publish an authenticated
GameServer session. Creating a role transfers the connection out of the
login coordinator; cancellation and transport loss dispose that owner
without reusing a half-completed proof. The GameServer session retains the
initial 1052 proof until the login route is known: it clears that copy
immediately for existing characters or after the one permitted repeat
following creation. It does not depend on a live AccountServer exchange
after login. A canceled role screen returns to login with a cleared role
status. Network and role-content failures must never be treated as world
admission.

## Verified inputs needed for the complete retail transition

- Character creation requires the original English retail 5517
  `ini/selectCharacter.ini`, SHA-256
  `45192235789d6c5ff43d963ce65b531b61d4b9f9423b669fa5e0496536cda92d`,
  recorded in `20_notes/Architecture/12_create_role_flow.md`.
  The role INI is a **loose retail file**, not generated localization or an
  embedded replacement. The runtime refuses a missing file and parses the
  supplied bytes; SHA-256 matching is enforced by the content-set verifier
  during import, CI and publish, not by a baked-in runtime fingerprint.
  The uploaded retail 5517 archive supplies the byte-identical original
  (3,612 bytes). Its original CRLF bytes are now tracked at
  `content/retail-5517/payload/ini/selectcharacter.ini`, listed in the
  content-set manifest, and included in the runtime content closure.
  The existing content importer re-creates this same payload entry from
  `ini/selectCharacter.ini` in an authorized retail root. Client build
  and publish stage that tracked payload without a separate manual copy.
  The older ZFSERVER file does not match this release.
- The GameServer only activates runtime creation with a validated
  `GameServer:CharacterCreation` configuration. Production defaults omit
  it. The server's Development configuration uses Birth Village
  (map 1010) at portal0 (60,108), as recorded in the archived 5517 map
  catalog and historical creation routine, with all six corrected
  allotments. Its released-content test checks the effective composed
  collision at that tile and the older blocked candidate (61,101).
  The Development candidate does not replace an independent production
  spawn approval or live runtime smoke test.
- The current client has no map/world scene renderer or gameplay input
  host. Completing the action ladder reaches the server's admission
  boundary but does **not** yet display or drive the real map. The
  role-loading movie and existing-character login screen therefore
  cannot be declared a completed native world transition.

## Sound-parity boundary

The verified retail `ini/selectCharacter.ini` names twelve distinct MP3s:
`music.mp3`, `page.mp3`, `error.mp3`, `Button_1.mp3`,
`Button_2.mp3`, `Button_3.mp3`, `click_1.mp3`, `Confirm.mp3`,
`Glide.mp3` and `key_1.mp3` through `key_3.mp3`. All twelve are
present in the 5517 source archive. The Flash translation already emits
`snd_cs`, `snd_bs` and `snd_db` events using the retail INI's
`[classSelection]`, `[bodySelection]` and `[dialogBox]` mappings.

**Role sound effects are not yet implemented:** `RoleCreationScreen.IRoleSounds.Play`
currently discards the events. The login OpenAL backend owns derived,
offline-decoded retail PCM for ten of these sounds, including the shared
`music.mp3` background track; `Button_3.mp3` and `click_1.mp3` are
not in its asset bank. An audio-capable login does not prove the role
stage has working sound. Do not map either missing cue to another effect.

To close this gate, use the existing retail-source verification and
offline-decode process to add the two missing clips, then connect the
role events to the same bounded OpenAL effect voices and preserve their
original per-event mapping, music and effect volumes, looping and
focus/shutdown behavior. Test every event key against the retail INI,
the decoded source files, and real playback. Do not include all 527
retail audio files in the startup closure solely because they exist;
future map/world sound assets belong to their implementing subsystems.

The separate world-sound contract is defined in
`20_notes/Sound/02_map_sound_path.md` and
`20_notes/Rendering/ndsound_dll/`: retail map sounds use the 5517
map-scene subcomponent and positional WAV playback, not the ancestor
`CMapSound` interval model.

## Acceptance gate

Do not represent a green unit-test/CI run as successful world entry.
The release gate is a live new-account creation and an existing-account
login, against the configured local GameServer and database, both reaching
and retaining a visible working world session with correct ownership,
disconnect/reconnect and cancellation behavior. Test refusal and
duplicate-name branches and prove no account ticket is redeemed twice.

Native anchors: `20_notes/Architecture/12_create_role_flow.md`,
`20_notes/Server/world-entry-bootstrap.md` and the client packet tests
under `tests/OpenConquer.Client.Tests/Networking`.
