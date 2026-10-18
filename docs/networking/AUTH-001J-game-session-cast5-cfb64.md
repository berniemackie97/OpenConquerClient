# AUTH-001J — Native GameServer session CAST5-CFB64 (offline)

## Audited baseline and evidence

Baseline: OpenConquerClient `main` `17487436f76010c1571231ee52aa19fec48c414d`,
after merged AUTH-001I (#127).

This slice completes the **offline** session-crypto transition following the
AUTH-001G decrypted challenge, AUTH-001H fixed-key bootstrap transform, and
AUTH-001I encrypted client DH response and native session-material derivation.

Independent server evidence:

- `OpenConquerServer/src/OpenConquer.Protocol/Game/Cryptography/GameSessionCipher.cs`
- `OpenConquerServer/src/OpenConquer.Protocol/Game/Cryptography/GameCast5Cfb64Cipher.cs`
- `OpenConquerServer/tests/OpenConquer.Protocol.Tests/Game/Cryptography/GameSessionCipherTests.cs`
- `OpenConquerServer/src/OpenConquer.Protocol/Game/Handshake/GameHandshakeExchange.cs`
- Retail 5517 network research cited in AUTH-001G through AUTH-001I, especially
  `20_notes/Network/13_connection_lifecycle.md` and
  `20_notes/Network/14_session_derived_legacy_cipher_mask.md`.

These establish the algorithm, OpenSSL shared-secret representation, first-16-byte
key selection, directional IV mapping, CFB-64 continuation, and post-DH state
transition. The fixed ciphertext fixtures below were **independently computed
using Python cryptography's CAST5/CFB-64 implementation**; they are not captures
of live GameServer traffic.

## Exact cryptographic contract

The native client retains the full 64-byte DH_compute_key-compatible key material:
the minimal big-endian secret begins at offset zero, followed by zeros. Standard
CAST5 takes **only the first 16 bytes** as its 128-bit key. Do not left-pad or
reinterpret the 64-byte material as a different derived key.

A session has exactly two separately owned CFB-64 streams:

| Client direction | Negotiated IV | GameServer direction |
| --- | --- | --- |
| Inbound decryption | Server outbound IV (challenge chunk 2) | Server outbound encryption |
| Outbound encryption | Server inbound IV (challenge chunk 3) | Server inbound decryption |

The shared `GameCast5Cfb64Cipher` owns one direction's 8-byte feedback register,
8-byte keystream and position. Every eight bytes, it CAST5-encrypts the current
feedback register, XORs keystream with input and incorporates **ciphertext**
into feedback. It retains feedback/position across successive fragments;
in-place transformation must preserve source ciphertext before writing plaintext.

The primitive is deliberately not concurrently callable on the same direction.
Its owners synchronize lifecycle and transform operations. The bootstrap
adapter and negotiated session owner use this one reviewed implementation
rather than diverging algorithm copies. Invalid output lengths and partial
overlap fail before modifying output or feedback state.

`GameSessionCipher` enforces an exact 64-byte native material buffer and two
8-byte IVs, constructs independent inbound/decryption and outbound/encryption
primitive instances, and owns their disposal. Separate inbound/outbound locks
permit concurrency across directions without corrupting feedback. Disposal
acquires inbound then outbound locks and waits for any active transform before
clearing the owned feedback and keystream state. All later transforms fail.
As with AUTH-001H, a third-party CAST5 engine may retain a provider-internal
expanded key schedule until garbage collection; do not claim complete
cryptographic key erasure.

`GameSessionCipher.CreateFromExchange` consumes AUTH-001I's transfer exactly
once, after its encrypted client response has been copied out. It stages the
secret and IVs on bounded stack spans and clears those spans in a `finally`,
including on constructor failure. Issuing a response in memory **does not
prove it was written to the network or accepted by the GameServer**. Therefore,
this factory is suitable for offline verification only; a future live
transport must enforce write-completion/uncertain-send failure semantics.

## Independently pinned vectors

For fixed native 64-byte material `02 71` followed by 62 zero bytes and
challenge IVs `0102030405060708` (client inbound) and
`15161718191A1B1C` (client outbound):

| Direction | Input | Expected ciphertext/plaintext |
| --- | --- | --- |
| Client outbound | ASCII `client-to-server` | `E96A3ACC730CDF643BDC70270D240D8D` |
| Client inbound | `3008641B48568808A401EBB910ABE8A4` | ASCII `server-to-client` |

A separate independent 97-byte outbound ciphertext covers arbitrary
3/17/1/51/25-byte fragment boundaries, including the CFB block transition.
The existing bootstrap known-vector tests must remain bit-for-bit unchanged.

Tests also exercise the existing AUTH-001I deterministic DH secret,
single-use transfer, identical in-place spans, rejected partial overlap,
invalid destination atomicity, key-tail truncation compatibility, independent
directional state, constructor validation, and idempotent disposal.

## Security, ownership and exclusions

The negotiated cipher is **legacy CAST5-CFB64 without a MAC**. The underlying
native DH group is 512 bits and does not authenticate the remote server.
Neither a valid challenge, DH shared secret, nor session ciphertext is evidence
of endpoint authenticity or integrity against active tampering.

This slice creates **no socket**, accepts **no redirect**, and sends neither
the client DH response nor the GameServer login proof 1052. There is no
gameplay framing, packet reader, automatic retry, UI credential submission, or
world admission. The AccountServer's loopback-only credential guard and signed
realm-directory trust policy are unchanged.

Only a later audited transport/endpoint-trust slice may bind these primitives
to a live session. That owner must preserve strict handshake ordering and
timeouts, revalidate authenticated endpoint authority, avoid reads ahead of
phase transitions, abort after uncertain writes, and zero caller-owned secret
copies and sensitive frame buffers.

## Acceptance gates

Locked restore, format verification, Release build, complete unit tests,
retail content verification, Linux Quality, macOS and Windows, Dependency
Review, external review threads, and explicit human merge acceptance.
