# CP3.1 — Wire-contract ownership

[Decision issue #247](https://github.com/berniemackie97/OpenConquerClient/issues/247) |
[Server companion](https://github.com/berniemackie97/OpenConquerServer/blob/main/docs/protocol/wire-contract-ownership.md)

## Recommendation

Keep client and server wire codecs in their existing repositories. Do not introduce a shared
runtime assembly or package for the current protocol. Make verified, immutable byte vectors
the common contract, with copies tested locally by each repository. This recommendation
becomes the accepted CP3.1 decision after both companion PRs are reviewed and merged.

## Implementation comparison

| Boundary | Client | Server | Consequence |
| --- | --- | --- | --- |
| Account cipher | `LoginLegacyStreamCipher` transforms outgoing client bytes | `LoginStreamCipher` uses the complementary server-side transform | Sharing the same transform method would obscure direction-specific behavior |
| Game cipher | `GameCast5Cfb64Cipher` and `GameSessionCipher` own two directional streams | Corresponding types also own two streams, with different disposal/locking contracts | CAST5/CFB64 is repeated, but session ownership differs |
| Secured framing | `GameSecuredFrameDecoder` accepts server-signed frames and returns owned buffers | The server decoder accepts client-signed frames and exposes its own buffer lifetime | Both enforce a 1,024-byte packet maximum and eight-byte trailer, but accept opposite signatures |
| Walk packet 10005 | `GameWalk10005.Read` retains entity ID zero for later dispatch; ordinary writes require nonzero ID | `GameWalkPacket.TryParse` rejects entity ID zero at the server boundary | A single shared validation rule would change current input handling |
| Account request 1060 | `StandardAccountRequest1060` writes a 16-byte identity field subject to the application's 1–15 ASCII name policy | `LoginAccountRequestPacket` decodes the field and the server applies its own authorization rules | Field representation and authorization policy must stay separate |

The compared sources live in
[`OpenConquer.Networking`](../../src/OpenConquer.Networking/) and
[`OpenConquer.Protocol`](https://github.com/berniemackie97/OpenConquerServer/tree/main/src/OpenConquer.Protocol).
Both use an eight-byte game trailer (`TQClient` or `TQServer`), a 1,024-byte
game packet limit and continuous directional CFB64 feedback. The repeated cipher,
frame encoder/decoder and packet-field code is a real maintenance cost: both
sides require review whenever a wire invariant changes.

A shared production package could remove some duplicate primitives. It would also
couple independent client/server releases and invite common-mode codec mistakes;
it cannot eliminate opposite signatures, inverse login transforms, different
admission rules, or distinct session, security and disposal ownership.
Independent implementations keep those boundaries explicit, at the cost of a
coordinated fixture review on protocol changes.

## Authority and change workflow

- The inspected 5517 client and retained, independently examined packet captures
  establish wire expectations. Neither an implementation nor agreement between the
  two repositories is a substitute for source evidence. Research references belong
  in the evidence ledger; unsafe captured secrets must never enter test fixtures.
- `OpenConquer.Client` owns signed realm selection and game-process coordination.
  `OpenConquer.Networking` owns wire serialization, read tolerances and account/game
  connection state. The server owns its
  parsing, authorization, session acceptance and transport boundaries in
  `OpenConquer.Protocol`, `OpenConquer.Transport` and its host projects.
- CP3.2 will establish a small reviewed corpus under
  `tests/Fixtures/WireGolden/` in the client repository. The server will check
  in a matching test-only snapshot; there is no runtime dependency and no
  build-time download. The corpus manifest must identify each case's direction,
  message, source-evidence reference, capture/derived/synthetic classification,
  expected bytes or failure, schema revision and payload hash. The server copy
  must record the client source commit and matching corpus hash.
- For a change to shared wire bytes or parser expectations, update the evidence
  first, then the corpus and both test suites through linked PRs. Review byte
  diffs, invalid-length/signature/order cases, fragmentation and split-direction
  cipher state independently before accepting either change. Each CI job validates
  its checked-in corpus; cross-repository equality must be reviewed against the
  pinned manifest without relying on network access during a build.
- Only reopen the production-sharing decision for a demonstrated maintenance
  or correctness problem that the common corpus cannot control. Do not add
  a compatibility assembly or protocol abstraction preemptively.

## Acceptance boundary

This document selects an ownership direction, not the packet evidence itself.
CP3.2 owns corpus creation, independent golden vectors, negative cases and CI
enforcement. This spike changes no codec, cryptography, framing, account policy
or transport behavior. Cross-platform test success does not establish independent
5517 captures or end-to-end network acceptance.
