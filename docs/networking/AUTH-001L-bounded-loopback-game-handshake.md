# AUTH-001L: Loopback GameServer DH handshake

## Sources

Builds on AUTH-001A through AUTH-001K, which end with the secured
framing and the negotiated CAST5 streams. The handshake follows the
companion server's `GameConnectionSession`,
`GameTransportHandshakeProcessor` and `GameHandshakeExchange`, its
reader for the client DH response, and its handshake and connection
tests. The retail 5517 sources are
`20_notes/Network/13_connection_lifecycle.md` and the earlier AUTH
documents.

## Handshake sequence

The first thing on a GameServer socket is not an ordinary secured
packet. The server sends a raw DH challenge encrypted with CAST5-CFB64
under a fixed key. The first 15 decrypted bytes give the total envelope
length, which has an upper limit. The client answers with one raw
`TQClient` DH response, encrypted under the fixed key by a separately
initialized cipher. Only after that response is written does the
GameServer session switch to the negotiated directional CAST5 streams.
The first secured packet after that is the standard 1052 login proof,
which AUTH-001L does not send.

## Starting a handshake

A `LoginAccountExchangeResult` can be claimed once to start a GameServer
handshake. The claim is allowed only after a successful 1055 result and
after both 5517 AccountServer reports (1100 and the resource-version
1052) have completed. The redirect must be the literal `127.0.0.1` with
a valid port. The claim fails before any connection attempt if the
result is a failure, a report has not completed, the result is disposed,
the redirect is remote, or the result was already claimed.

The transport always connects to `IPAddress.Loopback` with an IPv4
`TcpClient`. It does no DNS lookup, never connects to an address chosen
by the caller, and does not treat the 1055 redirect as the identity of a
remote peer. The AccountServer's loopback-only credential check is
unchanged. Disposing the AccountServer result cancels a GameServer
connect or handshake that is still running. Once the handoff succeeds,
the new GameServer session owns its socket and session cipher on its
own.

**This is for local testing only.** The 5517 handshake uses 512-bit
Diffie-Hellman and unauthenticated CAST5-CFB64, which cannot
authenticate a remote server or protect message integrity. Supporting
remote servers will need a separate way to authenticate the endpoint;
removing the loopback check is not enough.

## Timeouts and session lifetime

`GameServerHandshakeTimeouts` holds two separate limits: one for the TCP
connect and one for the whole handshake after it. Each must be positive
and at most 30 seconds. The handshake limit covers receiving and
validating the challenge, the DH calculation, building and writing the
encrypted response, and handing over the cipher. Cancellation by the
caller is passed through. When the internal connect or handshake
deadline expires, the error is reported as a timeout. Timeouts are never
retried.

`GameServerHandshakeSession.OpenAsync` creates a new IPv4 TCP client and
owns it from the first connect attempt. It sends nothing until it has
received and validated the server's challenge. It reads 15 encrypted
bytes and decrypts them with a single continuous bootstrap decryptor. It
checks the declared length against the limit, then reads and decrypts
the remaining bytes and no more. It never reads ahead into a later
secured frame and does not reset the CFB state between those reads.

The AUTH-001G parser rejects malformed challenge chunks, IVs, DH group
or public key, and trailer. AUTH-001I then builds a new client DH
response from cryptographically random values, and AUTH-001H encrypts it
with a separate bootstrap cipher instance that starts from a zero IV.
The response is written to TCP once. A partial write or one with an
unknown outcome, a timeout, cancellation, an invalid frame or EOF ends
the session and closes the socket. There is no reconnect or resend. The
negotiated AUTH-001J session cipher is handed over only after the write
returns successfully. A successful return from
`NetworkStream.WriteAsync` does not mean the server received the
response or admitted the client.

The new session is the only owner of the socket and the negotiated
cipher until it is disposed. Disposal is asynchronous and idempotent. At
this stage the session has no public API for sending or reading
GameServer packets, and the UI has no login path to it. AUTH-001M adds
secured-frame I/O with a size limit and sends the 1052 proof on this
session.

## Memory, concurrency and ownership

Handshake plaintext is at most 1035 bytes. It is allocated once for one
challenge and erased in a `finally` block. The encrypted DH response is
a single allocation of its exact size and is erased on every path,
success or error. AUTH-001I clears the secret and IV buffers it owns
after handing them over (they are used once). AUTH-001J owns the
negotiated directional CAST5 feedback registers. The host does not log
credentials, ticket material, session keys, redirects or packets.

The account result guards its single claim with its existing lock. The
new session's socket and cipher have one owner, and disposal closes the
socket and disposes the cipher. There are no unbounded queues,
background loops, connection retries or hidden tasks. The response
buffer is not kept after the write. The crypto provider and the
BigInteger implementation may keep managed copies of key material, so
zeroing the arrays this code owns is not a complete erasure of the
provider's internal memory.

## Testing

Loopback tests run the real client AccountServer seed, the 1060/1055
exchange and the post-authentication reports, then deliver the
GameServer challenge in fragments. The challenge ciphertext is the
AUTH-001H reference, which was generated independently. A mock server
decrypts and parses the new client response using the 5517 two-chunk
`TQClient` layout, computes the DH shared secret on its own from the
fixture server exponent, and sends secured session ciphertext that the
client decrypts.

Negative tests cover a rejected remote redirect, a failed account
result, repeated claims, an invalid timeout, a call that was canceled
before it started, a malformed encrypted challenge, a handshake timeout,
and cancellation by disposing the account result. No GameServer proof is
sent. CI runs locked restore, the formatter, Release build and tests,
retail content verification, the Linux Quality, macOS and Windows jobs,
and Dependency Review.

## Out of scope

AUTH-001L does not connect to a GameServer on a public network, send
credentials or the GameServer login proof, redeem a ticket, admit the
player to gameplay, provide a secured-frame stream reader or writer,
enable the UI, or trust a remote peer. A DH negotiation that completed
locally without authentication does not imply any of these.
