# AUTH-001P: 5517 1006 character information snapshot

Sources:
`20_notes/Network/MsgUserInfo1006-semantic-closure-2026-09-17.md`,
`MsgUserInfo1006-historical-field-reconciliation.md`, and
`Native-auth-string-validation-2026-10-18.md`, which includes an
instruction-level replay of the reader. The companion server's
`GameCharacterInfoPacket` independently confirms the fixed fields and
their offsets.

## Decoder

AUTH-001P adds a decoder that turns one inbound 1006 into a snapshot and
has no side effects. It does not change hero state, send action 74,
admit the character to the world or authenticate the remote peer. The
secured framing already limits packets to 1024 bytes.

The retail 1006 handler checks the header and type, copies the packet
into constructor-zeroed 1024-byte storage, and looks up string-list
entries 0, 1 and 2 one at a time. It does not check for a count of three
entries, does not require the string list to be used up, and does not
require a Windows-1252 decode. If the selected name is absent, has a
length of 16 or more, or starts with a NUL, the retail name is empty. An
embedded NUL keeps the bytes before it. String entry 1 is discarded
entirely, but its length decides where the spouse entry starts. Extra
string entries are not consumed.

The managed parser zero-fills a local 1024-byte snapshot and uses the
retail fixed-field offsets. These include PK points as a signed i16,
retired compatibility fields kept raw, the pre-rebirth level as an
unsigned encoded value, and subprofession phases packed as decimal
digits. The player and spouse names are raw byte buffers owned by the
snapshot. They are not decoded as CP1252 into Unicode at this stage. No
reference to the pooled source frame escapes.

Decoding the raw bytes is left to the UI code that eventually displays
them, with its own code page, not to the 1006 wire reader. Bytes that
are invalid in CP1252 and valid CP936 sequences therefore stay intact.

## Hero state and action 74

The retail 1006 Process step writes the hero's name, spouse, ID and
fixed-field state before it tries EnterMap action 74. It does not check
for a nonzero EntityId or a nonempty name. With EntityId 0, building the
action fails. The normal send handling for an active socket can then
close the socket, and the hero writes made earlier are not rolled back.

This client does not copy that partial, non-transactional update or the
unsafe send path. A decoded snapshot can still hold EntityId 0 and empty
names. Any later hero-state update and sending action 74 will need a
separate, reviewed design for ownership and authorization that rejects
anything invalid. A decoded snapshot alone does not mean the character
has entered the world.

## Testing

Byte fixtures derived from the 5517 client cover missing and extra
entries, an ignored entry with length 255, an overlong selected name,
NUL prefixes, CP936 bytes, a truncated packet where a fixed field and
the selected string are zero-filled, signed PK, packed phases, an
EntityId of 0, a malformed header and a packet over 1024 bytes. CI runs
locked restore, the Release build and full tests, format validation,
retail content verification, Linux, macOS and Windows builds, CodeQL and
dependency review.
