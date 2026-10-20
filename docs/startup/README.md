# Startup and login screen notes

These notes record how the client's startup sequence and login screen were built, one change per
note, in the order the changes were made. Each note describes the design at the time of its change.
Later notes refine some earlier decisions, and later refactoring renamed some types. In particular,
the original `LoginPresentation` and `LoginVisualLayout` classes were split into `LoginScreen`,
`LoginScreenRenderer` and the `Account`, `Audio`, `Controls`, `Credentials`, `Selection` and `Stage`
folders under `src/OpenConquer.Client/UI/Login`.

For the current state of the client, see the [README](../../README.md) and the
[architecture overview](../architecture/architecture.md).

| Note | Topic |
| --- | --- |
| [STARTUP-001C](STARTUP-001C.md) | Pre-world login backdrop |
| [STARTUP-001D](STARTUP-001D.md) | Login chrome and account-panel alignment |
| [STARTUP-001E](STARTUP-001E.md) | Login selection chrome |
| [STARTUP-001F](STARTUP-001F.md) | Region/server selection model and input |
| [STARTUP-001G](STARTUP-001G.md) | Signed realm catalog download |
| [STARTUP-001H](STARTUP-001H.md) | Unicode text layout |
| [STARTUP-001I](STARTUP-001I.md) | Realm labels from the signed catalog |
| [STARTUP-001J](STARTUP-001J.md) | Realm catalog refresh |
| [STARTUP-001K](STARTUP-001K.md) | Credential entry before authentication |
| [STARTUP-001L](STARTUP-001L.md) | Standard 5517 account request framing |
| [STARTUP-001M](STARTUP-001M.md) | Region and server page buttons |
| [STARTUP-001N](STARTUP-001N.md) | Realm directory status strip |
| [STARTUP-001P](STARTUP-001P.md) | Retail account panel field alignment |
| [STARTUP-001Q](STARTUP-001Q.md) | Retail 5517 Enter and Exit buttons |
| [STARTUP-001R](STARTUP-001R.md) | Server icons in the signed realm directory |
| [STARTUP-001S](STARTUP-001S.md) | Authoring the signed realm directory |
| [STARTUP-001T](STARTUP-001T.md) | Original 5517 selection artwork |
| [STARTUP-001U](STARTUP-001U.md) | Local signed-realm builds and selection screen cleanup |
| [STARTUP-001V](STARTUP-001V.md) | Keeping the selection across signed directory renewals |
| [STARTUP-001W](STARTUP-001W.md) | 5517 login seed and credential cipher primitives |
| [STARTUP-001X](STARTUP-001X.md) | Original 5517 cursor and login sounds |
| [STARTUP-001Y](STARTUP-001Y.md) | 5517 login credential editing |
| [STARTUP-001Z](STARTUP-001Z.md) | Retail 5517 login screen |
