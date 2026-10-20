# AUTH-001C: 5517 keypad encoding and account request composition

AUTH-001C builds the standard AccountServer request 1060: it encodes the password
with the retail per-account keypad, encrypts it with RC5 and writes the packet into a
caller buffer. It does not send anything.

## Sources

- `20_notes/Server/login-credential-envelope.md` (measurements of the retail client
  and a live server, 2026-08-23).
- `20_notes/Architecture/03_login_flash_startup_command_contract.md`, the confirmed
  CMD_PASSWORD path.
- OpenConquerServer `LoginPasswordKeypadDecoder`, `LoginCredentialKey`,
  `LoginAccountRequestPacket`.
- The existing client types `LoginSeedKey`, `CredentialRc5` and
  `StandardAccountRequest1060` are reused.

## Packet 1060 in the retail client

For the ordinary 5517 login (no shipped OEM configuration), client packet 1060 is 276
bytes: a four-byte little-endian frame header, a 128-byte account name, a 128-byte
credential field and a 16-byte protocol server name. Only the first 32 credential
bytes are RC5-encrypted. The remaining 96 bytes are zero.

The per-account keypad encoding runs before RC5. The keypad seed is the sum of the
account-name bytes, each taken as a signed byte. That sum seeds MSVCRT srand/rand,
which produces a 16-byte salt. The retail keyboard rows (VA `0x936768`) are searched
in order, shifted column first. A 255-entry permutation is sorted with the retail
client's descending selection-swap algorithm, using the weights
`value ^ salt[value & 15]`. The comparison is a strict greater-than, which matters
when two weights are equal. The encoder writes the 1-based permutation index of each
mapped character into a fixed 32-byte field padded with zeros.

RC5-32/12/16 derives its key from the login seed 1059, which the server sends
separately, and not from the account-name sum. The 16-byte key is produced with
MSVCRT and changes with every server seed. The retail client also XORs the credential
buffer in memory, but that XOR is undone before the RC5 input is assembled. It must
not be added as an extra transform on the wire.

The retail client's 48-row keyboard covers `0-9 a-z A-Z . - @ > _`. The encoder
rejects any password character outside that set instead of silently truncating the
password. This is a documented defensive deviation from the retail client. The
current login UI allows at most 15 password characters and 32 account characters, and
the composer uses the same input limits.

## Security and ownership

`StandardAccountCredentialComposer.Write` is a pure function. It takes password
characters owned by the caller and writes a 276-byte packet into a buffer owned by
the caller. It never sends packets, opens sockets, keeps references or logs
passwords. Its two temporary 32-byte spans (the encoded password and the ciphertext)
are zeroed in a `finally`, and `CredentialRc5` already zeroes its key, its working
copy of the input and its subkeys. The caller remains responsible for clearing its
source password, and the packet buffer once any permitted send is done.

The legacy RC5 and transport obfuscation are needed for wire compatibility but are
not authenticated encryption. A valid signed directory vouches for the endpoint
metadata only. It does not prove the server's identity or make the credential
transport secure. This code does not by itself permit sending credentials over a live
connection.

Out of scope: submitting from the login UI, sending `1060` on a socket, reading the
reply, handling the 1055 response, the GameServer handoff, retries, status reporting,
OEM account setup, mobile and bound-account variants, and any new login UI visuals.

## Testing

- The encoder matches the independent OpenConquerServer keypad vectors, including
  shifted digit collisions and an account seed with signed high bytes. NUL padding is
  checked.
- The composer matches an end-to-end packet fixture for a known account, password,
  server and seed. The fixture comes from a separately computed permutation that
  follows the retail algorithm, and a standard RC5 reference.
- An invalid account, password, realm or destination is rejected and the output is
  left unchanged.
- The earlier RC5 vectors and the zero-copy buffer and parser behavior from
  AUTH-001A/B still hold.
- Locked restore, format verification, Release build, full tests and content-set
  verification pass, as do the macOS, Windows, Quality and Dependency Review CI jobs.

## Realm protocol name

The signed catalog consumer and the publishing tool both enforce the AccountServer
rule for `ProtocolServerName`: 1 to 15 ASCII characters from `[A-Za-z0-9_()-]`. The
packet 1060 field is 16 bytes and padded with NUL. Protocol names are never
truncated, normalized or substituted. Display labels may use Unicode and are kept
separate from the protocol name. Regression tests cover acceptance by the publisher,
rejection in the signed catalog, and names of the maximum length.
