# Launcher instances

One launcher owns the product workflow per OS user, across desktop sessions and installation paths.
Opening it again requests activation of that user's existing window. This policy is separate from
any future machine-wide installation/update transaction lock.

## Startup and shutdown

1. Before opening diagnostics or Avalonia, `Program` resolves the activation namespace and attempts
   a user-scoped named mutex.
2. The owner creates the activation listener, then starts the desktop. It retains the mutex on the
   main thread until both desktop and listener shutdown have completed.
3. A second invocation requests activation and exits with code `0` after acknowledgement. If the
   owner exits during startup, it may acquire the released mutex and become the new owner.
4. After a five-second retry budget, an unresponsive owner produces a small explanatory window;
   closing that notice returns code `2`. It does not start installation work or another log writer.

The OS releases ownership after process termination. Once it owns the mutex, a replacement process
can recreate the Unix socket left by an interrupted owner. Ordinary shutdown cancels pending accepts,
reads, and queued UI activation before disposing the listener and releasing ownership. Unexpected
listener/application failures reach the existing redacted fatal-host boundary and return code `1`.

Activation restores a minimized window and requests foreground activation on the UI dispatcher.
The desktop window manager retains control of foreground-focus policy. Closing windows reject
activation; an aborted dispatcher request cannot reopen them later.

## Local channel

- Windows keeps its user-identity-derived named pipe. Unix uses the private namespace below.
- The mutex uses `CurrentUserOnly`, with session restriction disabled. Both pipe peers use
  `PipeOptions.CurrentUserOnly`; Unix socket permissions are additionally restricted to `0600`.
  Identity checks come from the OS, not the endpoint name or a client-supplied identifier.
- Each connection reads one eight-byte activation frame: `4F 43 4C 41 01 01 00 00` (magic, version,
  command, reserved bytes). The one-byte response is `01` for accepted or `02` for unavailable.
- Unknown frames and truncated connections are closed. No variable-length data is parsed; only one
  command is handled per connection. Reads and UI acknowledgement have a one-second deadline.
- The channel carries no arguments, paths, credentials, settings, or game-session data. It must not
  be reused for native account authentication or secure client handoff.

Runtime behavior follows [.NET pipe peer isolation](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.pipeoptions?view=net-10.0)
and [named mutex scoping](https://learn.microsoft.com/en-us/dotnet/api/system.threading.namedwaithandleoptions?view=net-10.0).

## Unix namespace

| Platform | Location |
| --- | --- |
| Linux | `$XDG_RUNTIME_DIR/openconquer/activate`; if unset, `/run/user/<effective UID>/openconquer/activate` |
| Linux without a runtime directory | `<validated home>/.openconquer-runtime/activate` |
| macOS | `confstr(_CS_DARWIN_USER_TEMP_DIR)` plus `openconquer/activate`; independent of `TMPDIR` |

Configured runtime roots must already exist, belong to the effective UID, and have mode `0700`.
An invalid configured root fails closed; only an absent default `/run/user` location permits the
home fallback. The home must belong to the user and be protected from writes by other users.
Overlong paths fail closed: UTF-8 paths may occupy at most 107 bytes on Linux or 103 on macOS.

`UnixActivationNamespace` traverses from `/` using `openat` with no-follow directory handles and
checks ownership/mode on those handles. Ancestors must belong to root or the effective UID and
exclude group/other writes, except root-owned sticky directories. macOS extended ACL grants are
rejected; deny-only ACLs are allowed. Apple's fixed `/var` alias is mapped to `/private/var` when
resolving the OS-provided path; arbitrary symlinks are never followed.

The application directory is created atomically with `mkdirat(..., 0700)` and validated again.
Existing permissions are never repaired. Before binding, the server revalidates its private parent
and permits only an absent endpoint or a same-user socket; it never replaces a symlink or ordinary
file. The mutex must be held before stale-socket replacement. Shutdown removes the socket, leaving
the private directory reusable. Cross-user protection assumes the OS/root and the current user
are trusted; a same-UID process can already alter that user's launcher and files.

The interop uses public Linux `statx` and Darwin `stat64` ABIs on x64/arm64, not private .NET APIs.
See the [XDG runtime contract](https://specifications.freedesktop.org/basedir/0.8/) and
[handle-relative filesystem metadata](https://man7.org/linux/man-pages/man2/statx.2.html).
There is no fallback to the old shared `/tmp/oc-launcher-*` endpoint. Close an old launcher before
starting the upgraded version. Sessions must use the same per-user runtime location for activation.

## Verification

`LauncherInstanceProcessTests` use the test-only `Launcher.InstanceProbe` executable to exercise
concurrent processes, activation, unresponsive ownership, normal release, and forced termination.
Transport tests cover invalid/truncated frames, idle peers, cancellation, application-failure
propagation, and Unix socket permissions. The probe is not part of either product publish.
Namespace tests cover unsafe modes, directory/endpoint links, writable ancestors, preserved foreign
objects, UTF-8 path limits, macOS ACLs, and process-termination recovery on both Unix platforms.

Linux CI stages the complete built C# probe in a temporary root-owned directory, readable/executable
but not writable by the test UIDs, and runs it with `--verify-cross-uid`. This avoids depending on
access to the runner's private checkout. An exit trap removes the staged copy on success or failure.
The root coordinator uses `setpriv` to run the launcher and attacker as UIDs 65534 and 1, with
matching GIDs, no supplementary groups/capabilities, and privilege escalation disabled. Actors
verify their kernel-reported identities and privileges; missing prerequisites fail the check.
Positive controls establish that filesystem/socket operations work in the attacker's own directory.
The test requires permission-denied results before startup, while listening, and after a forced
crash. It also verifies independent per-user ownership, normal activation, same-user stale recovery,
and rejection of a foreign runtime root. The attacker holds the exact obsolete `/tmp` endpoint
throughout startup and recovery. The test creates no user accounts.

To repeat in an **isolated Linux VM/container**, build Release and run the
“Verify launcher isolation across Unix users” step from [CI](../../.github/workflows/ci.yml).
The installed .NET host/runtime must also be readable/executable by both test UIDs. Do not loosen
checkout, home, or activation-directory permissions to accommodate the test.

Desktop smoke check: open a staged launcher, minimize it, and execute it again. The existing window
must return; the second process must exit successfully. Closing the owner must exit successfully,
and the next invocation must be able to become the owner.
