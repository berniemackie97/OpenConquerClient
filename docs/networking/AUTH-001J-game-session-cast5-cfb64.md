# AUTH-001J: GameServer session CAST5-CFB64 (offline)

AUTH-001J completes the offline switch to session encryption. It follows the
AUTH-001G decrypted challenge, the AUTH-001H fixed-key bootstrap transform, and
the AUTH-001I encrypted client DH response and session-material derivation that
matches the retail client.

## Sources

- `OpenConquerServer/src/OpenConquer.Protocol/Game/Cryptography/GameSessionCipher.cs`
- `OpenConquerServer/src/OpenConquer.Protocol/Game/Cryptography/GameCast5Cfb64Cipher.cs`
- `OpenConquerServer/tests/OpenConquer.Protocol.Tests/Game/Cryptography/GameSessionCipherTests.cs`
- `OpenConquerServer/src/OpenConquer.Protocol/Game/Handshake/GameHandshakeExchange.cs`
- The retail 5517 network research cited in AUTH-001G through AUTH-001I,
  especially `20_notes/Network/13_connection_lifecycle.md` and
  `20_notes/Network/14_session_derived_legacy_cipher_mask.md`.

Together these define the algorithm, the OpenSSL representation of the shared
secret, the use of its first 16 bytes as the key, the mapping of IVs to
directions, how CFB-64 continues across calls, and the state change after DH.
The fixed ciphertext fixtures below were computed independently with the
CAST5/CFB-64 implementation of the Python cryptography package. They are not
captures of live GameServer traffic.

## Cipher

The retail client keeps the full 64-byte key material in the layout that
DH_compute_key produces: the minimal big-endian secret starts at offset zero and
is followed by zeros. Standard CAST5 takes only the first 16 bytes as its
128-bit key. The 64-byte material must not be left-padded or reinterpreted as
some other derived key.

A session has two separately owned CFB-64 streams:

| Client direction | Negotiated IV | GameServer direction |
| --- | --- | --- |
| Inbound decryption | Server outbound IV (challenge chunk 2) | Server outbound encryption |
| Outbound encryption | Server inbound IV (challenge chunk 3) | Server inbound decryption |

The shared `GameCast5Cfb64Cipher` owns the 8-byte feedback register, the 8-byte
keystream and the position for one direction. Every eight bytes, it encrypts the
current feedback register with CAST5. It XORs the keystream with the input and
feeds the ciphertext back into the feedback register. Feedback and position
carry over between successive fragments. For an in-place transform, the source
ciphertext must be saved before plaintext is written over it.

By design, the primitive must not be called concurrently for the same direction.
Its owners serialize lifecycle and transform calls. The bootstrap adapter and
the negotiated session owner share this one reviewed implementation instead of
keeping separate copies of the algorithm. Invalid output lengths and partial
overlap are rejected before the output or the feedback state changes.

`GameSessionCipher` requires a 64-byte key material buffer in the 5517 layout
and two 8-byte IVs. It creates separate primitive instances for inbound
decryption and outbound encryption, and owns their disposal. Separate inbound
and outbound locks let the two directions run at the same time without
corrupting each other's feedback. Disposal takes the inbound lock and then the
outbound lock, waits for any running transform, and then clears the owned
feedback and keystream state. Every later transform fails. As in AUTH-001H, a
third-party CAST5 engine may keep its internal expanded key schedule until
garbage collection, so this code cannot promise complete erasure of the key.

`GameSessionCipher.CreateFromExchange` consumes the AUTH-001I transfer once,
after the encrypted client response has been copied out. It stages the secret
and the IVs in fixed-size stack spans and clears them in a `finally`, including
when the constructor fails. Issuing a response in memory does not prove that it
was written to the network or accepted by the GameServer. This factory is
therefore suitable only for offline verification. A future live transport must
treat an incomplete or uncertain send as a failure.

## Test vectors

For the fixed 64-byte material `02 71` followed by 62 zero bytes, in the 5517
layout, and the challenge IVs `0102030405060708` (client inbound) and
`15161718191A1B1C` (client outbound):

| Direction | Input | Expected ciphertext/plaintext |
| --- | --- | --- |
| Client outbound | ASCII `client-to-server` | `E96A3ACC730CDF643BDC70270D240D8D` |
| Client inbound | `3008641B48568808A401EBB910ABE8A4` | ASCII `server-to-client` |

A separate, independently computed 97-byte outbound ciphertext covers fragment
boundaries at arbitrary sizes of 3/17/1/51/25 bytes, including the CFB block
transition. The existing bootstrap known-vector tests must stay bit-for-bit
unchanged.

Tests also cover the existing fixed DH secret from AUTH-001I, single-use
transfer, identical in-place spans, rejection of partial overlap, atomic
rejection of invalid destinations, compatibility with key-tail truncation,
independent state per direction, constructor validation and idempotent disposal.

The change passes locked restore, format verification, Release build, the
complete unit tests, retail content verification, and the Linux Quality, macOS,
Windows and Dependency Review jobs.

## Security and scope

The negotiated cipher is legacy CAST5-CFB64 without a MAC. The underlying 5517
DH group is 512 bits and does not authenticate the remote server. A valid
challenge, a DH shared secret and session ciphertext do not prove that the
endpoint is genuine, and do not protect message integrity against an active
attacker.

AUTH-001J creates no socket and accepts no redirect. It sends neither the client
DH response nor the GameServer login proof 1052. It adds no gameplay framing,
packet reader, automatic retry, UI credential submission or world admission. The
AccountServer's loopback-only credential guard and the trust policy of the
signed realm directory are unchanged.

Only a later, separately reviewed transport and endpoint-trust step may bind
these primitives to a live session. That owner must keep the handshake order and
timeouts, check again that the endpoint is authenticated, avoid reading ahead
across phase transitions, abort after an uncertain write, and zero the
caller-owned copies of secrets and sensitive frame buffers.
