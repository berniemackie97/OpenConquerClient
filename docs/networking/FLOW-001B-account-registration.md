# FLOW-001B — In-client account registration

Retail 5517 opened a signup website from the login screen's **Sign Up** button
(`CMD_GETURL` → `ShellExecuteA`). OpenConquer registers in place instead. The flow
and its two packets are a project extension; no retail resource exists for it.

## Presentation

The account panel keeps its native fields, and every prompt uses the retail
errorMC dialog, so no new artwork is added.

| Step | Account panel | errorMC text |
| --- | --- | --- |
| Sign Up clicked | Sign Up held in its Down state | "Enter a new account name and password, then press Enter." |
| Enter, valid entry | Password cleared, first entry held | "Please enter the password again to confirm it." |
| Enter, confirmed | Input blocked while the request is in flight | — |
| Created | Leaves the flow; account name stays in its field | "Your account has been created. Enter your password to log in." |

Sign Up again, or Escape, leaves the flow before submission and wipes the held
entry. Escape during the request opens the native loading-quit dialog, as it
does during login. Enter while registering never starts a login.

| Condition | Text |
| --- | --- |
| Empty account / password | SWF constants `Please Enter Your User Name.` / `Please Enter Your Password.` |
| Account not 4–16 characters | "The account name must be 4-16 characters." |
| Password not 6–14 characters | "The password must be 6-14 characters." |
| Second password differs | "The passwords did not match. Please try again." |
| Account edited between entries | Restarts with the first prompt |
| No usable realm selection | StrRes 10012 (as login) |
| Name taken (code 2) | "That account name is already in use." |
| Policy refusal (code 1) | "That account name or password cannot be used." |
| Unavailable / rate limited (code 4) | "Account registration is unavailable. Please try again later." |
| Server failure (code 3) | "Your account could not be created. Please try again later." |
| AccountServer unreachable | StrRes 10397 (as login) |
| Lost or malformed reply, timeout | StrRes 100116 (as login); the outcome is unknown and not claimed |

The account and password fields only accept the native keypad alphabet
(letters, digits, `.`, `-`, `@`, `>`, `_`), which is exactly the server's
password alphabet and a subset of its name alphabet. The password limit is 14,
not the server's 15, because the English client refuses longer passwords at
login (StrRes 11400).

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

`LoginRegistrationCoordinator` owns the step, the held first entry and the
exchange. Both copies of the password are compared in constant time and wiped;
the held buffers move to the exchange, which zeroes them on every path. A
catalog or selection change cancels the request. Shutdown cancels and drains it.

## Verification

Tests cover the 0xF001 bytes against an independent server-side decoder and the
shared credential vector, every 0xF002 code against independently computed wire
frames, malformed and undefined replies, authority rejection, timeouts, one
exchange per session, every coordinator step and refusal, connection loss, and
the screen's routing of Sign Up, Enter and Escape. A live desktop signup against
a local AccountServer with registration enabled is a separate smoke check.
