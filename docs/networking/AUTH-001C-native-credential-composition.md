# AUTH-001C — Native keypad and standard account request composition

## Audited dependency baseline

- OpenConquerClient `main` at `759c7eedce052e952142b13b18ba70af407d8b4f`, after merged AUTH-001B PR #115.
- `20_notes/Server/login-credential-envelope.md` (native client and live-server measurements, 2026-08-23).
- `20_notes/Architecture/03_login_flash_startup_command_contract.md`, verified CMD_PASSWORD path.
- OpenConquerServer `LoginPasswordKeypadDecoder`, `LoginCredentialKey`, `LoginAccountRequestPacket`.
- Existing client `LoginSeedKey`, `CredentialRc5` and `StandardAccountRequest1060` are reused.

## Proven native contract

For the ordinary 5517 login (no shipped OEM configuration), client packet 1060 is 276 bytes:
four-byte little-endian frame header, 128-byte account name, 128-byte credential field,
16-byte protocol server name. Only the first 32 credential bytes are RC5-encrypted;
the remaining 96 bytes are zero-filled.

Per-account keypad encoding precedes RC5. The keypad seed is the signed-byte sum of
the exact account-name bytes, fed to MSVCRT srand/rand for a 16-byte salt.
Native keyboard rows (VA `0x936768`) are searched in order, shifted column first.
A 255-entry permutation is sorted by the native descending selection-swap algorithm
using weights `value ^ salt[value & 15]`; strict greater-than comparison matters
for equal weights. Encoding yields each mapped 1-based permutation index, with
a zero-padded fixed 32-byte field.

RC5-32/12/16 uses the **separately received login seed 1059**, not the account-name
sum, to derive its key. The MSVCRT-derived 16-byte key changes with each server seed.
The in-memory credential-buffer XOR is canceled before the RC5 input is assembled;
it must NOT be recreated as another wire transform.

Native's 48-row keyboard repertoire is `0-9 a-z A-Z . - @ > _`. The modern
encoder rejects unsupported password characters rather than silently truncating,
a documented defensive deviation. The current modern UI allows at most 15 password
characters and 32 account characters; the composer adopts those input bounds.

## Security, ownership and exclusions

The pure `StandardAccountCredentialComposer.Write` takes caller-owned password
characters and produces a caller-owned 276-byte packet. It never sends packets,
opens sockets, retains references, or logs passwords. Both 32-byte transient
encoded/ciphertext spans are zeroed in a `finally`; `CredentialRc5` already zeroes
its key, working source and subkeys. The caller remains responsible for clearing
its source password and the eventual packet buffer after any permitted send.

The wire-compatible legacy RC5 and transport obfuscation are **not authenticated
encryption**. A valid signed directory establishes endpoint metadata authority,
not server identity or secure credential transport. This code does not grant
authority to send credentials over a live connection.

Out of scope: login UI submission, `1060` socket sends, post-submit reads,
1055 response handling, GameServer handoff, retries, status reporting, OEM
account setup, mobile/bound-account variants, or new login UI visuals.

## Acceptance criteria

- Match independent OpenConquerServer keypad vectors including shifted digit
  collisions and signed high-byte account seed; assert NUL padding.
- Match an end-to-end packet fixture for a known account/password/server/seed,
  from a separately evaluated native-consistent permutation + standard RC5 reference.
- Reject invalid account/password/realm/destination without modifying the output.
- Preserve prior RC5 vectors and the zero-copy buffer/parser contract from AUTH-001A/B.
- Locked restore, format verify, Release build, full tests and content-set verify;
  macOS, Windows, Quality and Dependency Review CI must pass before merge.

## Realm identity boundary review (PR #117)

The signed catalog consumer and publishing tool both enforce the AccountServer's
`ProtocolServerName` contract: 1–15 ASCII characters from `[A-Za-z0-9_()-]`.
The packet 1060 field is 16 bytes and is NUL-padded. Protocol identities are not
truncated, normalized, or substituted. Unicode remains supported for *display*
labels, which are separate from the protocol identity. Regression tests cover
publisher acceptance, signed-catalog rejection, and maximum-length names.
