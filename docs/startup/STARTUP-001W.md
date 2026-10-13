# STARTUP-001W — Native 5517 login seed and credential cipher primitives

## Audited baseline and rationale

This slice applies after merged PR #102, main `f725e558cf7b12bace59abd45decb6747e3ad2f8`. PRs #93 and #102 established, respectively, the standard 276-byte account request envelope and the authenticated realm selection continuity. The next missing **bounded native-account prerequisite** is the original login-seed to credential-cipher path. Do not enable Enter, establish sockets, invent authentication responses or turn a verified packet encoder into an unverified login session.

Sources from the project-owned `20_notes` evidence archive:

- `Network/08_system_packets.md`: `CMsgLoginSeed__Process_DeriveCredentialRc5KeyFromSeed` at `0x764BC6` reads a uint32 at packet offset 4; message type is 1059. Original evidence does **not** establish an exact overall type-1059 frame length; our frame parser therefore checks a conservative minimum of eight bytes, declared-length equality and a first-party 64-byte cap, while interpreting **only** the verified seed field. The 64-byte cap is a defensive implementation limit, not native proof. A future transport must deliver one fully decrypted, framed message before invoking this reader.
- `Server/login-credential-envelope.md`: the server-supplied seed initializes the MSVCRT LCG (`state = state * 0x343FD + 0x269EC3`) and sixteen successive `rand() % 256` values form the actual RC5 key. The static pre-login key sometimes noted in older references is not the active login key.
- `Crypto/01_rc5_credential_field.md`: the credential transform is RC5-32/12/16 (`P32=0xB7E15163`, `Q32=0x9E3779B9`, 26 subkeys, 78 expansion mixes). Zero-key, zero-block reference output is `21A5DBEE154B8F6D`.
- `Architecture/03_login_flash_startup_command_contract.md`: for default `AccountSetup/Type != 1`, the client encrypts exactly 32 credential bytes for standard type 1060 and leaves the remaining 96-byte credential envelope zero-filled. The `Type == 1` OEM variant encrypts 128 bytes and is intentionally **not** implemented here.

## Behavior and security boundaries

`NativeLoginSeed1059` verifies the little-endian `[length:u16][type:u16][seed:u32]` portion of an already-framed plaintext message. It does no socket I/O, buffering, endian guessing, decompression or hidden negotiation. Inconsistent frames fail closed without exposing an out-seed.

`NativeLoginSeedKey.Derive` exactly reproduces the 16 MSVCRT seed bytes without altering the process-wide C runtime RNG, producing deterministic key material on macOS, Windows and Linux.

`NativeCredentialRc5` expands RC5-32/12/16 round keys once for a 32-byte default credential and encrypts four 8-byte blocks. It accepts **already keypad-encoded and padded** credential bytes, not a .NET password string. It snapshots the entire source before writing, so overlapping buffers are safe; temporary credential bytes, derived key bytes, and expanded round keys are cleared on exit. No persistent plaintext, strings, logs or global cryptographic state are created. Wrong lengths throw before output mutation. The single-block operation is a private validation primitive for the published RC5 test vector.

**Critical:** Legacy RC5 is for compatibility with 5517 and is not modern transport security. The native per-account keypad transform, native socket stream cipher and seed-handshake orchestration remain separate, **unimplemented** prerequisites. Encrypting a cleartext password directly with this function would be incorrect. This slice does not wire Enter, create connections, accept unsigned realm endpoints, weaken credential cleanup, alter server/account routing or modify Caddy.

## Tests and gates

Independent fixed reference fixtures cover seeds 0, 1, `0x12345678`, and `0xFFFFFFFF`, full-width overflow behavior, the published RC5 vector, 32-byte multi-block encryption, overlapping spans, validation-before-write, bounded malformed 1059 frames and compatibility with the existing type-1060 envelope's zero-fill. Run locked restore, formatting, Release build, focused network tests and full solution tests, content-set verification, Windows/macOS CI and CodeQL. The end-to-end live login **cannot** be claimed from these offline primitives; a separate authenticated, native-verified seed/stream/keypad/session slice is required.
