# AUTH-001L — Bounded loopback GameServer DH handshake transport

## Audited baseline and protocol authority

Client `main` `df955197efbc19f454e8bf31ad7b30619fa29842` includes merged
AUTH-001A through AUTH-001K, ending with the secured-framing and negotiated
CAST5 streams from PR #129. The companion server implementation was checked
against `GameConnectionSession`, `GameTransportHandshakeProcessor`,
`GameHandshakeExchange`, the server-side client-DH-response reader,
and its handshake/connection tests. Native retail 5517 evidence remains in
`20_notes/Network/13_connection_lifecycle.md` and preceding AUTH documents.

A GameServer socket does **not** receive an ordinary secured packet first.
The server sends a fixed-key CAST5-CFB64-encrypted **raw DH challenge**
whose first 15 decrypted bytes establish a bounded total envelope length.
The client replies with one separately initialized, fixed-key-encrypted raw
`TQClient` DH response. Only after that response is written does the
GameServer session switch to the negotiated directional CAST5 streams.
The standard 1052 login proof is the first subsequent secured packet;
**AUTH-001L deliberately does not send that proof**.

## Authority and strict scope

The `LoginAccountExchangeResult` permits **one** claim to begin a GameServer
handshake, and only after its successful 1055 result has completed both
native AccountServer reports (1100 and resource-version 1052). It requires
a literal `127.0.0.1` redirect and a valid port. A failure result, missing
report completion, disposed result, unsupported remote redirect or repeated
claim fails before connecting.

The transport unconditionally connects to `IPAddress.Loopback` with an
IPv4 `TcpClient`; it does not resolve DNS, connect to a caller-selected
address, or trust a 1055 redirect as a remote peer identity. The existing
AccountServer loopback-only credential guard remains intact.
Disposing the AccountServer result cancels any still-active GameServer
connect/handshake operation. Once successfully handed off, the new
GameServer session owns its socket and session cipher independently.

**This is local interoperability infrastructure, not a remotely secure
connection mode.** Native legacy 512-bit DH and unauthenticated CAST5-CFB64
cannot authenticate a remote endpoint or provide modern message integrity.
Future real-remote support requires independently authenticated endpoint
policy and explicit deployment design, not removal of this loopback gate.

## Session and timeout contract

`GameServerHandshakeTimeouts` validates separate positive, maximum
30-second limits for TCP connect and the subsequent entire handshake.
The handshake limit includes challenge reception, validation, DH
calculation, encrypted response construction/write and cipher transfer.
User cancellation propagates. Internal connect/handshake expiration is
reported as a timeout, and no timeout is retried.

`GameServerHandshakeSession.OpenAsync` owns a fresh IPv4 TCP client from
its first connect attempt. It sends **no data before receiving and
validating** the server's challenge. It reads precisely 15 encrypted bytes,
decrypts them with one continuous bootstrap decryptor, validates the
declared bounded length, then reads/decrypts the **exact remaining bytes**.
It does not read ahead into a future secured frame or reset CFB state
between those reads.

The original strict AUTH-001G parser rejects malformed challenge chunks,
IVs, DH group/public key and trailer. AUTH-001I then creates a fresh
cryptographically random client DH response; AUTH-001H encrypts it under
a separate zero-IV bootstrap instance; the response is written **once**
to TCP. An uncertain/partial write, timeout, cancellation, invalid frame
or EOF is terminal and closes the socket, without a reconnect or resend.
The negotiated AUTH-001J session cipher is transferred only after that
write returns successfully. Returning from `NetworkStream.WriteAsync`
does **not** prove receipt or admission by the server.

The new session exclusively owns the socket and the negotiated cipher
until its idempotent asynchronous disposal. It deliberately exposes no
public GameServer send/read protocol or UI login path in this slice:
AUTH-001M may add bounded secured-frame I/O and native 1052 submission
against this owned session after a separate audit.

## Memory, concurrency and ownership

Handshake plaintext is limited to 1035 bytes, allocated once for one
challenge and erased in a `finally` block. The encrypted DH response is
one exactly sized allocation and is erased on every success/error path.
AUTH-001I clears its owned secret/IV buffers after single-use transfer;
AUTH-001J owns the negotiated CAST5 directional feedback registers.
The host itself does not log credentials, ticket material, session keys,
redirects or packets.

The account result uses its existing lock to protect the single claim.
The new session's socket and cipher have one lifetime owner; disposal
closes the socket and disposes the cipher. There are no unbounded queues,
background loops, connection retries, or hidden tasks. The response
buffer is never retained after a write. The underlying provider and
BigInteger implementations may retain managed copies of key material:
zeroing our owned arrays must not be described as complete erasure of
provider-internal memory.

## Regression evidence and acceptance

Loopback tests exercise the actual client AccountServer seed, 1060/1055
exchange and post-authentication reports, followed by GameServer challenge
fragmentation. The challenge ciphertext is the previously independently
generated AUTH-001H reference. A mock peer decrypts and parses the new
client response using the native two-chunk `TQClient` contract, independently
calculates the DH shared secret using the fixture server exponent, and
produces a secured session ciphertext that the client decrypts.

Negative tests verify remote redirect rejection, failure account result,
repeat claims, invalid timeout/pre-canceled call, malformed encrypted
challenge, handshake timeout and account-disposal cancellation. No GameServer
proof is sent. CI requires locked restore, formatter, Release build/test,
retail content verification, Linux Quality/macOS/Windows, Dependency Review,
independent review resolution, and explicit human merge approval.

## Out of scope

No public-network GameServer connection, credential transmission,
GameServer login proof transmission, ticket redemption, gameplay admission,
secured-frame stream reader/writer, UI enablement, or remote peer trust is
implemented here. Those capabilities must not be inferred from a locally
completed unauthenticated DH negotiation.
