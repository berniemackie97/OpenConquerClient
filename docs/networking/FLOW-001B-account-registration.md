# FLOW-001B — In-client account registration

Retail 5517 opened a signup website from the login screen's **Sign Up** button
(`CMD_GETURL` → `ShellExecuteA`). OpenConquer registers in place instead. The flow
and its two packets are a project extension; no retail resource exists for it.

## Presentation

Sign Up opens a modal **Create Account** dialog built only from retail art:

| Part | Source |
| --- | --- |
| Frame | clearMC's held picture (sprite 285), its flat body lengthened by 40 rows; same centre as clearMC, (361, 239) |
| Fields | The account panel's password box (shape 328), three times; text placed as enterMC field 364 |
| Buttons | Ok / Cancel 96x26 with their native Over and Down states |
| Text | Device Arial 14; labels, title and guidance in the field colour #908B37, errors in the dialogs' #FF4642 |
| Timeline | The account dialogs' ten frames: mask fades in, holds on frame 6, fades out; input only while held |

Account (≤16), Password and Confirm (≤14, masked) accept the native keypad
alphabet (letters, digits, `.`, `-`, `@`, `>`, `_`), which is exactly the server's
password alphabet and a subset of its name alphabet. Tab and Shift+Tab move
between fields and select their text, Enter or Ok submits, Escape or Cancel
closes. Clicks place the caret; drag, Ctrl+A and clipboard behave as in the login
fields, and password text never reaches the clipboard. The dialog shows the
account rules until a problem replaces them in red.

| Condition | Where | Text |
| --- | --- | --- |
| No usable realm at Sign Up | errorMC | "Select a server before signing up." |
| Account not 4–16 characters | dialog, account selected | "The account name must be 4 to 16 characters." |
| Password not 6–14 characters | dialog, both passwords cleared | "The password must be 6 to 14 characters." |
| Passwords differ | dialog, both passwords cleared | "The passwords do not match." |
| Realm lost before submit | dialog | "Select a server before signing up." |
| In flight | dialog locked | "Creating your account..." |
| Created (code 0) | dialog closes, name fills the login account field; errorMC | "Your account has been created. Enter your password to log in." |
| Name taken (code 2) | dialog, account selected | "That account name is already taken." |
| Policy refusal (code 1) | dialog, account selected | "That account name or password cannot be used." |
| Unavailable / rate limited (code 4) | dialog | "Sign up is unavailable right now. Please try again later." |
| Server failure (code 3) | dialog | "Your account could not be created. Please try again later." |
| AccountServer unreachable | dialog | StrRes 10397 (as login) |
| Lost or malformed reply, timeout, revocation | dialog | "The server did not answer. The account may exist, so try logging in first." |

A password problem clears both password fields because masked text cannot be
corrected in place. Submitting takes both passwords out of the form, so every
answer but an account problem asks for them again. Escape during the request
opens the native loading-quit dialog, as it does during login. The password limit
is 14, not the server's 15, because the English client refuses longer passwords
at login (StrRes 11400).

## Wire

The exchange runs on a fresh connection to the selected realm's AccountServer,
under the same signed-catalog and IPv4-loopback restrictions as login.

```text
1059 seed → 0xF001 (260 B) → 0xF002 (8 B) → close
```

`0xF001` is the first 256 bytes of 1060: the account field and the same
seed-keyed RC5/keypad credential field, so the server recovers exactly the
password a later login sends. There is no server name. `0xF002` carries a
`UInt32` result (0 created, 1 invalid, 2 taken, 3 failed, 4 unavailable); an
undefined code or any other frame is a protocol error. One session carries one
credential exchange, and a registration session is always closed afterwards.
The server side is documented in OpenConquerServer
`docs/architecture/account-registration.md`.

## Ownership

`LoginRegistrationForm` owns the dialog: its timeline, the three
`LoginTextField` editors (shared with the login fields), focus and message.
`LoginRegistrationCoordinator` decides whether the dialog opens, applies
`LoginRegistrationPolicy` in field order, compares the passwords in constant
time, and runs the one exchange. Taking the entries copies the name and password
into buffers the exchange zeroes on every path and wipes both password fields at
once; closing the dialog wipes them too. A catalog or selection change cancels
the request. Shutdown cancels and drains it.

## Verification

Tests cover the 0xF001 bytes against an independent server-side decoder and the
shared credential vector, every 0xF002 code against independently computed wire
frames, malformed and undefined replies, authority rejection, timeouts, one
exchange per session, the policy in field order, every result and its focus,
connection loss and revocation, the dialog's timeline, keys, limits, masking and
wiping, the screen's modality and routing, and the assembled artwork against its
retail sources. The dialog was checked in the running client from its rendered
frames. A live signup against a local AccountServer with registration enabled is
a separate smoke check.
