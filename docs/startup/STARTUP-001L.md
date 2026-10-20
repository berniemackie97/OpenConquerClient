# STARTUP-001L: Standard 5517 account request framing

## Sources and scope

The research notes `20_notes/Network/13_connection_lifecycle.md` and `20_notes/Server/login-credential-envelope.md` show that the normal startup login message is type 1060, a 276-byte little-endian AccountServer message. The standard branch (as opposed to the OEM branch taken when `AccountSetup/Type == 1`) encrypts the first 32 bytes of the 128-byte credential field and leaves the remaining 96 bytes zero. In the 5517 client, `CMsg1060__Create` copies the account and the protocol server name with fixed-width `strncpy` and copies the encrypted credential with a fixed 128-byte `memcpy`. Zero bytes inside the encrypted region are real ciphertext and must not end the copy.

STARTUP-001L adds a serializer for that plaintext envelope and nothing more. The same input always produces the same bytes. The serializer is not connected to the login UI, sockets or the session lifecycle. The caller passes in ciphertext that the 5517 account credential pipeline has already produced. The serializer will accept any bytes in that position, but arbitrary bytes do not make a valid 5517 login request.

## Envelope

| Byte offset | Byte count | Data |
| --- | ---: | --- |
| `0` | 2 | Little-endian length: `276` |
| `2` | 2 | Little-endian packet type: `1060` |
| `4` | 128 | Caller-supplied ANSI account bytes, zero padded |
| `132` | 32 | RC5 credential ciphertext from the standard branch, already encrypted and copied as is |
| `164` | 96 | Zero bytes filling the rest of the 128-byte credential field |
| `260` | 16 | Caller-supplied protocol server name bytes, zero padded |

`StandardAccountRequest1060.Write` requires a destination of exactly 276 bytes, a nonempty account of at most 128 bytes, a nonempty protocol name of at most 16 bytes and an encrypted credential block of exactly 32 bytes. The account and protocol name may not contain NUL or ASCII control bytes. Bytes at or above `0x80` are kept as they are, so that a later ANSI encoding policy for 5517 names can use them. The serializer takes bytes rather than Unicode strings because the 5517 field lengths are byte lengths. It never applies UTF-8 or CP936 encoding implicitly, and it never truncates or replaces characters. An account of 128 bytes or a protocol name of 16 bytes fills its field with no NUL terminator, which matches the documented fixed-width copy in the 5517 client.

If validation fails, the destination is left untouched. Once validation passes, the inputs are copied to fixed-size stack buffers before the destination is cleared, so the output is the same even when an input overlaps the destination. The temporary 32-byte copy of the ciphertext is zeroed before the method returns. On the normal path, serialization makes no managed allocations, does no network I/O and has no other side effects. The finished frame belongs to the caller and contains sensitive binary data. **Whatever code later sends the frame must erase it once it is no longer needed.**

## Trust and missing pieces

No production code calls the serializer yet. The signed catalog remains the only source of the account server endpoint, and a server's display label can never be used for routing. When the serializer is integrated, the caller must reject any signed `protocolServerName` that cannot be encoded faithfully in the 16-byte field. Signed names are never clipped to fit. Several separate, reviewed components are needed before any login packet can be sent: the 5517 per-account keypad permutation, RC5 key derivation from the per-login seed, the standard 32-byte credential encryption, the AccountServer connection handshake, encrypted transport and server response parsing.

**Never send this plaintext packet directly on a socket.** The 5517 connection notes separate the handshake cipher from the encrypted packet transport used after the handshake, and they require the AccountServer session to be confirmed active before a request is forwarded. This class does not create a cipher context and does not check for one.

Nothing else is added: no login event, socket, read of the credential buffers, status icon, made-up endpoint, retail content or path to world entry. The serializer lives in the `OpenConquer.Networking` project, which owns protocol code. Its internals are visible only to `OpenConquer.Client.Tests`.

## Testing

Tests check the 276-byte envelope and its offsets, zero padding, zero bytes inside the ciphertext, names that fill a field completely, overlapping input and output buffers, identical output when a destination is reused, and rejection without partial writes. The usual checks also run: locked restore, format verification, Release build, the serializer tests, the full solution tests, retail content-set verification and Windows/macOS CI. No live login is expected to succeed with this change alone.
