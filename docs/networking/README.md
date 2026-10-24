# Login networking notes

These notes record how the client's AccountServer and GameServer login networking was built, one
change per note, in the order the changes were made. Each note describes the design at the time of
its change, and later notes build on earlier ones. Notes that name server types, such as
`GameConnectionSession`, refer to the
[OpenConquerServer](https://github.com/berniemackie97/OpenConquerServer) repository.

For the current state of the client, see the [README](../../README.md) and the
[architecture overview](../architecture/architecture.md).

| Note | Topic |
| --- | --- |
| [AUTH-001A](AUTH-001A-account-wire-codecs.md) | AccountServer wire codecs |
| [AUTH-001B](AUTH-001B-bounded-account-session.md) | AccountServer seed session |
| [AUTH-001C](AUTH-001C-native-credential-composition.md) | 5517 keypad encoding and account request composition |
| [AUTH-001D](AUTH-001D-bounded-account-exchange.md) | AccountServer authentication exchange |
| [AUTH-001E](AUTH-001E-account-post-authentication-reports.md) | AccountServer post-authentication reports |
| [AUTH-001F](AUTH-001F-offline-game-login-proof.md) | Standard GameServer login proof (offline) |
| [AUTH-001G](AUTH-001G-game-server-challenge-envelope.md) | GameServer DH challenge envelope (offline) |
| [AUTH-001H](AUTH-001H-game-bootstrap-cast5-cfb64.md) | GameServer bootstrap CAST5-CFB64 |
| [AUTH-001I](AUTH-001I-offline-game-dh-response.md) | Client GameServer DH response and session material (offline) |
| [AUTH-001J](AUTH-001J-game-session-cast5-cfb64.md) | GameServer session CAST5-CFB64 (offline) |
| [AUTH-001K](AUTH-001K-game-secured-framing.md) | Secured GameServer framing (offline) |
| [AUTH-001L](AUTH-001L-bounded-loopback-game-handshake.md) | Loopback GameServer DH handshake |
| [AUTH-001M](AUTH-001M-game-login-proof-and-secured-io.md) | Loopback GameServer 1052 proof and secured inbound I/O |
| [AUTH-001N](AUTH-001N-game-login-control.md) | First GameServer login control after the proof |
| [AUTH-001O](AUTH-001O-existing-character-prelude.md) | Existing-character 2078/2079 prelude |
| [AUTH-001P](AUTH-001P-character-info-1006.md) | 5517 1006 character information snapshot |
| [AUTH-001Q](AUTH-001Q-character-info-receipt.md) | Reading character information (1006) after the prelude |
| [FLOW-001A](FLOW-001A-live-login-bootstrap.md) | Desktop login against the live servers and native error presentation |
| [FLOW-001B](FLOW-001B-account-registration.md) | In-client account registration (OpenConquer extension) |
