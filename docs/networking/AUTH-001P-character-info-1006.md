# AUTH-001P — Native 5517 1006 character-information snapshot

Evidence: `20_notes/Network/MsgUserInfo1006-semantic-closure-2026-09-17.md`,
`MsgUserInfo1006-historical-field-reconciliation.md`, and
`Native-auth-string-validation-2026-10-18.md` with instruction-level
reader replay. Fixed fields and their exact offsets are independently
corroborated by the companion server `GameCharacterInfoPacket`.

## This slice

A bounded, **pure inbound snapshot decoder**. It does not mutate a hero,
send action 74, admit the character to the world, or authenticate the
remote peer. Existing secured framing caps packets at 1024 bytes.

Native 1006 validates the header/type, copies into constructor-zeroed
1024-byte storage, and queries string-list entries 0, 1, 2 separately.
There is **no exact three-entry count validation**, string-list
exhaustion requirement, or mandatory Windows-1252 decode. An absent
selected name, selected length >=16, or leading NUL yields an empty
native name; an embedded NUL keeps the prefix. String entry 1 is
discarded completely but its length determines spouse-entry location.
Extra string entries are not consumed.

The managed parser zero-fills **within a bounded local 1024-byte
snapshot** and retains the actual native fixed-field offsets, including
signed i16 PK points, raw retired compatibility fields, the unsigned
encoded pre-rebirth-level value, and decimal-packed subprofession
phases. Player/spouse names are **owned raw byte buffers**, not prematurely
interpreted as CP1252 Unicode. No source pooled frame reference escapes.

Native byte decoding occurs at the eventual UI consumer/code page,
not in the 1006 wire reader; invalid CP1252 slots and valid CP936
sequences therefore remain intact.

## Critical mutation/transport boundary

Native 1006 Process mutates hero name, spouse, ID and fixed-field state
**before** attempting EnterMap action74. It has **no nonzero-EntityId
or nonempty-name gate**. With EntityId0, action construction fails;
normal active-socket send handling can then close the socket, without
rolling back earlier hero writes.

This implementation deliberately does **not** emulate that
nontransactional mutation or unsafe send path. EntityId0 and
empty names remain representable in a decoded snapshot. Any future
hero-state transition and action74 send requires a **separate,
explicitly audited, fail-closed ownership/authorization design**.
A decoded snapshot alone is not proof of world admission.

## Regression gates

Use native-derived byte fixtures for missing/extra entries,
length255 ignored entry, overlong selected name, NUL prefixes,
CP936 bytes, zero-filled truncated field and selected string,
signed PK, packed phases, EntityId0, malformed header and >1024.
Require locked restore, Release build/full tests, format validation,
retail content verification, Linux/macOS/Windows CI, CodeQL,
dependency review and review signoff. Never auto-merge.
