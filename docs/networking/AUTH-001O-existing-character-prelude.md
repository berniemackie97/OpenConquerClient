# AUTH-001O — Existing-character 2078/2079 prelude (native compatibility correction)

Original implementation: PR #133, client main `fa3dcd81c419e9a048cfebff562eef013c4dcb75`.
Evidence: `20_notes/Network/10_extended_packets.md` and
`20_notes/Network/Native-auth-string-validation-2026-10-18.md`.

The current companion server sends `1004 ANSWER_OK → 2078 → 2079 →
1006`. Native 5517 does **not** require 2078/2079 before processing
1006. `ReadExistingCharacterPreludeAsync` is a **companion-server-specific,
exclusive, one-shot read**; it is not the retail dispatcher's universal
prerequisite, and it cannot grant world admission.

## 2078

Wire fields begin `[u16 length][u16 type=2078][u32 timestamp][u8 flag]`,
followed by account location bytes. Native `CHero_SetAccountLoginAlertData`
at `0x6577BF` copies the first NUL-terminated byte prefix without
Windows-1252 validation; later text display uses its own code-page path.
This implementation retains an **owned byte snapshot** of the location
(`LastLoginLocationBytes`), not a fixed CP1252 string. All five bytes
often called undefined Windows-1252 slots are legitimate raw values;
valid multibyte content is not decoded during protocol parsing.

After an explicit NUL, bounded padding is ignored. The native
constructor-zeroed packet storage also supplies an implicit terminator
for short packets; a size-9 history with no location is therefore
accepted. An unterminated maximal 1024-byte packet is rejected to
avoid replicating native unbounded `strlen`. The fixed timestamp/flag
fields, packet header/type and maximum packet size remain bounded.

## 2079

The wire state is a single u32 at +4. Native state 0 selects UI global
2000, state 1 selects 2001, and unknown values cause **no update**.
Bounded trailing bytes are ignored. No 2079 changes were required.

## Ownership, safety and limits

The existing session controls the socket, pooled decrypted frames,
continuous CAST5 cipher, terminal failure and one total two-frame
deadline. The prelude reader does not perform native hero mutations,
world admission, 1006 consumption or EnterMap action 74. Strict
frame type/order is the current companion-server contract rather
than an intrinsic native 5517 requirement.

Regression gates cover raw CP1252/CP936 bytes without premature
Unicode conversion; zero-padded short and explicit-NUL forms;
extreme packet lengths and rejection of unsafe full-length
unterminated storage; state semantics; and encrypted loopback
lifetime/concurrency behavior. Full solution CI is mandatory.
