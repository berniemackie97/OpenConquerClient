# STARTUP-001W: 5517 login seed and credential cipher primitives

Builds on the standard 276-byte account request envelope and on the signed realm selection retention from STARTUP-001V. This note adds the next piece needed for 5517 account login: the path from the server's login seed to the cipher that encrypts the credentials. Enter stays disabled, no sockets are opened, no authentication responses are simulated, and the existing packet encoder is not turned into a login session.

Sources, from the project's `20_notes` research notes:

- `Network/08_system_packets.md`: `CMsgLoginSeed__Process_DeriveCredentialRc5KeyFromSeed` at `0x764BC6` reads a uint32 at packet offset 4. The message type is 1059. The retail sources do not establish the full length of a type-1059 frame. The frame parser therefore requires at least eight bytes, requires the declared length to match, and caps frames at 64 bytes. It reads only the seed field, which is the one field confirmed by the sources. The 64-byte cap is our own defensive limit and does not come from the retail client. Any transport added later must hand this reader one complete, decrypted, framed message.
- `Server/login-credential-envelope.md`: the seed sent by the server initializes the MSVCRT LCG (`state = state * 0x343FD + 0x269EC3`), and sixteen successive `rand() % 256` values form the RC5 key used at login. The static pre-login key mentioned in some older references is not the key used at login.
- `Crypto/01_rc5_credential_field.md`: credentials are encrypted with RC5-32/12/16 (`P32=0xB7E15163`, `Q32=0x9E3779B9`, 26 subkeys, 78 expansion mixes). With an all-zero key and block, the reference output is `21A5DBEE154B8F6D`.
- `Architecture/03_login_flash_startup_command_contract.md`: with the default `AccountSetup/Type != 1`, the client encrypts 32 credential bytes for standard type 1060 and leaves the remaining 96 bytes of the credential envelope zero-filled. The `Type == 1` OEM variant encrypts 128 bytes and is not implemented.

## Behavior

`LoginSeed1059` checks the little-endian `[length:u16][type:u16][seed:u32]` part of a plaintext message that has already been framed. It does no socket I/O, buffering, decompression or hidden negotiation, and it never guesses the byte order. An inconsistent frame is rejected and no seed is returned.

`LoginSeedKey.Derive` reproduces the 16 key bytes that MSVCRT generates from the seed, without touching the process-wide C runtime RNG. The same seed gives the same key bytes on macOS, Windows and Linux.

`CredentialRc5` expands the RC5-32/12/16 round keys once for a 32-byte default credential and encrypts it as four 8-byte blocks. Its input is credential bytes that are already keypad-encoded and padded. It does not take a .NET password string. It copies the whole source before writing, so overlapping buffers are safe. Temporary credential bytes, derived key bytes and expanded round keys are cleared on exit. It keeps no plaintext, creates no strings or log entries, and holds no global cryptographic state. A wrong length throws before the output is modified. The single-block operation is private and exists only to check the published RC5 test vector.

**RC5 is here only for compatibility with 5517. It is not modern transport security.** The 5517 per-account keypad transform, the 5517 socket stream cipher and the code that drives the seed handshake are separate prerequisites and are not implemented yet. Passing a cleartext password straight to this function would give the wrong result. Enter is not wired up and no connections are made. Unsigned realm endpoints are still rejected, and credential cleanup, server and account routing, and Caddy are unchanged.

## Testing

Independently computed reference fixtures cover seeds 0, 1, `0x12345678` and `0xFFFFFFFF`, full-width overflow behavior, the published RC5 vector, 32-byte multi-block encryption, overlapping spans, validation before any write, malformed 1059 frames and the 64-byte cap, and compatibility with the zero-fill of the existing type-1060 envelope. Run a locked restore, the formatting check, a Release build, the focused network tests and the full solution tests, the content-set verification, Windows and macOS CI and CodeQL.

These offline primitives do not give a working live login. That needs separate work on the seed exchange, stream cipher, keypad transform and session, with each part authenticated and checked against the retail client.
